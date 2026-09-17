using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PrestamosCobros.BLL.DTOs;
using PrestamosCobros.BLL.Interfaces;
using PrestamosCobros.DAL.Entities;
using PrestamosCobros.DAL.Repositories;
using PrestamosCobros.Infrastructure.Audit;

using PrestamosCobros.BLL.Services;

namespace PrestamosCobros.Web.Controllers;

[Authorize]
public class PrestamosController : Controller
{
    private readonly IPrestamoService _prestamoService;
    private readonly IPagoService _pagoService;
    private readonly IAuditoriaService _auditoria;
    private readonly UserManager<Usuario> _userManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly PrestamosCobros.Infrastructure.Interfaces.IComprobantePdfService _pdfService;
    private readonly WhatsAppService _whatsAppService;

    public PrestamosController(
        IPrestamoService prestamoService,
        IPagoService pagoService,
        IAuditoriaService auditoria,
        UserManager<Usuario> userManager,
        IUnitOfWork unitOfWork,
        PrestamosCobros.Infrastructure.Interfaces.IComprobantePdfService pdfService,
        WhatsAppService whatsAppService)
    {
        _prestamoService = prestamoService;
        _pagoService = pagoService;
        _auditoria = auditoria;
        _userManager = userManager;
        _unitOfWork = unitOfWork;
        _pdfService = pdfService;
        _whatsAppService = whatsAppService;
    }

    public async Task<IActionResult> Index(string? estado, string? busqueda)
    {
        var prestamos = await _prestamoService.ObtenerTodosAsync(estado, busqueda);
        ViewData["Estado"] = estado;
        ViewData["Busqueda"] = busqueda;
        return View(prestamos);
    }

    [Authorize(Roles = "Admin,Asistente")]
    public async Task<IActionResult> Crear(int? clienteId)
    {
        var defaultSinpe = await _unitOfWork.Preferencias.Query()
            .FirstOrDefaultAsync(p => p.Clave == "Default_Sinpe");
        var defaultIban1 = await _unitOfWork.Preferencias.Query()
            .FirstOrDefaultAsync(p => p.Clave == "Default_Iban1");
        var defaultIban2 = await _unitOfWork.Preferencias.Query()
            .FirstOrDefaultAsync(p => p.Clave == "Default_Iban2");
        var defaultIban3 = await _unitOfWork.Preferencias.Query()
            .FirstOrDefaultAsync(p => p.Clave == "Default_Iban3");

        var model = new PrestamoCreateDto
        {
            FechaInicio = DateTime.Today,
            PorcentajeInteres = 20,
            PeriodicidadDias = 7,
            NumeroCuotas = 4,
            NumeroSinpe = defaultSinpe?.Valor ?? "",
            CuentaIban1 = defaultIban1?.Valor ?? "",
            CuentaIban2 = defaultIban2?.Valor ?? "",
            CuentaIban3 = defaultIban3?.Valor ?? ""
        };

        if (clienteId.HasValue)
            model.ClienteId = clienteId.Value;

        await CargarClientesSelectList(model.ClienteId);
        return View(model);
    }

