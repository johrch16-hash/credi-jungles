using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrestamosCobros.BLL.DTOs;
using Microsoft.EntityFrameworkCore;
using PrestamosCobros.BLL.Interfaces;
using PrestamosCobros.BLL.Services;
using PrestamosCobros.DAL.Entities;
using PrestamosCobros.DAL.Repositories;

namespace PrestamosCobros.Web.Controllers;

[Authorize(Roles = "Admin,Asistente")]
public class ProyeccionController : Controller
{
    private readonly IPrestamoService _prestamoService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly WhatsAppService _whatsAppService;

    public ProyeccionController(
        IPrestamoService prestamoService,
        IUnitOfWork unitOfWork,
        WhatsAppService whatsAppService)
    {
        _prestamoService = prestamoService;
        _unitOfWork = unitOfWork;
        _whatsAppService = whatsAppService;
    }

    /// <summary>
    /// Envía la proyección de un préstamo por WhatsApp usando la "capa espejo" al 3% mensual (Ley de Usura).
    /// Los datos financieros enviados al cliente se recalculan al 3%, pero la tasa real del negocio
    /// permanece intacta en la base de datos.
    /// 
    /// Plantilla Meta (6 variables):
    /// {{1}} = Nombre del cliente
    /// {{2}} = Capital (monto prestado)
    /// {{3}} = Interés total visual (3% Ley)
    /// {{4}} = Cuota pactada visual (3% Ley)
    /// {{5}} = Detalle de cuotas en línea horizontal
    /// {{6}} = Monto total a pagar visual (3% Ley)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var clientes = await _unitOfWork.Clientes.Query()
            .Where(c => c.Activo)
            .OrderBy(c => c.NombreCompleto)
            .ToListAsync();
            
