using Microsoft.EntityFrameworkCore;
using Hangfire;
using PrestamosCobros.BLL.DTOs;
using PrestamosCobros.BLL.Interfaces;
using PrestamosCobros.DAL.Repositories;
using PrestamosCobros.Infrastructure.Interfaces;

namespace PrestamosCobros.BLL.Services;

public class PagoService : IPagoService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IComprobantePdfService _pdfService;
    private readonly IEmailService _emailService;
    private readonly WhatsAppService _whatsAppService;

    public PagoService(
        IUnitOfWork unitOfWork,
        IBackgroundJobClient backgroundJobClient,
        IComprobantePdfService pdfService,
        IEmailService emailService,
        WhatsAppService whatsAppService)
    {
        _unitOfWork = unitOfWork;
        _backgroundJobClient = backgroundJobClient;
        _pdfService = pdfService;
        _emailService = emailService;
        _whatsAppService = whatsAppService;
    }

    public async Task<(bool Exito, string Mensaje, int PagoId)> RegistrarPagoAsync(PagoCreateDto dto, int usuarioId)
    {
        var cuota = await _unitOfWork.Cuotas.Query()
            .Include(c => c.Prestamo)
            .Include(c => c.Pago)
            .FirstOrDefaultAsync(c => c.CuotaId == dto.CuotaId);

        if (cuota == null)
            return (false, "Cuota no encontrada.", 0);

        DAL.Entities.Pago pago;

        if (cuota.Pago != null)
        {
            if (cuota.Pago.Estado == "Anulado")
            {
                // Reutilizar el pago anulado
                pago = cuota.Pago;
                pago.UsuarioId = usuarioId;
                pago.MontoPagado = dto.MontoPagado;
                pago.FechaPago = DateTime.UtcNow;
                pago.Estado = "Activo";
                pago.MotivoAnulacion = null;
                
                _unitOfWork.Pagos.Update(pago);
            }
            else
            {
                return (false, "Esta cuota ya fue pagada.", 0);
            }
        }
        else
        {
            // Registrar nuevo pago
            pago = new DAL.Entities.Pago
            {
                CuotaId = dto.CuotaId,
                UsuarioId = usuarioId,
                MontoPagado = dto.MontoPagado,
                FechaPago = DateTime.UtcNow,
                Estado = "Activo"
            };
            await _unitOfWork.Pagos.AddAsync(pago);
        }

        if (cuota.Prestamo.Estado != "Activo")
            return (false, "El préstamo no está activo.", 0);

        // Actualizar cuota
        if (dto.EsPagoSoloInteres && cuota.Prestamo.TipoCredito == "Cuota Completa")
        {
            // Escenario A: Traditional credit, only interest paid.
            // Guardamos el monto y número original porque la cuota pendiente debe mantenerse íntegra
            var montoOriginal = cuota.Monto;
            var numeroOriginal = cuota.NumeroCuota;
            
            cuota.Monto = dto.MontoPagado;
            cuota.Estado = "Pagado";
            cuota.FechaPago = DateTime.UtcNow;
            cuota.NumeroCuota = 0; // 0 significa "Abono de Interés"
            _unitOfWork.Cuotas.Update(cuota);

            // Creamos una nueva cuota pendiente con el monto y número original intacto
            var nuevaCuota = new DAL.Entities.Cuota
            {
                PrestamoId = cuota.PrestamoId,
                NumeroCuota = numeroOriginal,
                Monto = montoOriginal,
                FechaVencimiento = cuota.FechaVencimiento, // Continúa venciendo el mismo día
                Estado = "Pendiente"
            };
            await _unitOfWork.Cuotas.AddAsync(nuevaCuota);
        }
        else if (cuota.Prestamo.TipoCredito == "Solo Interés")
        {
            // Escenario B: Credit "Solo Interés", only interest paid.
            cuota.Estado = "Pagado";
            cuota.FechaPago = DateTime.UtcNow;
            _unitOfWork.Cuotas.Update(cuota);

            // Generate next interest cuota
            var nextCuota = new DAL.Entities.Cuota
            {
                PrestamoId = cuota.PrestamoId,
                NumeroCuota = cuota.NumeroCuota + 1,
                Monto = cuota.Monto, // Se mantiene la misma cuota proporcional del ciclo
                FechaVencimiento = cuota.FechaVencimiento.AddDays(cuota.Prestamo.PeriodicidadDias),
                Estado = "Pendiente"
            };
            await _unitOfWork.Cuotas.AddAsync(nextCuota);
        }
        else
        {
            // Escenario C: Full quota payment
            cuota.Estado = "Pagado";
            cuota.FechaPago = DateTime.UtcNow;
            _unitOfWork.Cuotas.Update(cuota);

            // Amortización: Si el pago excede el monto de la cuota, reducir el excedente de las cuotas futuras
            var excedente = dto.MontoPagado - cuota.Monto;
            if (excedente > 0)
            {
                var cuotasPendientes = await _unitOfWork.Cuotas.Query()
                    .Where(c => c.PrestamoId == cuota.PrestamoId && c.Estado != "Pagado" && c.CuotaId != cuota.CuotaId)
                    .OrderBy(c => c.NumeroCuota)
                    .ToListAsync();

                foreach (var cPendiente in cuotasPendientes)
                {
                    if (excedente <= 0) break;

                    if (cPendiente.Monto > excedente)
                    {
                        cPendiente.Monto -= excedente;
                        excedente = 0;
                    }
                    else
                    {
                        excedente -= cPendiente.Monto;
                        cPendiente.Monto = 0;
                        cPendiente.Estado = "Pagado";
                        cPendiente.FechaPago = DateTime.UtcNow;
                    }
                    _unitOfWork.Cuotas.Update(cPendiente);
                }
            }
        }

        // Actualizar saldo del préstamo if it's not a Solo Interés payment where capital isn't reduced
        if (!dto.EsPagoSoloInteres && cuota.Prestamo.TipoCredito != "Solo Interés")
        {
            cuota.Prestamo.SaldoPendiente -= dto.MontoPagado;
            if (cuota.Prestamo.SaldoPendiente <= 0)
            {
                cuota.Prestamo.SaldoPendiente = 0;
                cuota.Prestamo.Estado = "Liquidado";
            }
            _unitOfWork.Prestamos.Update(cuota.Prestamo);
        }

        await _unitOfWork.SaveChangesAsync();

        // Enqueue background job to process the receipt
        _backgroundJobClient.Enqueue<IPagoService>(x => x.ProcesarReciboAsync(pago.PagoId));

        return (true, "Pago registrado exitosamente.", pago.PagoId);
    }

    public async Task<PagoDetalleDto?> ObtenerPagoAsync(int pagoId)
    {
        var pago = await _unitOfWork.Pagos.Query()
            .Include(p => p.Cuota)
                .ThenInclude(c => c.Prestamo)
                    .ThenInclude(pr => pr.Cliente)
            .Include(p => p.Usuario)
            .FirstOrDefaultAsync(p => p.PagoId == pagoId);

        if (pago == null) return null;

        var proximaCuota = await _unitOfWork.Cuotas.Query()
            .Where(c => c.PrestamoId == pago.Cuota.PrestamoId && c.Estado != "Pagado")
            .OrderBy(c => c.NumeroCuota)
            .FirstOrDefaultAsync();

        return new PagoDetalleDto
        {
            PagoId = pago.PagoId,
            CuotaId = pago.CuotaId,
            NumeroCuota = pago.Cuota.NumeroCuota,
            MontoPagado = pago.MontoPagado,
            FechaPago = pago.FechaPago,
            UsuarioNombre = pago.Usuario.NombreCompleto,
            ComprobanteEnviado = pago.ComprobanteEnviado,
            PrestamoId = pago.Cuota.PrestamoId,
            ClienteNombre = pago.Cuota.Prestamo.Cliente.NombreCompleto,
            ClienteTelefono = pago.Cuota.Prestamo.Cliente.Telefono,
            ClienteEmail = pago.Cuota.Prestamo.Cliente.Email,
            SaldoPendiente = pago.Cuota.Prestamo.SaldoPendiente,
            FechaProximoPago = proximaCuota?.FechaVencimiento
        };
    }

    public async Task<string> GenerarTextoComprobanteAsync(int pagoId)
    {
        var pago = await ObtenerPagoAsync(pagoId);
        if (pago == null) return "";

        var proximoPagoText = pago.FechaProximoPago.HasValue 
            ? $"📅 Próximo pago: {pago.FechaProximoPago.Value:dd/MM/yyyy}\n" 
            : "📅 Próximo pago: N/A (Liquidado)\n";

        var textoAdicional = (await _unitOfWork.Preferencias.Query()
            .FirstOrDefaultAsync(p => p.Clave == "Comprobante_TextoAdicional"))?.Valor;

        var extraInfo = !string.IsNullOrWhiteSpace(textoAdicional)
            ? $"\n━━━━━━━━━━━━━━━━\n📝 *Notas/Cuentas:*\n{textoAdicional}\n"
            : "";

        return $"📋 *Comprobante de Pago*\n" +
               $"━━━━━━━━━━━━━━━━\n" +
               $"👤 Cliente: {pago.ClienteNombre}\n" +
               $"📱 Teléfono: {pago.ClienteTelefono}\n" +
               $"💰 Cuota #{pago.NumeroCuota}: ₡{pago.MontoPagado:N0}\n" +
               $"📅 Fecha: {pago.FechaPago:dd/MM/yyyy HH:mm}\n" +
               proximoPagoText +
               $"💳 Saldo pendiente: ₡{pago.SaldoPendiente:N0}\n" +
               extraInfo +
               $"━━━━━━━━━━━━━━━━\n" +
               $"Registrado por: {pago.UsuarioNombre}\n" +
               $"Ref: PAG-{pago.PagoId:D6}";
    }

    public async Task ProcesarReciboAsync(int pagoId)
    {
        var pago = await ObtenerPagoAsync(pagoId);
        if (pago == null || string.IsNullOrEmpty(pago.ClienteEmail)) return;

        var pdfBytes = _pdfService.GenerarComprobante(
            pago.ClienteNombre,
            pago.MontoPagado,
            pago.FechaPago,
            pago.NumeroCuota,
            pago.SaldoPendiente,
            pago.PagoId);

        var asunto = $"Comprobante de Pago Cuota #{pago.NumeroCuota} - Sistema de Préstamos";
        var cuerpo = $"Hola {pago.ClienteNombre},<br><br>Adjunto enviamos el comprobante de pago de la cuota #{pago.NumeroCuota} por el monto de ₡{pago.MontoPagado:N2}.<br><br>Su saldo actual es de ₡{pago.SaldoPendiente:N2}.<br><br>Gracias.";

        await _emailService.EnviarCorreoAsync(pago.ClienteEmail, asunto, cuerpo, pdfBytes, $"Comprobante_Pago_{pago.PagoId:D6}.pdf");

        // Actualizar el estado de comprobante enviado
        var pagoEntity = await _unitOfWork.Pagos.GetByIdAsync(pagoId);
        if (pagoEntity != null)
        {
            pagoEntity.ComprobanteEnviado = true;
            _unitOfWork.Pagos.Update(pagoEntity);
            await _unitOfWork.SaveChangesAsync();
        }
    }

    public async Task<(bool Exito, string Mensaje)> AnularPagoAsync(int pagoId, string motivo, int usuarioId)
    {
        var pago = await _unitOfWork.Pagos.Query()
            .Include(p => p.Cuota)
                .ThenInclude(c => c.Prestamo)
                    .ThenInclude(p => p.Cliente)
            .FirstOrDefaultAsync(p => p.PagoId == pagoId);

        if (pago == null) return (false, "Pago no encontrado.");
        if (pago.Estado == "Anulado") return (false, "El pago ya está anulado.");

        // Extraer datos antes de modificar la DB para evitar NullReferenceException
        var telefonoCliente = pago.Cuota?.Prestamo?.Cliente?.Telefono;
        var nombreCliente = pago.Cuota?.Prestamo?.Cliente?.NombreCompleto ?? "";
        var refPago = $"PAG-{pago.PagoId:D6}";
        var montoStr = pago.MontoPagado.ToString("N0");
        var fechaPagoStr = pago.FechaPago.ToString("dd/MM/yyyy");
        var aliasCredito = pago.Cuota?.Prestamo?.NombreAlias;
        if (string.IsNullOrWhiteSpace(aliasCredito)) aliasCredito = "Crédito Estándar";

        // Revertir Cuota
        if (pago.Cuota.NumeroCuota == 0)
        {
            // Fue un abono de interes extra. Al anularlo, lo eliminamos por completo
            // para que no salga en el listado y se pueda volver a cobrar sin restricciones.
            var notificaciones = await _unitOfWork.Notificaciones.Query().Where(n => n.CuotaId == pago.Cuota.CuotaId).ToListAsync();
            foreach (var n in notificaciones)
            {
                _unitOfWork.Notificaciones.Remove(n);
            }
            _unitOfWork.Cuotas.Remove(pago.Cuota);
            _unitOfWork.Pagos.Remove(pago);
            try {
                await _unitOfWork.SaveChangesAsync();
            } catch (Exception ex) {
                return (false, "Error interno de BD: " + (ex.InnerException?.Message ?? ex.Message));
            }
        }
        else
        {
            pago.Estado = "Anulado";
            pago.MotivoAnulacion = motivo;
            _unitOfWork.Pagos.Update(pago);

            // Revertir Saldo
            bool eraSoloInteres = pago.Cuota.NumeroCuota == 0 || pago.Cuota.Prestamo.TipoCredito == "Solo Interés";
            if (!eraSoloInteres)
            {
                pago.Cuota.Prestamo.SaldoPendiente += pago.MontoPagado;
            }
            else if (pago.Cuota.Prestamo.TipoCredito == "Solo Interés" && pago.Cuota.NumeroCuota > 0)
            {
                // Borrar la cuota infinita que se autogeneró al hacer este pago
                var cuotaAutogenerada = await _unitOfWork.Cuotas.Query()
                    .Where(c => c.PrestamoId == pago.Cuota.PrestamoId && c.NumeroCuota > pago.Cuota.NumeroCuota && c.Estado == "Pendiente")
                    .FirstOrDefaultAsync();
                
                if (cuotaAutogenerada != null)
                {
                    var notificacionesAutogenerada = await _unitOfWork.Notificaciones.Query().Where(n => n.CuotaId == cuotaAutogenerada.CuotaId).ToListAsync();
                    foreach (var n in notificacionesAutogenerada)
                    {
                        _unitOfWork.Notificaciones.Remove(n);
                    }
                    _unitOfWork.Cuotas.Remove(cuotaAutogenerada);
                }
            }

            if (pago.Cuota.Prestamo.Estado == "Pagado")
            {
                pago.Cuota.Prestamo.Estado = "Activo";
            }
            _unitOfWork.Prestamos.Update(pago.Cuota.Prestamo);

            pago.Cuota.Estado = "Pendiente";
            pago.Cuota.FechaPago = null;
            _unitOfWork.Cuotas.Update(pago.Cuota);
            try {
                await _unitOfWork.SaveChangesAsync();
            } catch (Exception ex) {
                return (false, "Error interno de BD: " + (ex.InnerException?.Message ?? ex.Message));
            }
        }

        // Enviar WhatsApp de Anulación
        if (!string.IsNullOrEmpty(telefonoCliente))
        {
            try 
            {
                var motivoMensaje = string.IsNullOrWhiteSpace(motivo) ? "Anulación general" : motivo;
                await _whatsAppService.EnviarMensajePlantillaAsync(
                    telefonoCliente,
                    "anulacion_de_recibo",
                    "es",
                    new string[] { nombreCliente, refPago, montoStr, fechaPagoStr, motivoMensaje, aliasCredito }
                );
            }
            catch 
            {
                return (true, "Pago anulado exitosamente. (La notificación no se pudo enviar por problemas de conexión).");
            }
        }

        return (true, "Pago anulado exitosamente. Se envió notificación al cliente.");
    }
}