    [HttpPost]
    //[ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Asistente")]
    public async Task<IActionResult> Crear(PrestamoCreateDto model)
    {
        if (!ModelState.IsValid)
        {
            await CargarClientesSelectList(model.ClienteId);
            return View(model);
        }

        var (exito, mensaje, prestamoId) = await _prestamoService.CrearAsync(model);
        if (!exito)
        {
            ModelState.AddModelError("", mensaje);
            await CargarClientesSelectList(model.ClienteId);
            return View(model);
        }

        var user = await _userManager.GetUserAsync(User);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditoria.RegistrarAsync(user!.Id, "Crear préstamo",
            $"Préstamo ID={prestamoId} para ClienteId={model.ClienteId}, Monto=₡{model.MontoPrestado:N2}", ip);

        var isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest" || 
                     Request.Headers["Accept"].ToString().Contains("application/json");

        if (isAjax)
        {
            // Enviar WhatsApp (Proyección Espejo 3%)
            try
            {
                var prestamo = await _unitOfWork.Prestamos.Query()
                    .Include(p => p.Cliente)
                    .Include(p => p.Cuotas)
                    .FirstOrDefaultAsync(p => p.PrestamoId == prestamoId);

                if (prestamo != null)
                {
                    var cliente = prestamo.Cliente;
                    var monto = prestamo.MontoPrestado;
                    var numCuotas = prestamo.Cuotas.Count > 0 ? prestamo.Cuotas.Count : 1;
                    
                    var montoBase = prestamo.MontoTotal;
                    var interesTotal = montoBase - monto;
                    var gastoLey = Math.Round(montoBase * 0.03m, 0);
                    var montoCreditoVisible = montoBase - gastoLey;
                    var cuotaVisual = Math.Round(montoBase / numCuotas, 0);
                    var difRedondeo = montoBase - (cuotaVisual * numCuotas);

                    var partesCuotas = new List<string>();
                    for (int i = 1; i <= numCuotas; i++)
                    {
                        var fechaCuota = prestamo.FechaInicio.AddDays(prestamo.PeriodicidadDias * i);
                        var fechaFormateada = fechaCuota.ToString("dd MMM").ToUpper();
                        var montoCuotaActual = (i == numCuotas) ? cuotaVisual + difRedondeo : cuotaVisual;
                        partesCuotas.Add($"\U0001f539 Cuota #{i:D2} ({fechaFormateada}): \u20a1{montoCuotaActual.ToString("N0")}");
                    }

                    var detalleCuotas = string.Join("  |  ", partesCuotas).Replace("\n", " ").Replace("\r", " ").Replace("\t", " ").Trim();

                    var templateName = (await _unitOfWork.Preferencias.Query()
                        .FirstOrDefaultAsync(p => p.Clave == "Notificacion_ProyeccionTemplateName"))?.Valor ?? "proyeccion_credito_v1";

                    var exitoWa = await _whatsAppService.EnviarMensajePlantillaAsync(
                        cliente.Telefono, templateName, "es", new string[] {
                            cliente.NombreCompleto.Trim(), montoCreditoVisible.ToString("N0"), gastoLey.ToString("N0"),
                            cuotaVisual.ToString("N0"), detalleCuotas, montoBase.ToString("N0")
                        }
                    );

                    if (exitoWa)
                    {
                        var primeraCuota = prestamo.Cuotas.OrderBy(c => c.NumeroCuota).FirstOrDefault();
                        await _unitOfWork.Notificaciones.AddAsync(new Notificacion
                        {
                            PrestamoId = prestamo.PrestamoId, CuotaId = primeraCuota?.CuotaId ?? 0,
                            ClienteId = cliente.ClienteId, Tipo = "ProyeccionWhatsApp",
                            Mensaje = $"Proyección enviada. Capital Unificado: \u20a1{montoBase:N0}, Gasto Ley: \u20a1{gastoLey:N0}",
                            Estado = "Enviado", FechaEnvio = DateTime.UtcNow, Canal = "WhatsApp"
                        });
                        await _unitOfWork.SaveChangesAsync();
                    }
                }
            }
            catch { /* Log error, but proceed with creation response */ }

            return Ok(new { success = true, message = "El crédito ha sido creado con éxito y se ha enviado la proyección de crédito al cliente." });
        }

        TempData["Mensaje"] = mensaje;
        return RedirectToAction("Detalle", new { id = prestamoId });
    }

    // AJAX: Preview del plan de cuotas
    [HttpPost]
    public async Task<IActionResult> Preview([FromBody] PrestamoCreateDto model)
    {
        if (model.MontoPrestado <= 0 || model.NumeroCuotas <= 0)
            return BadRequest();

        var preview = await _prestamoService.GenerarPreviewAsync(model);
        return Json(preview);
    }

