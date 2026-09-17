using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrestamosCobros.BLL.Interfaces;

namespace PrestamosCobros.Web.Controllers;

[Authorize(Roles = "Admin,Master")]
public class NotificacionesController : Controller
{
    private readonly INotificacionService _notificacionService;

    public NotificacionesController(INotificacionService notificacionService)
    {
        _notificacionService = notificacionService;
    }

    public async Task<IActionResult> Index(string? tipo, string? estado)
    {
        var notificaciones = await _notificacionService.ObtenerTodasAsync(tipo, estado);
        ViewData["Tipo"] = tipo;
        ViewData["Estado"] = estado;
        return View(notificaciones);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarcarEnviada(int id)
    {
        await _notificacionService.MarcarComoEnviadaAsync(id);
        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> EnviarWhatsApp(int id)
    {
        var link = await _notificacionService.ObtenerLinkWhatsAppAsync(id);
        await _notificacionService.MarcarComoEnviadaAsync(id);
        return Redirect(link);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerarAhora()
    {
        await _notificacionService.MarcarCuotasAtrasadasAsync();
        await _notificacionService.GenerarRecordatoriosAsync();
        TempData["Mensaje"] = "Notificaciones generadas exitosamente.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnviarAutomaticosAhora()
    {
        // Limpiar el bloqueo de envío diario para pruebas inmediatas
        await _notificacionService.GuardarPreferenciaAsync("Notificacion_UltimoEnvioAutomatico", "reset");

        // Ejecutar el envío automático
        await _notificacionService.EnviarRecordatoriosPendientesAsync();

        TempData["Mensaje"] = "Envío automático de notificaciones ejecutado correctamente.";
        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> Configuracion()
    {
        ViewData["Title"] = "Configuración";
        var plantilla = await _notificacionService.ObtenerPlantillaAsync("PlantillaRecordatorio");
        
        ViewData["DiasAnticipacion"] = await _notificacionService.ObtenerPreferenciaAsync("Notificacion_DiasAnticipacion", "1");
        ViewData["HoraEnvio"] = await _notificacionService.ObtenerPreferenciaAsync("Notificacion_HoraEnvio", "09:00");
        ViewData["WhatsAppActivo"] = await _notificacionService.ObtenerPreferenciaAsync("Notificacion_WhatsAppActivo", "true");
        ViewData["EmailActivo"] = await _notificacionService.ObtenerPreferenciaAsync("Notificacion_EmailActivo", "true");
        ViewData["ComprobanteTextoAdicional"] = await _notificacionService.ObtenerPreferenciaAsync("Comprobante_TextoAdicional", "");
        ViewData["WhatsAppTemplateName"] = await _notificacionService.ObtenerPreferenciaAsync("Notificacion_WhatsAppTemplateName", "recordatorio_pago");
        ViewData["ComprobanteTemplateName"] = await _notificacionService.ObtenerPreferenciaAsync("Notificacion_ComprobanteTemplateName", "comprobante_pago");
        
        ViewData["DefaultSinpe"] = await _notificacionService.ObtenerPreferenciaAsync("Default_Sinpe", "");
        ViewData["DefaultIban1"] = await _notificacionService.ObtenerPreferenciaAsync("Default_Iban1", "");
        ViewData["DefaultIban2"] = await _notificacionService.ObtenerPreferenciaAsync("Default_Iban2", "");
        ViewData["DefaultIban3"] = await _notificacionService.ObtenerPreferenciaAsync("Default_Iban3", "");

        ViewData["ProyeccionTemplateName"] = await _notificacionService.ObtenerPreferenciaAsync("Notificacion_ProyeccionTemplateName", "proyeccion_credito_v1");
        ViewData["RenovacionTemplateName"] = await _notificacionService.ObtenerPreferenciaAsync("Notificacion_RenovacionTemplateName", "renovacion_de_credito");
        ViewData["ComprobanteInteresTemplateName"] = await _notificacionService.ObtenerPreferenciaAsync("Notificacion_ComprobanteInteresTemplateName", "comprobante_pago_interes");
        ViewData["ComprobanteSoloInteresTemplateName"] = await _notificacionService.ObtenerPreferenciaAsync("Notificacion_ComprobanteSoloInteresTemplateName", "comprobante_de_solo_interes");
        ViewData["CreacionClienteTemplateName"] = await _notificacionService.ObtenerPreferenciaAsync("Notificacion_CreacionClienteTemplateName", "creacion_de_cliente");
        ViewData["AnulacionReciboTemplateName"] = await _notificacionService.ObtenerPreferenciaAsync("Notificacion_AnulacionReciboTemplateName", "anulacion_de_recibo");
        ViewData["RecordatorioSoloInteresTemplateName"] = await _notificacionService.ObtenerPreferenciaAsync("Notificacion_RecordatorioSoloInteresTemplateName", "cobro_de_");
        ViewData["MensajeTextoTemplateName"] = await _notificacionService.ObtenerPreferenciaAsync("Notificacion_MensajeTextoTemplateName", "mensaje_texto");

        return View("Configuracion", plantilla);
    }

    [HttpPost]
    [Authorize(Roles = "Master")]
    public async Task<IActionResult> GuardarPlantillasAdicionales([FromForm] string proyeccionTemplateName, [FromForm] string renovacionTemplateName, [FromForm] string comprobanteInteresTemplateName, [FromForm] string comprobanteSoloInteresTemplateName, [FromForm] string creacionClienteTemplateName, [FromForm] string anulacionReciboTemplateName, [FromForm] string recordatorioSoloInteresTemplateName, [FromForm] string mensajeTextoTemplateName)
    {
        try
        {
            await _notificacionService.GuardarPreferenciaAsync("Notificacion_ProyeccionTemplateName", proyeccionTemplateName ?? "proyeccion_credito_v1");
            await _notificacionService.GuardarPreferenciaAsync("Notificacion_RenovacionTemplateName", renovacionTemplateName ?? "renovacion_de_credito");
            await _notificacionService.GuardarPreferenciaAsync("Notificacion_ComprobanteInteresTemplateName", comprobanteInteresTemplateName ?? "comprobante_pago_interes");
            await _notificacionService.GuardarPreferenciaAsync("Notificacion_ComprobanteSoloInteresTemplateName", comprobanteSoloInteresTemplateName ?? "comprobante_de_solo_interes");
            await _notificacionService.GuardarPreferenciaAsync("Notificacion_CreacionClienteTemplateName", creacionClienteTemplateName ?? "creacion_de_cliente");
            await _notificacionService.GuardarPreferenciaAsync("Notificacion_AnulacionReciboTemplateName", anulacionReciboTemplateName ?? "anulacion_de_recibo");
            await _notificacionService.GuardarPreferenciaAsync("Notificacion_RecordatorioSoloInteresTemplateName", recordatorioSoloInteresTemplateName ?? "cobro_de_");
            await _notificacionService.GuardarPreferenciaAsync("Notificacion_MensajeTextoTemplateName", mensajeTextoTemplateName ?? "mensaje_texto");
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> GuardarConfigRecibo([FromForm] string textoAdicional, [FromForm] string whatsAppComprobanteTemplateName)
    {
        try
        {
            await _notificacionService.GuardarPreferenciaAsync("Comprobante_TextoAdicional", textoAdicional ?? "");
            await _notificacionService.GuardarPreferenciaAsync("Notificacion_ComprobanteTemplateName", whatsAppComprobanteTemplateName ?? "comprobante_pago");
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> GuardarConfigCuentasDefault([FromForm] string defaultSinpe, [FromForm] string defaultIban1, [FromForm] string defaultIban2, [FromForm] string defaultIban3)
    {
        try
        {
            await _notificacionService.GuardarPreferenciaAsync("Default_Sinpe", defaultSinpe ?? "");
            await _notificacionService.GuardarPreferenciaAsync("Default_Iban1", defaultIban1 ?? "");
            await _notificacionService.GuardarPreferenciaAsync("Default_Iban2", defaultIban2 ?? "");
            await _notificacionService.GuardarPreferenciaAsync("Default_Iban3", defaultIban3 ?? "");
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> GuardarConfigAutomatizacion([FromForm] string diasAnticipacion, [FromForm] string horaEnvio, [FromForm] string whatsAppActivo, [FromForm] string emailActivo, [FromForm] string whatsAppTemplateName)
    {
        try
        {
            await _notificacionService.GuardarPreferenciaAsync("Notificacion_DiasAnticipacion", diasAnticipacion);
            await _notificacionService.GuardarPreferenciaAsync("Notificacion_HoraEnvio", horaEnvio);
            await _notificacionService.GuardarPreferenciaAsync("Notificacion_WhatsAppActivo", whatsAppActivo);
            await _notificacionService.GuardarPreferenciaAsync("Notificacion_EmailActivo", emailActivo);
            await _notificacionService.GuardarPreferenciaAsync("Notificacion_WhatsAppTemplateName", whatsAppTemplateName ?? "recordatorio_pago");
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> ActualizarPlantilla([FromForm] string valor)
    {
        try
        {
            var sanitizado = valor.Replace("<script>", "").Replace("</script>", "").Replace("onmouseover", "").Replace("onerror", "").Replace("onload", "");
            await _notificacionService.ActualizarPlantillaAsync("PlantillaRecordatorio", sanitizado);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Asistente")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnviarManual([FromForm] int cuotaId)
    {
        try
        {
            var exito = await _notificacionService.EnviarRecordatorioManualAsync(cuotaId);
            if (exito)
                return Ok(new { success = true, message = "Recordatorio enviado exitosamente vía WhatsApp Cloud API." });
            else
                return BadRequest(new { success = false, message = "No se pudo enviar el mensaje. Revisa la configuración de WhatsApp." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Asistente")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnviarCorreoManual([FromForm] int cuotaId)
    {
        try
        {
            var exito = await _notificacionService.EnviarCorreoManualAsync(cuotaId);
            if (exito)
                return Ok(new { success = true, message = "Recordatorio enviado exitosamente vía Correo Electrónico." });
            else
                return BadRequest(new { success = false, message = "No se pudo enviar el correo." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Roles = "Master")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(int id)
    {
        await _notificacionService.EliminarAsync(id);
        TempData["Mensaje"] = "Alerta eliminada permanentemente.";
        return RedirectToAction("Index");
    }
}