        ViewBag.Clientes = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(clientes, "ClienteId", "NombreCompleto");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnviarWhatsApp(int prestamoId)
    {
        try
        {
            // ── 1. Obtener el préstamo real de la BD ──
            var prestamo = await _unitOfWork.Prestamos.Query()
                .Include(p => p.Cliente)
                .Include(p => p.Cuotas)
                .FirstOrDefaultAsync(p => p.PrestamoId == prestamoId);

            if (prestamo == null)
                return NotFound(new { success = false, message = "Préstamo no encontrado." });

            var cliente = prestamo.Cliente;
            var monto = prestamo.MontoPrestado;
            var numeroCuotas = prestamo.Cuotas.Count > 0 ? prestamo.Cuotas.Count : 1;
            var periodicidadDias = prestamo.PeriodicidadDias;
            var fechaInicio = prestamo.FechaInicio;

            // ── 2. Capa Espejo: Recalcular al 3% mensual (tope Ley de Usura) ──
            // Interés simple lineal: Capital × 3% × meses
            // Convertimos la periodicidad a meses aproximados
            var totalDias = periodicidadDias * numeroCuotas;
            var mesesAproximados = Math.Round((decimal)totalDias / 30m, 0, MidpointRounding.AwayFromZero);
            if (mesesAproximados < 1) mesesAproximados = 1m;

            // Interés total visual al 3% mensual
            var tasaMensualLey = 0.03m;
            var interesTotalVisual = Math.Round(monto * tasaMensualLey * mesesAproximados, 0);
            var montoTotalVisual = monto + interesTotalVisual;
            // Redondear la cuota visual a la centena más cercana
            var cuotaVisual = Math.Round((montoTotalVisual / numeroCuotas) / 100m) * 100m;

            // Ajustar para que el total cuadre exactamente
            var montoTotalAjustado = cuotaVisual * numeroCuotas;
            var diferenciaRedondeo = montoTotalVisual - montoTotalAjustado;

            // ── 3. Generar detalle de cuotas en formato horizontal ──
            var partesCuotas = new List<string>();
            for (int i = 1; i <= numeroCuotas; i++)
            {
                var fechaCuota = fechaInicio.AddDays(periodicidadDias * i);
                var fechaFormateada = fechaCuota.ToString("dd MMM").ToUpper();

                // Última cuota absorbe la diferencia de redondeo
                var montoCuotaActual = (i == numeroCuotas)
                    ? cuotaVisual + diferenciaRedondeo
                    : cuotaVisual;

                partesCuotas.Add($"\U0001f539 Cuota #{i:D2} ({fechaFormateada}): \u20a1{montoCuotaActual.ToString("N0")}");
            }

            // Concatenar horizontalmente separado por "  |  " (sin saltos de línea)
            var detalleCuotas = string.Join("  |  ", partesCuotas);

            // ── 4. Sanitizar: Limpiar saltos de línea y tabs para cumplir con Meta ──
            detalleCuotas = detalleCuotas
                .Replace("\n", " ")
                .Replace("\r", " ")
                .Replace("\t", " ")
                .Trim();

            // ── 5. Preparar las 6 variables de la plantilla ──
            var param1_nombre = cliente.NombreCompleto.Trim();
            var param2_capital = monto.ToString("N0");
            var param3_interes = interesTotalVisual.ToString("N0");
            var param4_cuota = cuotaVisual.ToString("N0");
            var param5_detalle = detalleCuotas;
            var param6_total = montoTotalVisual.ToString("N0");

            // ── 6. Obtener nombre de plantilla de configuración ──
            var templateName = (await _unitOfWork.Preferencias.Query()
                .FirstOrDefaultAsync(p => p.Clave == "Notificacion_ProyeccionTemplateName"))?.Valor
                ?? "proyeccion_credito_v1";

            // ── 7. Enviar vía WhatsApp Cloud API ──
            var exito = await _whatsAppService.EnviarMensajePlantillaAsync(
                cliente.Telefono,
                templateName,
                "es",
                new string[]
                {
                    param1_nombre,
                    param2_capital,
                    param3_interes,
                    param4_cuota,
                    param5_detalle,
                    param6_total
                }
            );

            if (exito)
            {
                // Registrar en historial de notificaciones
                var primeraCuota = prestamo.Cuotas.OrderBy(c => c.NumeroCuota).FirstOrDefault();
                await _unitOfWork.Notificaciones.AddAsync(new Notificacion
                {
                    PrestamoId = prestamo.PrestamoId,
                    CuotaId = primeraCuota?.CuotaId ?? 0,
                    ClienteId = cliente.ClienteId,
                    Tipo = "ProyeccionWhatsApp",
                    Mensaje = $"Proyección de crédito enviada por WhatsApp. Capital: \u20a1{param2_capital}, Total (3% Ley): \u20a1{param6_total}, {numeroCuotas} cuotas de \u20a1{param4_cuota}",
                    Estado = "Enviado",
                    FechaEnvio = DateTime.UtcNow,
                    Canal = "WhatsApp"
                });
                await _unitOfWork.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = $"Proyección enviada exitosamente al cliente {param1_nombre} vía WhatsApp (tasa visual: 3% Ley de Usura)."
                });
            }
            else
            {
                return BadRequest(new
                {
                    success = false,
                    message = "No se pudo enviar la proyección por WhatsApp. Revisa la configuración de la API."
                });
            }
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = $"Error al enviar proyección: {ex.Message}" });
        }
    }

    /// <summary>
    /// Endpoint AJAX para previsualizar los datos de la proyección al 3% sin enviarla.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Preview(int prestamoId)
    {
        var prestamo = await _unitOfWork.Prestamos.Query()
            .Include(p => p.Cliente)
            .Include(p => p.Cuotas)
            .FirstOrDefaultAsync(p => p.PrestamoId == prestamoId);

        if (prestamo == null)
            return NotFound(new { success = false, message = "Préstamo no encontrado." });

        var monto = prestamo.MontoPrestado;
        var numeroCuotas = prestamo.Cuotas.Count > 0 ? prestamo.Cuotas.Count : 1;
        var periodicidadDias = prestamo.PeriodicidadDias;
        var totalDias = periodicidadDias * numeroCuotas;
        var mesesAproximados = Math.Round((decimal)totalDias / 30m, 0, MidpointRounding.AwayFromZero);
        if (mesesAproximados < 1) mesesAproximados = 1m;

        var tasaMensualLey = 0.03m;
        var interesTotalVisual = Math.Round(monto * tasaMensualLey * mesesAproximados, 0);
        var montoTotalVisual = monto + interesTotalVisual;
        // Redondear la cuota visual a la centena más cercana
        var cuotaVisual = Math.Round((montoTotalVisual / numeroCuotas) / 100m) * 100m;

        return Ok(new
        {
            success = true,
            clienteNombre = prestamo.Cliente.NombreCompleto,
            capital = monto.ToString("N0"),
            interes3Porciento = interesTotalVisual.ToString("N0"),
            cuotaVisual = cuotaVisual.ToString("N0"),
            montoTotal = montoTotalVisual.ToString("N0"),
            numeroCuotas,
            tasaRealNegocio = prestamo.PorcentajeInteres.ToString("N2") + "%",
            montoTotalReal = prestamo.MontoTotal.ToString("N0")
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnviarWhatsAppSimulacion([FromBody] ProyeccionSimulacionDto dto)
    {
        try
        {
            var telefono = dto.TelefonoCliente;
            if (dto.ClienteId > 0)
            {
                var cliente = await _unitOfWork.Clientes.Query().FirstOrDefaultAsync(c => c.ClienteId == dto.ClienteId);
                if (cliente != null) telefono = cliente.Telefono;
            }

            if (string.IsNullOrEmpty(telefono))
            {
                return BadRequest(new { success = false, message = "No hay teléfono válido para enviar el mensaje." });
            }

            var templateName = (await _unitOfWork.Preferencias.Query()
                .FirstOrDefaultAsync(p => p.Clave == "Notificacion_ProyeccionTemplateName"))?.Valor
                ?? "proyeccion_credito_v1";

            var exito = await _whatsAppService.EnviarMensajePlantillaAsync(
                telefono,
                templateName,
                "es",
                new string[]
                {
                    dto.NombreCliente.Trim(),
                    dto.CapitalFormateado,
                    dto.InteresFormateado,
                    dto.CuotaFormateada,
                    dto.DetalleCuotas,
                    dto.TotalFormateado
                }
            );

            if (exito)
            {
                return Ok(new { success = true });
            }
            else
            {
                return BadRequest(new { success = false, message = "No se pudo enviar la proyección por WhatsApp. Revisa la configuración de la API." });
            }
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = $"Error al enviar proyección: {ex.Message}" });
        }
    }
}