    public async Task<IActionResult> Detalle(int id)
    {
        var prestamo = await _prestamoService.ObtenerPorIdAsync(id);
        if (prestamo == null) return NotFound();
        return View(prestamo);
    }

    [HttpPost]
    //[ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Asistente,Cobrador")]
    public async Task<IActionResult> RegistrarPago(int cuotaId, decimal montoPagado, int prestamoId, bool esPagoSoloInteres = false)
    {
        var model = new PagoCreateDto { CuotaId = cuotaId, MontoPagado = montoPagado, EsPagoSoloInteres = esPagoSoloInteres };
        var user = await _userManager.GetUserAsync(User);
        var (exito, mensaje, pagoId) = await _pagoService.RegistrarPagoAsync(model, user!.Id);

        var isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest" || 
                     (Request.Headers["Accept"].ToString().Contains("application/json"));

        if (!exito)
        {
            if (isAjax) return BadRequest(new { exito = false, mensaje });
            TempData["Error"] = mensaje;
            return RedirectToAction("Detalle", new { id = prestamoId });
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditoria.RegistrarAsync(user.Id, "Registrar pago",
            $"Pago ID={pagoId}, CuotaId={model.CuotaId}, Monto=₡{model.MontoPagado:N2}", ip);

        // Enviar comprobante automáticamente
        try
        {
            var pago = await _pagoService.ObtenerPagoAsync(pagoId);
            if (pago != null)
            {
                var pagoEntity = await _unitOfWork.Pagos.Query()
                    .Include(p => p.Cuota)
                        .ThenInclude(c => c.Prestamo)
                    .FirstOrDefaultAsync(p => p.PagoId == pagoId);

                var clienteId = pagoEntity?.Cuota.Prestamo.ClienteId ?? 0;

                var refPago = $"{pago.PagoId:D6}";
                var montoStr = pago.MontoPagado.ToString("N0");
                var cliente = pago.ClienteNombre;
                var cuotaStr = $"{pago.NumeroCuota}";
                var fechaHora = pago.FechaPago.ToString("dd/MM/yyyy HH:mm");
                var saldo = pago.SaldoPendiente.ToString("N0");
                var proximo = pago.FechaProximoPago.HasValue ? pago.FechaProximoPago.Value.ToString("dd/MM/yyyy") : "Liquidado";

                var templateName = "";
                string[] parametros;
                
                if (esPagoSoloInteres && pagoEntity?.Cuota.Prestamo.TipoCredito == "Cuota Completa")
                {
                    templateName = (await _unitOfWork.Preferencias.Query()
                        .FirstOrDefaultAsync(p => p.Clave == "Notificacion_ComprobanteSoloInteresTemplateName"))?.Valor ?? "comprobante_de_solo_interes"; // Escenario A
                    
                    var capitalPendienteCuota = await _unitOfWork.Cuotas.Query()
                        .Where(c => c.PrestamoId == pagoEntity.Cuota.PrestamoId 
                                 && c.NumeroCuota == pagoEntity.Cuota.NumeroCuota 
                                 && c.Estado == "Pendiente")
                        .Select(c => c.Monto)
                        .FirstOrDefaultAsync();

                    parametros = new string[] { 
                        $"PAG-{refPago}", 
                        cliente, 
                        montoStr, 
                        cuotaStr, 
                        fechaHora, 
                        capitalPendienteCuota.ToString("N0"), 
                        saldo, 
                        proximo 
                    };
                }
                else if (pagoEntity?.Cuota.Prestamo.TipoCredito == "Solo Interés")
                {
                    templateName = (await _unitOfWork.Preferencias.Query()
                        .FirstOrDefaultAsync(p => p.Clave == "Notificacion_ComprobanteInteresTemplateName"))?.Valor ?? "comprobante_pago_interes"; // Escenario B
                    parametros = new string[] { 
                        $"PAG-{refPago}", 
                        montoStr, 
                        cliente, 
                        fechaHora, 
                        saldo, 
                        proximo 
                    };
                }
                else
                {
                    templateName = (await _unitOfWork.Preferencias.Query()
                        .FirstOrDefaultAsync(p => p.Clave == "Notificacion_ComprobanteTemplateName"))?.Valor 
                        ?? "comprobante_pago"; // Escenario C
                    parametros = new string[] { refPago, montoStr, cliente, cuotaStr, fechaHora, saldo, proximo };
                }

                var exitoWa = await _whatsAppService.EnviarMensajePlantillaAsync(
                    pago.ClienteTelefono,
                    templateName,
                    "es",
                    parametros
                );

                if (exitoWa)
                {
                    await _unitOfWork.Notificaciones.AddAsync(new Notificacion
                    {
                        PrestamoId = pago.PrestamoId,
                        CuotaId = pago.CuotaId,
                        ClienteId = clienteId,
                        Tipo = "ComprobanteWhatsAppAuto",
                        Mensaje = $"Comprobante automático de Pago enviado por WhatsApp. Ref: PAG-{refPago}, Monto: ₡{montoStr}",
                        Estado = "Enviado",
                        FechaEnvio = DateTime.UtcNow,
                        Canal = "WhatsApp"
                    });
                    await _unitOfWork.SaveChangesAsync();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al enviar comprobante automático: {ex.Message}");
        }

        if (isAjax)
        {
            var pagoDetalle = await _pagoService.ObtenerPagoAsync(pagoId);
            return Ok(new { exito = true, mensaje, pago = pagoDetalle });
        }

        TempData["Mensaje"] = mensaje;
        TempData["PagoId"] = pagoId;
        return RedirectToAction("Detalle", new { id = prestamoId });
    }

    [HttpGet]
    public async Task<IActionResult> DescargarPdf(int id)
    {
        var detalle = await _pagoService.ObtenerPagoAsync(id);
        if (detalle == null) return NotFound();

        var pdfBytes = _pdfService.GenerarComprobante(
            detalle.ClienteNombre, 
            detalle.MontoPagado, 
            detalle.FechaPago, 
            detalle.NumeroCuota, 
            detalle.SaldoPendiente, 
            detalle.PagoId);

        return File(pdfBytes, "application/pdf", $"Comprobante_PAG_{detalle.PagoId:D6}.pdf");
    }

    public async Task<IActionResult> Comprobante(int id)
    {
        // id = CuotaId — buscar el pago correspondiente
        var pago = await _unitOfWork.Pagos.Query()
            .FirstOrDefaultAsync(p => p.CuotaId == id);

        if (pago == null) return NotFound();

        var detalle = await _pagoService.ObtenerPagoAsync(pago.PagoId);
        if (detalle == null) return NotFound();

        ViewData["TextoComprobante"] = await _pagoService.GenerarTextoComprobanteAsync(pago.PagoId);
        return View(detalle);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Master")]
    public async Task<IActionResult> AnularPago(int cuotaId, string motivo, int prestamoId)
    {
        var pago = await _unitOfWork.Pagos.Query().OrderByDescending(p => p.PagoId).FirstOrDefaultAsync(p => p.CuotaId == cuotaId);
        if (pago == null) 
        {
            TempData["Error"] = "No se encontró el recibo de pago para esta cuota.";
            return RedirectToAction("Detalle", new { id = prestamoId });
        }

        var user = await _userManager.GetUserAsync(User);
        var (exito, mensaje) = await _pagoService.AnularPagoAsync(pago.PagoId, motivo, user!.Id);

        if (exito)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _auditoria.RegistrarAsync(user.Id, "Anular pago", $"Pago ID={pago.PagoId}, Motivo={motivo}", ip);
            
            TempData["Mensaje"] = mensaje;
        }
        else
        {
            TempData["Error"] = mensaje;
        }

        return RedirectToAction("Detalle", new { id = prestamoId });
    }


    [HttpPost]
    //[ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Cancelar(int id)
    {
        var (exito, mensaje) = await _prestamoService.CancelarAsync(id);
        if (!exito)
        {
            TempData["Error"] = mensaje;
            return RedirectToAction("Detalle", new { id });
        }

        var user = await _userManager.GetUserAsync(User);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditoria.RegistrarAsync(user!.Id, "Cancelar préstamo",
            $"Préstamo ID={id} cancelado", ip);

        TempData["Mensaje"] = mensaje;
        return RedirectToAction("Detalle", new { id });
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Master")]
    public async Task<IActionResult> AbonoCapital(int id, decimal montoAbono)
    {
        var user = await _userManager.GetUserAsync(User);
        var (exito, mensaje) = await _prestamoService.AbonarCapitalAsync(id, montoAbono, user!.Id);
        
        if (exito)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _auditoria.RegistrarAsync(user.Id, "Abono a capital",
                $"Préstamo ID={id} Abono=₡{montoAbono:N2}", ip);
            TempData["Mensaje"] = mensaje;
        }
        else
        {
            TempData["Error"] = mensaje;
        }

        return RedirectToAction("Detalle", new { id });
    }

    [HttpPost]
    //[ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Reactivar(int id)
    {
        var (exito, mensaje) = await _prestamoService.ReactivarAsync(id);
        TempData["Mensaje"] = mensaje;
        return RedirectToAction("Detalle", new { id });
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Asistente")]
    public async Task<IActionResult> Renovar([FromBody] PrestamoRenovarDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var (exito, mensaje, nuevoId) = await _prestamoService.RenovarAsync(dto);
        if (!exito) return BadRequest(new { success = false, message = mensaje });

        var user = await _userManager.GetUserAsync(User);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditoria.RegistrarAsync(user!.Id, "Renovar préstamo",
            $"Préstamo ID={dto.PrestamoIdActual} renovado en nuevo ID={nuevoId}", ip);

        try
        {
            var nuevoPrestamo = await _unitOfWork.Prestamos.Query()
                .Include(p => p.Cliente)
                .Include(p => p.Cuotas)
                .FirstOrDefaultAsync(p => p.PrestamoId == nuevoId);

            if (nuevoPrestamo != null)
            {
                var primeraCuota = nuevoPrestamo.Cuotas.OrderBy(c => c.NumeroCuota).FirstOrDefault();
                var cuotaEstimada = primeraCuota?.Monto.ToString("N0") ?? "0";
                var fechaPrimerPago = primeraCuota?.FechaVencimiento.ToString("dd/MM/yyyy") ?? "-";

                var templateName = (await _unitOfWork.Preferencias.Query()
                    .FirstOrDefaultAsync(p => p.Clave == "Notificacion_RenovacionTemplateName"))?.Valor ?? "renovacion_de_credito";
                
                var prestamoAnterior = await _unitOfWork.Prestamos.Query().FirstOrDefaultAsync(p => p.PrestamoId == dto.PrestamoIdActual);
                var saldoAnteriorVal = prestamoAnterior?.SaldoPendiente ?? 0m;
                var saldoAnterior = saldoAnteriorVal.ToString("N0");
                var montoAdicional = (nuevoPrestamo.MontoPrestado - saldoAnteriorVal).ToString("N0");
                
                var cuotasDetalle = string.Join(" | ", nuevoPrestamo.Cuotas.OrderBy(c => c.NumeroCuota).Select(c => 
                    $"Cuota #{c.NumeroCuota:00} ({c.FechaVencimiento.ToString("dd MMM").ToUpper()}): ₡{c.Monto:N0}"));
                
                if (cuotasDetalle.Length > 800) cuotasDetalle = cuotasDetalle.Substring(0, 795) + "..."; // Meta limits length

                var parametros = new string[] {
                    nuevoPrestamo.Cliente.NombreCompleto.Trim(), // {{1}}
                    saldoAnterior, // {{2}}
                    montoAdicional, // {{3}}
                    nuevoPrestamo.MontoPrestado.ToString("N0"), // {{4}}
                    nuevoPrestamo.PorcentajeInteres.ToString("G29"), // {{5}}
                    cuotaEstimada, // {{6}}
                    cuotasDetalle, // {{7}}
                    nuevoPrestamo.MontoTotal.ToString("N0"), // {{8}}
                    fechaPrimerPago // {{9}}
                };

                var exitoWa = await _whatsAppService.EnviarMensajePlantillaAsync(
                    nuevoPrestamo.Cliente.Telefono,
                    templateName,
                    "es",
                    parametros
                );

                if (exitoWa && primeraCuota != null)
                {
                    await _unitOfWork.Notificaciones.AddAsync(new Notificacion
                    {
                        PrestamoId = nuevoId,
                        CuotaId = primeraCuota.CuotaId,
                        ClienteId = nuevoPrestamo.ClienteId,
                        Tipo = "RenovacionWhatsApp",
                        Mensaje = $"Renovación de Crédito. Nuevo Capital: ₡{nuevoPrestamo.MontoPrestado:N0}",
                        Estado = "Enviado",
                        FechaEnvio = DateTime.UtcNow,
                        Canal = "WhatsApp"
                    });
                    await _unitOfWork.SaveChangesAsync();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error enviando WA en renovación: {ex.Message}");
            var prestamo = await _unitOfWork.Prestamos.Query().Include(p => p.Cuotas).FirstOrDefaultAsync(p => p.PrestamoId == nuevoId);
            if (prestamo != null)
            {
                var pCuota = prestamo.Cuotas.OrderBy(c => c.NumeroCuota).FirstOrDefault();
                if (pCuota != null) 
                {
                    await _unitOfWork.Notificaciones.AddAsync(new Notificacion
                    {
                        PrestamoId = prestamo.PrestamoId,
                        CuotaId = pCuota.CuotaId,
                        ClienteId = prestamo.ClienteId,
                        Tipo = "RenovacionWhatsApp",
                        Mensaje = $"Error al enviar renovación: {ex.Message}",
                        Estado = "Error",
                        FechaEnvio = DateTime.UtcNow,
                        Canal = "WhatsApp"
                    });
                    await _unitOfWork.SaveChangesAsync();
                }
            }
        }

        return Ok(new { success = true, message = mensaje, nuevoId = nuevoId });
    }

    [HttpGet]
    [HttpPost]
    //[ValidateAntiForgeryToken]
    [Authorize(Roles = "Master,Admin")]
    public async Task<IActionResult> Eliminar(int id)
    {
        var prestamo = await _prestamoService.ObtenerPorIdAsync(id);

        // Los Administradores solo pueden eliminar créditos inactivos
        if (User.IsInRole("Admin") && !User.IsInRole("Master"))
        {
            if (prestamo != null && prestamo.Estado == "Activo")
            {
                TempData["Error"] = "No tienes permisos para eliminar créditos activos. Solo el Master puede hacerlo.";
                return RedirectToAction("Detalle", new { id });
            }
        }

        var (exito, mensaje) = await _prestamoService.EliminarAsync(id);
        if (!exito)
        {
            TempData["Error"] = mensaje;
            return RedirectToAction("Detalle", new { id });
        }

        var user = await _userManager.GetUserAsync(User);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditoria.RegistrarAsync(user?.Id ?? 0, "Eliminar préstamo",
            $"Préstamo ID={id} eliminado permanentemente", ip);

        TempData["Mensaje"] = mensaje;
        if (prestamo != null)
        {
            return RedirectToAction("Detalle", "Clientes", new { id = prestamo.ClienteId });
        }
        return RedirectToAction("Index");
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Asistente,Cobrador")]
    //[ValidateAntiForgeryToken]
    public async Task<IActionResult> EnviarComprobanteWhatsApp(int id)
    {
        try
        {
            var pago = await _pagoService.ObtenerPagoAsync(id);
            if (pago == null) return NotFound();

            var pagoEntity = await _unitOfWork.Pagos.Query()
                .Include(p => p.Cuota)
                    .ThenInclude(c => c.Prestamo)
                .FirstOrDefaultAsync(p => p.PagoId == id);

            var clienteId = pagoEntity?.Cuota.Prestamo.ClienteId ?? 0;

            var refPago = $"{pago.PagoId:D6}";
            var monto = pago.MontoPagado.ToString("N0");
            var cliente = pago.ClienteNombre;
            var cuota = $"{pago.NumeroCuota}";
            var fechaHora = pago.FechaPago.ToString("dd/MM/yyyy HH:mm");
            var saldo = pago.SaldoPendiente.ToString("N0");
            var proximo = pago.FechaProximoPago.HasValue ? pago.FechaProximoPago.Value.ToString("dd/MM/yyyy") : "Liquidado";

            var templateName = (await _unitOfWork.Preferencias.Query()
                .FirstOrDefaultAsync(p => p.Clave == "Notificacion_ComprobanteTemplateName"))?.Valor 
                ?? "comprobante_pago";

            var exito = await _whatsAppService.EnviarMensajePlantillaAsync(
                pago.ClienteTelefono,
                templateName,
                "es",
                new string[] { refPago, monto, cliente, cuota, fechaHora, saldo, proximo }
            );

            if (exito)
            {
                // Registrar log
                await _unitOfWork.Notificaciones.AddAsync(new Notificacion
                {
                    PrestamoId = pago.PrestamoId,
                    CuotaId = pago.CuotaId,
                    ClienteId = clienteId,
                    Tipo = "ComprobanteWhatsApp",
                    Mensaje = $"Comprobante de Pago enviado por WhatsApp. Ref: PAG-{refPago}, Monto: ₡{monto}",
                    Estado = "Enviado",
                    FechaEnvio = DateTime.UtcNow,
                    Canal = "WhatsApp"
                });
                await _unitOfWork.SaveChangesAsync();

                return Ok(new { success = true, message = "Comprobante enviado por WhatsApp exitosamente usando la API Cloud." });
            }
            else
            {
                return BadRequest(new { success = false, message = "No se pudo enviar la plantilla de comprobante por WhatsApp." });
            }
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Roles = "Master")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarCuota(int cuotaId)
    {
        var cuota = await _unitOfWork.Cuotas.GetByIdAsync(cuotaId);
        if (cuota == null) return NotFound();

        var tienePago = await _unitOfWork.Pagos.Query().AnyAsync(p => p.CuotaId == cuotaId && p.Estado != "Anulado");
        if (tienePago) return BadRequest(new { exito = false, mensaje = "No se puede eliminar una cuota con pagos activos." });

        _unitOfWork.Cuotas.Remove(cuota);
        await _unitOfWork.SaveChangesAsync();

        return Ok(new { exito = true, mensaje = "Cuota eliminada correctamente." });
    }

    [HttpPost]
    [Authorize(Roles = "Master")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditarMontoCuota(int cuotaId, decimal nuevoMonto)
    {
        if (nuevoMonto <= 0) return BadRequest(new { exito = false, mensaje = "Monto inválido." });

        var cuota = await _unitOfWork.Cuotas.GetByIdAsync(cuotaId);
        if (cuota == null) return NotFound();

        cuota.Monto = nuevoMonto;
        _unitOfWork.Cuotas.Update(cuota);
        await _unitOfWork.SaveChangesAsync();

        return Ok(new { exito = true, mensaje = "Monto de cuota actualizado." });
    }

    private async Task CargarClientesSelectList(int selectedId = 0)
    {
        var clientes = await _unitOfWork.Clientes.Query()
            .Where(c => c.Activo)
            .OrderBy(c => c.NombreCompleto)
            .Select(c => new { c.ClienteId, Texto = c.NombreCompleto + " - " + c.Telefono })
            .ToListAsync();

        ViewBag.Clientes = new SelectList(clientes, "ClienteId", "Texto", selectedId);
    }
}
