using Microsoft.EntityFrameworkCore;
using PrestamosCobros.BLL.DTOs;
using PrestamosCobros.BLL.Interfaces;
using PrestamosCobros.DAL.Entities;
using PrestamosCobros.DAL.Repositories;
using PrestamosCobros.Infrastructure.Interfaces;

namespace PrestamosCobros.BLL.Services;

public class NotificacionService : INotificacionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly WhatsAppService _whatsAppService;
    private readonly IEmailService _emailService;

    public NotificacionService(IUnitOfWork unitOfWork, WhatsAppService whatsAppService, IEmailService emailService)
    {
        _unitOfWork = unitOfWork;
        _whatsAppService = whatsAppService;
        _emailService = emailService;
    }

    public async Task<List<NotificacionListDto>> ObtenerTodasAsync(string? tipo = null, string? estado = null)
    {
        var query = _unitOfWork.Notificaciones.Query()
            .Include(n => n.Cliente)
            .Include(n => n.Cuota)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(tipo))
            query = query.Where(n => n.Tipo == tipo);

        if (!string.IsNullOrWhiteSpace(estado))
            query = query.Where(n => n.Estado == estado);

        return await query.OrderByDescending(n => n.FechaCreacion)
            .Select(n => new NotificacionListDto
            {
                NotificacionId = n.NotificacionId,
                ClienteNombre = n.Cliente.NombreCompleto,
                ClienteTelefono = n.Cliente.Telefono,
                PrestamoId = n.PrestamoId,
                NumeroCuota = n.Cuota.NumeroCuota,
                Tipo = n.Tipo,
                Mensaje = n.Mensaje,
                Canal = n.Canal,
                Estado = n.Estado,
                FechaCreacion = n.FechaCreacion,
                FechaEnvio = n.FechaEnvio
            })
            .Take(200)
            .ToListAsync();
    }

    public async Task MarcarCuotasAtrasadasAsync()
    {
        var hoy = ObtenerFechaLocal();
        var cuotasVencidas = await _unitOfWork.Cuotas.Query()
            .Include(c => c.Prestamo)
            .Where(c => c.Estado == "Pendiente"
                     && c.FechaVencimiento.Date < hoy
                     && c.Prestamo.Estado == "Activo")
            .ToListAsync();

        foreach (var cuota in cuotasVencidas)
        {
            cuota.Estado = "Atrasado";
            _unitOfWork.Cuotas.Update(cuota);
        }

        // También actualizar NivelAtrasos de los clientes
        var clienteIds = cuotasVencidas.Select(c => c.Prestamo.ClienteId).Distinct().ToList();
        foreach (var clienteId in clienteIds)
        {
            var cliente = await _unitOfWork.Clientes.GetByIdAsync(clienteId);
            if (cliente == null) continue;

            var totalAtrasadas = await _unitOfWork.Cuotas.Query()
                .Include(c => c.Prestamo)
                .Where(c => c.Prestamo.ClienteId == clienteId
                         && c.Estado == "Atrasado"
                         && c.Prestamo.Estado == "Activo")
                .CountAsync();

            cliente.NivelAtrasos = totalAtrasadas;
            if (totalAtrasadas >= 3)
                cliente.Estado = "Bloqueado";

            _unitOfWork.Clientes.Update(cliente);
        }

        if (cuotasVencidas.Count > 0)
            await _unitOfWork.SaveChangesAsync();
    }

    public async Task GenerarRecordatoriosAsync()
    {
        var hoy = ObtenerFechaLocal();
        
        string diasStr = await ObtenerPreferenciaAsync("Notificacion_DiasAnticipacion", "1");
        int.TryParse(diasStr, out int diasAnticipacion);
        if (diasAnticipacion < 0) diasAnticipacion = 0;

        var diaObjetivo = hoy.AddDays(diasAnticipacion);

        var plantilla = await ObtenerPlantillaAsync("PlantillaRecordatorio");

        // 1. Recordatorios: cuotas que vencen en el día de anticipación configurado
        var cuotasProximas = await _unitOfWork.Cuotas.Query()
            .Include(c => c.Prestamo)
                .ThenInclude(p => p.Cliente)
            .Where(c => c.Estado == "Pendiente"
                     && c.FechaVencimiento.Date == diaObjetivo.Date
                     && c.Prestamo.Estado == "Activo")
            .ToListAsync();

        foreach (var cuota in cuotasProximas)
        {
            var yaNotificada = await _unitOfWork.Notificaciones.Query()
                .AnyAsync(n => n.CuotaId == cuota.CuotaId && n.Tipo == "Recordatorio");

            if (yaNotificada) continue;

            var msg = ProcesarPlantilla(plantilla, cuota);

            await _unitOfWork.Notificaciones.AddAsync(new Notificacion
            {
                PrestamoId = cuota.PrestamoId,
                CuotaId = cuota.CuotaId,
                ClienteId = cuota.Prestamo.ClienteId,
                Tipo = "Recordatorio",
                Mensaje = msg
            });
        }

        // 2. Alertas de atraso: cuotas atrasadas sin notificación de atraso
        var cuotasAtrasadas = await _unitOfWork.Cuotas.Query()
            .Include(c => c.Prestamo)
                .ThenInclude(p => p.Cliente)
            .Where(c => c.Estado == "Atrasado"
                     && c.Prestamo.Estado == "Activo")
            .ToListAsync();

        foreach (var cuota in cuotasAtrasadas)
        {
            var yaNotificada = await _unitOfWork.Notificaciones.Query()
                .AnyAsync(n => n.CuotaId == cuota.CuotaId && n.Tipo == "Atraso");

            if (yaNotificada) continue;

            var diasAtraso = (hoy - cuota.FechaVencimiento.Date).Days;
            var msg = $"Aviso de atraso: Su cuota #{cuota.NumeroCuota} de ₡{cuota.Monto:N2} " +
                      $"venció hace {diasAtraso} día(s) ({cuota.FechaVencimiento:dd/MM/yyyy}). " +
                      $"Favor comunicarse para regularizar su situación.";

            await _unitOfWork.Notificaciones.AddAsync(new Notificacion
            {
                PrestamoId = cuota.PrestamoId,
                CuotaId = cuota.CuotaId,
                ClienteId = cuota.Prestamo.ClienteId,
                Tipo = "Atraso",
                Mensaje = msg
            });
        }

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task MarcarComoEnviadaAsync(int notificacionId)
    {
        var notif = await _unitOfWork.Notificaciones.GetByIdAsync(notificacionId);
        if (notif == null || notif.Estado == "Enviado") return;

        notif.Estado = "Enviado";
        notif.FechaEnvio = DateTime.UtcNow;
        _unitOfWork.Notificaciones.Update(notif);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<string> ObtenerLinkWhatsAppAsync(int notificacionId)
    {
        var notif = await _unitOfWork.Notificaciones.Query()
            .Include(n => n.Cliente)
            .FirstOrDefaultAsync(n => n.NotificacionId == notificacionId);

        if (notif == null) return "#";

        var phone = notif.Cliente.Telefono.Replace("-", "").Replace(" ", "").Replace("+", "");
        if (!phone.StartsWith("506") && phone.Length == 8) phone = "506" + phone;

        var text = System.Net.WebUtility.UrlEncode(notif.Mensaje);
        return $"https://wa.me/{phone}?text={text}";
    }

    public async Task<string> ObtenerPlantillaAsync(string clave)
    {
        var pref = await _unitOfWork.Preferencias.Query()
            .FirstOrDefaultAsync(p => p.Clave == clave);
        
        if (pref != null)
        {
            if (pref.Valor == "Hola [Nombre Cliente], ⚡<br><br>Le recordamos que su cuota por ₡[Monto] vence el día [Fecha].<br><br>Puede cancelar vía SINPE al 8888-8888.<br><br>¡Gracias por su puntualidad!" ||
                pref.Valor.Contains("Banco Nacional IBAN: <b>CR001122334455667788</b>"))
            {
                pref.Valor = "Hola <b>[Nombre Cliente]</b>, ⚡<br><br>Le recordamos que la cuota <b>#[Numero Cuota]</b> del crédito <b>[Nombre Credito]</b> por un monto de <b>₡[Monto]</b> vence el <b>[Fecha]</b>.<br><br><b>Métodos de Pago:</b><br>• SINPE Móvil: <b>[SINPE]</b><br>• Cuenta IBAN 1: <b>[Cuenta IBAN 1]</b><br>• Cuenta IBAN 2: <b>[Cuenta IBAN 2]</b><br>• Cuenta IBAN 3: <b>[Cuenta IBAN 3]</b><br><br>Fecha del siguiente pago: <b>[Siguiente Pago]</b>.<br><br>¡Gracias por su puntualidad!";
                _unitOfWork.Preferencias.Update(pref);
                await _unitOfWork.SaveChangesAsync();
            }
            return pref.Valor;
        }

        // Valores por defecto
        if (clave == "PlantillaRecordatorio")
            return "Hola <b>[Nombre Cliente]</b>, ⚡<br><br>Le recordamos que la cuota <b>#[Numero Cuota]</b> del crédito <b>[Nombre Credito]</b> por un monto de <b>₡[Monto]</b> vence el <b>[Fecha]</b>.<br><br><b>Métodos de Pago:</b><br>• SINPE Móvil: <b>[SINPE]</b><br>• Cuenta IBAN 1: <b>[Cuenta IBAN 1]</b><br>• Cuenta IBAN 2: <b>[Cuenta IBAN 2]</b><br>• Cuenta IBAN 3: <b>[Cuenta IBAN 3]</b><br><br>Fecha del siguiente pago: <b>[Siguiente Pago]</b>.<br><br>¡Gracias por su puntualidad!";
        
        return string.Empty;
    }

    public async Task ActualizarPlantillaAsync(string clave, string valor)
    {
        var pref = await _unitOfWork.Preferencias.Query()
            .FirstOrDefaultAsync(p => p.Clave == clave);

        if (pref == null)
        {
            await _unitOfWork.Preferencias.AddAsync(new Preferencias { Clave = clave, Valor = valor });
        }
        else
        {
            pref.Valor = valor;
            _unitOfWork.Preferencias.Update(pref);
        }

        await _unitOfWork.SaveChangesAsync();
    }

    private string ExtraerNumeroSinpe(string plantilla)
    {
        var match = System.Text.RegularExpressions.Regex.Match(plantilla, @"\b\d{4}-?\d{4}\b");
        return match.Success ? match.Value : "8888-8888";
    }

    // Método privado para procesar variables en la plantilla
    private string ProcesarPlantilla(string plantilla, Cuota cuota)
    {
        var nombreCredito = !string.IsNullOrEmpty(cuota.Prestamo.NombreAlias) 
            ? cuota.Prestamo.NombreAlias 
            : $"Crédito #{cuota.PrestamoId:D5}";

        string sinpe = !string.IsNullOrEmpty(cuota.Prestamo.NumeroSinpe)
            ? cuota.Prestamo.NumeroSinpe
            : ExtraerNumeroSinpe(plantilla);

        var siguienteCuota = _unitOfWork.Cuotas.Query()
            .Where(c => c.PrestamoId == cuota.PrestamoId && c.FechaVencimiento > cuota.FechaVencimiento)
            .OrderBy(c => c.FechaVencimiento)
            .FirstOrDefault();
        
        string fechaSiguiente = siguienteCuota != null 
            ? siguienteCuota.FechaVencimiento.ToString("dd/MM/yyyy") 
            : cuota.FechaVencimiento.AddDays(cuota.Prestamo.PeriodicidadDias).ToString("dd/MM/yyyy");

        var cuentas = (cuota.Prestamo.CuentasBancarias ?? "")
            .Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(c => c.Trim())
            .ToList();
        var iban1 = cuentas.Count > 0 ? cuentas[0] : "";
        var iban2 = cuentas.Count > 1 ? cuentas[1] : "";
        var iban3 = cuentas.Count > 2 ? cuentas[2] : "";

        return plantilla
            .Replace("<br>", "\n")
            .Replace("<b>", "*")
            .Replace("</b>", "*")
            .Replace("<strong>", "*")
            .Replace("</strong>", "*")
            .Replace("<span class=\"font-black text-primary-container\" contenteditable=\"false\">", "")
            .Replace("</span>", "")
            .Replace("[Nombre Cliente]", cuota.Prestamo.Cliente.NombreCompleto)
            .Replace("[Monto]", cuota.Monto.ToString("N0"))
            .Replace("[Fecha]", cuota.FechaVencimiento.ToString("dd/MM/yyyy"))
            .Replace("[Nombre Credito]", nombreCredito)
            .Replace("[Numero Cuota]", cuota.NumeroCuota.ToString())
            .Replace("[SINPE]", sinpe)
            .Replace("[Siguiente Pago]", fechaSiguiente)
            .Replace("[Cuenta IBAN 1]", iban1)
            .Replace("[Cuenta IBAN 2]", iban2)
            .Replace("[Cuenta IBAN 3]", iban3);
    }

    public async Task<bool> EnviarRecordatorioManualAsync(int cuotaId, bool esSoloInteres = false)
    {
        var cuota = await _unitOfWork.Cuotas.Query()
            .Include(c => c.Prestamo)
                .ThenInclude(p => p.Cliente)
            .FirstOrDefaultAsync(c => c.CuotaId == cuotaId);

        if (cuota == null) return false;

        var siguienteCuota = await _unitOfWork.Cuotas.Query()
            .Where(c => c.PrestamoId == cuota.PrestamoId && c.FechaVencimiento > cuota.FechaVencimiento)
            .OrderBy(c => c.FechaVencimiento)
            .FirstOrDefaultAsync();

        string fechaSiguiente = siguienteCuota != null 
            ? siguienteCuota.FechaVencimiento.ToString("dd/MM/yyyy") 
            : cuota.FechaVencimiento.AddDays(cuota.Prestamo.PeriodicidadDias).ToString("dd/MM/yyyy");

        var plantilla = await ObtenerPlantillaAsync("PlantillaRecordatorio");
        var mensaje = ProcesarPlantilla(plantilla, cuota);

        var templateName = await ObtenerPreferenciaAsync("Notificacion_WhatsAppTemplateName", "recordatorio_pago");
        if (esSoloInteres || cuota.Prestamo.TipoCredito == "Solo Interés")
        {
            templateName = await ObtenerPreferenciaAsync("Notificacion_RecordatorioSoloInteresTemplateName", "cobro_de_");
        }
        string sinpe = !string.IsNullOrEmpty(cuota.Prestamo.NumeroSinpe)
            ? cuota.Prestamo.NumeroSinpe
            : ExtraerNumeroSinpe(plantilla);

        var cuentas = (cuota.Prestamo.CuentasBancarias ?? "")
            .Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(c => c.Trim())
            .ToList();
        var iban1 = cuentas.Count > 0 ? cuentas[0] : "-";
        var iban2 = cuentas.Count > 1 ? cuentas[1] : "-";
        var iban3 = cuentas.Count > 2 ? cuentas[2] : "-";

        string[] parametros;
        if (templateName == "recordatorio_pago_cuentas")
        {
            parametros = new string[]
            {
                cuota.Prestamo.Cliente.NombreCompleto,
                cuota.NumeroCuota.ToString(),
                !string.IsNullOrEmpty(cuota.Prestamo.NombreAlias) ? cuota.Prestamo.NombreAlias : $"Crédito #{cuota.PrestamoId:D5}",
                cuota.Monto.ToString("N0"),
                cuota.FechaVencimiento.ToString("dd/MM/yyyy"),
                sinpe,
                fechaSiguiente,
                iban1,
                iban2,
                iban3
            };
        }
        else if (templateName == "recordatorio_pago_completo")
        {
            parametros = new string[]
            {
                cuota.Prestamo.Cliente.NombreCompleto,
                cuota.NumeroCuota.ToString(),
                !string.IsNullOrEmpty(cuota.Prestamo.NombreAlias) ? cuota.Prestamo.NombreAlias : $"Crédito #{cuota.PrestamoId:D5}",
                cuota.Monto.ToString("N0"),
                cuota.FechaVencimiento.ToString("dd/MM/yyyy"),
                sinpe,
                fechaSiguiente,
                iban1,
                iban2,
                iban3
            };
        }
        else
        {
            parametros = new string[]
            {
                cuota.Prestamo.Cliente.NombreCompleto,
                cuota.Monto.ToString("N0"),
                cuota.FechaVencimiento.ToString("dd/MM/yyyy"),
                sinpe,
                fechaSiguiente
            };
        }

        bool enviado = await _whatsAppService.EnviarMensajePlantillaAsync(
            cuota.Prestamo.Cliente.Telefono,
            templateName,
            "es",
            parametros
        );

        if (enviado)
        {
            var notificacion = new Notificacion
            {
                PrestamoId = cuota.PrestamoId,
                CuotaId = cuota.CuotaId,
                ClienteId = cuota.Prestamo.ClienteId,
                Tipo = "Manual",
                Mensaje = mensaje,
                Estado = "Enviado",
                FechaEnvio = DateTime.UtcNow
            };
            await _unitOfWork.Notificaciones.AddAsync(notificacion);
            await _unitOfWork.SaveChangesAsync();
        }

        return enviado;
    }

    public async Task<bool> EnviarCorreoManualAsync(int cuotaId)
    {
        var cuota = await _unitOfWork.Cuotas.Query()
            .Include(c => c.Prestamo)
                .ThenInclude(p => p.Cliente)
            .FirstOrDefaultAsync(c => c.CuotaId == cuotaId);

        if (cuota == null) return false;

        var emailDestino = cuota.Prestamo.Cliente.Email;
        if (string.IsNullOrWhiteSpace(emailDestino))
        {
            throw new InvalidOperationException("El cliente no tiene un correo electrónico registrado.");
        }

        var siguienteCuota = await _unitOfWork.Cuotas.Query()
            .Where(c => c.PrestamoId == cuota.PrestamoId && c.FechaVencimiento > cuota.FechaVencimiento)
            .OrderBy(c => c.FechaVencimiento)
            .FirstOrDefaultAsync();

        string fechaSiguiente = siguienteCuota != null 
            ? siguienteCuota.FechaVencimiento.ToString("dd/MM/yyyy") 
            : cuota.FechaVencimiento.AddDays(cuota.Prestamo.PeriodicidadDias).ToString("dd/MM/yyyy");

        var plantilla = await ObtenerPlantillaAsync("PlantillaRecordatorio");
        var cuerpo = ProcesarPlantilla(plantilla, cuota);

        // En correo electrónico, los saltos de línea de la plantilla se conservan como HTML (<br>)
        var cuerpoHtml = cuerpo.Replace("\n", "<br>");

        await _emailService.EnviarCorreoAsync(
            emailDestino,
            "Recordatorio de Pago - CrediGest",
            cuerpoHtml
        );

        var notificacion = new Notificacion
        {
            PrestamoId = cuota.PrestamoId,
            CuotaId = cuota.CuotaId,
            ClienteId = cuota.Prestamo.ClienteId,
            Tipo = "EmailManual",
            Mensaje = cuerpoHtml,
            Estado = "Enviado",
            FechaEnvio = DateTime.UtcNow
        };
        await _unitOfWork.Notificaciones.AddAsync(notificacion);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    public async Task<string> ObtenerPreferenciaAsync(string clave, string valorPorDefecto)
    {
        var pref = await _unitOfWork.Preferencias.Query()
            .FirstOrDefaultAsync(p => p.Clave == clave);
        return pref?.Valor ?? valorPorDefecto;
    }

    public async Task GuardarPreferenciaAsync(string clave, string valor)
    {
        var pref = await _unitOfWork.Preferencias.Query()
            .FirstOrDefaultAsync(p => p.Clave == clave);

        if (pref == null)
        {
            await _unitOfWork.Preferencias.AddAsync(new Preferencias { Clave = clave, Valor = valor });
        }
        else
        {
            pref.Valor = valor;
            _unitOfWork.Preferencias.Update(pref);
        }

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task EnviarRecordatoriosPendientesAsync()
    {
        string waActivo = await ObtenerPreferenciaAsync("Notificacion_WhatsAppActivo", "true");
        if (waActivo != "true") return;

        string horaEnvioStr = await ObtenerPreferenciaAsync("Notificacion_HoraEnvio", "09:00");
        if (!TimeSpan.TryParse(horaEnvioStr, out TimeSpan horaEnvio))
        {
            horaEnvio = new TimeSpan(9, 0, 0);
        }

        TimeZoneInfo tz;
        try { tz = TimeZoneInfo.FindSystemTimeZoneById("Central America Standard Time"); }
        catch { tz = TimeZoneInfo.FindSystemTimeZoneById("America/Costa_Rica"); }
        
        var horaLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);

        if (horaLocal.TimeOfDay < horaEnvio) return;

        string hoyStr = horaLocal.ToString("yyyy-MM-dd");
        // string ultimoEnvio = await ObtenerPreferenciaAsync("Notificacion_UltimoEnvioAutomatico", "");
        // if (ultimoEnvio == hoyStr) return;

        var pendientes = await _unitOfWork.Notificaciones.Query()
            .Include(n => n.Cliente)
            .Include(n => n.Cuota)
                .ThenInclude(c => c.Prestamo)
            .Where(n => n.Estado == "Pendiente")
            .ToListAsync();

        if (pendientes.Count == 0)
        {
            return;
        }

        foreach (var notif in pendientes)
        {
            try
            {
                bool exito = false;
                if (notif.Tipo == "Recordatorio" && notif.Cuota != null && notif.Cuota.Prestamo != null)
                {
                    var cuota = notif.Cuota;
                    var siguienteCuota = await _unitOfWork.Cuotas.Query()
                        .Where(c => c.PrestamoId == cuota.PrestamoId && c.FechaVencimiento > cuota.FechaVencimiento)
                        .OrderBy(c => c.FechaVencimiento)
                        .FirstOrDefaultAsync();

                    string fechaSiguiente = siguienteCuota != null 
                        ? siguienteCuota.FechaVencimiento.ToString("dd/MM/yyyy") 
                        : cuota.FechaVencimiento.AddDays(cuota.Prestamo.PeriodicidadDias).ToString("dd/MM/yyyy");

                    var plantilla = await ObtenerPlantillaAsync("PlantillaRecordatorio");
                    string sinpe = !string.IsNullOrEmpty(cuota.Prestamo.NumeroSinpe)
                        ? cuota.Prestamo.NumeroSinpe
                        : ExtraerNumeroSinpe(plantilla);

                    var templateName = await ObtenerPreferenciaAsync("Notificacion_WhatsAppTemplateName", "recordatorio_pago");
                    if (cuota.Prestamo.TipoCredito == "Solo Interés")
                    {
                        templateName = await ObtenerPreferenciaAsync("Notificacion_RecordatorioSoloInteresTemplateName", "cobro_de_");
                    }

                    var cuentas = (cuota.Prestamo.CuentasBancarias ?? "")
                        .Split('|', StringSplitOptions.RemoveEmptyEntries)
                        .Select(c => c.Trim())
                        .ToList();
                    var iban1 = cuentas.Count > 0 ? cuentas[0] : "-";
                    var iban2 = cuentas.Count > 1 ? cuentas[1] : "-";
                    var iban3 = cuentas.Count > 2 ? cuentas[2] : "-";

                    string[] parametros;
                    if (templateName == "recordatorio_pago_cuentas")
                    {
                        parametros = new string[]
                        {
                            notif.Cliente.NombreCompleto,
                            cuota.NumeroCuota.ToString(),
                            !string.IsNullOrEmpty(cuota.Prestamo.NombreAlias) ? cuota.Prestamo.NombreAlias : $"Crédito #{cuota.PrestamoId:D5}",
                            cuota.Monto.ToString("N0"),
                            cuota.FechaVencimiento.ToString("dd/MM/yyyy"),
                            sinpe,
                            fechaSiguiente,
                            iban1,
                            iban2,
                            iban3
                        };
                    }
                    else if (templateName == "recordatorio_pago_completo")
                    {
                        parametros = new string[]
                        {
                            notif.Cliente.NombreCompleto,
                            cuota.NumeroCuota.ToString(),
                            !string.IsNullOrEmpty(cuota.Prestamo.NombreAlias) ? cuota.Prestamo.NombreAlias : $"Crédito #{cuota.PrestamoId:D5}",
                            cuota.Monto.ToString("N0"),
                            cuota.FechaVencimiento.ToString("dd/MM/yyyy"),
                            sinpe,
                            fechaSiguiente,
                            iban1,
                            iban2,
                            iban3
                        };
                    }
                    else
                    {
                        parametros = new string[]
                        {
                            notif.Cliente.NombreCompleto,
                            cuota.Monto.ToString("N0"),
                            cuota.FechaVencimiento.ToString("dd/MM/yyyy"),
                            sinpe,
                            fechaSiguiente
                        };
                    }

                    exito = await _whatsAppService.EnviarMensajePlantillaAsync(
                        notif.Cliente.Telefono,
                        templateName,
                        "es",
                        parametros
                    );
                }
                else
                {
                    exito = await _whatsAppService.EnviarMensajeTexto(notif.Cliente.Telefono, notif.Mensaje);
                }

                if (exito)
                {
                    notif.Estado = "Enviado";
                    notif.FechaEnvio = DateTime.UtcNow;
                    notif.Canal = "WhatsApp";

                    // Enviar correo automático de respaldo si tiene email
                    if (!string.IsNullOrWhiteSpace(notif.Cliente.Email))
                    {
                        try
                        {
                            var cuerpoHtml = notif.Mensaje.Replace("\n", "<br>");
                            await _emailService.EnviarCorreoAsync(
                                notif.Cliente.Email,
                                "Recordatorio de Pago - CrediGest",
                                cuerpoHtml
                            );

                            await _unitOfWork.Notificaciones.AddAsync(new Notificacion
                            {
                                PrestamoId = notif.PrestamoId,
                                CuotaId = notif.CuotaId,
                                ClienteId = notif.ClienteId,
                                Tipo = notif.Tipo == "Recordatorio" ? "EmailAutomatico" : "EmailAtrasoAutomatico",
                                Mensaje = cuerpoHtml,
                                Estado = "Enviado",
                                FechaEnvio = DateTime.UtcNow,
                                Canal = "Email"
                            });
                        }
                        catch (Exception emailEx)
                        {
                            Console.WriteLine($"[Email Auto Error] {emailEx.Message}");
                        }
                    }
                }
                else
                {
                    notif.Estado = "Fallo";
                }
            }
            catch (Exception ex)
            {
                notif.Estado = "Fallo";
                Console.WriteLine($"[Notificaciones Auto] Error: {ex.Message}");
            }

            _unitOfWork.Notificaciones.Update(notif);
        }

        await GuardarPreferenciaAsync("Notificacion_UltimoEnvioAutomatico", hoyStr);
        await _unitOfWork.SaveChangesAsync();
    }

    private DateTime ObtenerFechaLocal()
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Central America Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).Date;
        }
        catch
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById("America/Costa_Rica");
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).Date;
            }
            catch
            {
                return DateTime.Now.Date;
            }
        }
    }

    public async Task EliminarAsync(int notificacionId)
    {
        var notificacion = await _unitOfWork.Notificaciones.GetByIdAsync(notificacionId);
        if (notificacion != null)
        {
            _unitOfWork.Notificaciones.Remove(notificacion);
            await _unitOfWork.SaveChangesAsync();
        }
    }
}
