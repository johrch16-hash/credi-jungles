using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrestamosCobros.BLL.Interfaces;
using PrestamosCobros.BLL.Services;
using PrestamosCobros.DAL.Repositories;

namespace PrestamosCobros.Web.Controllers
{
    [Authorize(Roles = "Master")]
    [Route("api/configuracion/whatsapp")]
    [ApiController]
    public class ConfiguracionWhatsAppController : ControllerBase
    {
        private readonly IConfiguracionWhatsAppService _configService;
        private readonly WhatsAppService _whatsAppService;
        private readonly IUnitOfWork _unitOfWork;

        public ConfiguracionWhatsAppController(IConfiguracionWhatsAppService configService, WhatsAppService whatsAppService, IUnitOfWork unitOfWork)
        {
            _configService = configService;
            _whatsAppService = whatsAppService;
            _unitOfWork = unitOfWork;
        }

        [HttpGet("obtener")]
        public async Task<IActionResult> Obtener()
        {
            var config = await _configService.ObtenerConfiguracion();
            if (config == null) return NotFound();

            var adminPhone = (await _unitOfWork.Preferencias.Query()
                .FirstOrDefaultAsync(p => p.Clave == "Notificacion_AdminWhatsApp"))?.Valor ?? "";

            return Ok(new
            {
                phoneNumberId = config.PhoneNumberId,
                accessToken = config.AccessToken.Length > 8 
                    ? string.Concat(config.AccessToken.AsSpan(0, 4), "...", config.AccessToken[^4..]) 
                    : "****",
                adminPhone = adminPhone,
                fechaActualizacion = config.FechaActualizacion
            });
        }

        [HttpPost("guardar")]
        public async Task<IActionResult> Guardar([FromBody] WhatsAppConfigRequest request)
        {
            if (string.IsNullOrEmpty(request.PhoneNumberId))
            {
                return BadRequest("El Phone Number ID es obligatorio.");
            }

            var configExistente = await _configService.ObtenerConfiguracion();
            string tokenParaGuardar = request.AccessToken;

            if (string.IsNullOrEmpty(tokenParaGuardar) || tokenParaGuardar.Contains("...") || tokenParaGuardar == "****")
            {
                if (configExistente != null)
                {
                    tokenParaGuardar = configExistente.AccessToken;
                }
                else
                {
                    return BadRequest("El Access Token es obligatorio para la primera configuración.");
                }
            }

            await _configService.GuardarConfiguracion(request.PhoneNumberId, tokenParaGuardar);

            // Guardar Admin Phone en Preferencias
            var pref = await _unitOfWork.Preferencias.Query()
                .FirstOrDefaultAsync(p => p.Clave == "Notificacion_AdminWhatsApp");

            if (pref == null)
            {
                await _unitOfWork.Preferencias.AddAsync(new DAL.Entities.Preferencias 
                { 
                    Clave = "Notificacion_AdminWhatsApp", 
                    Valor = request.AdminPhone ?? "" 
                });
            }
            else
            {
                pref.Valor = request.AdminPhone ?? "";
                _unitOfWork.Preferencias.Update(pref);
            }

            // Guardar WabaId en Preferencias
            var prefWaba = await _unitOfWork.Preferencias.Query()
                .FirstOrDefaultAsync(p => p.Clave == "Notificacion_WhatsAppWabaId");

            if (prefWaba == null)
            {
                await _unitOfWork.Preferencias.AddAsync(new DAL.Entities.Preferencias 
                { 
                    Clave = "Notificacion_WhatsAppWabaId", 
                    Valor = request.WabaId ?? "" 
                });
            }
            else
            {
                prefWaba.Valor = request.WabaId ?? "";
                _unitOfWork.Preferencias.Update(prefWaba);
            }

            await _unitOfWork.SaveChangesAsync();

            // Suscribir la WABA a la aplicación en Meta automáticamente
            try
            {
                if (!string.IsNullOrEmpty(request.WabaId))
                {
                    using var client = new HttpClient();
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenParaGuardar);

                    var subscribeUrl = $"https://graph.facebook.com/v18.0/{request.WabaId}/subscribed_apps";
                    var subscribeResponse = await client.PostAsync(subscribeUrl, new StringContent("", Encoding.UTF8, "application/json"));
                    if (subscribeResponse.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"[WhatsApp Webhook Auto-Subscribe] Éxito al suscribir WABA: {request.WabaId}");
                    }
                    else
                    {
                        var subErr = await subscribeResponse.Content.ReadAsStringAsync();
                        Console.WriteLine($"[WhatsApp Webhook Auto-Subscribe Error] Al suscribir: {subErr}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WhatsApp Webhook Auto-Subscribe Exception] {ex.Message}");
            }

            return Ok(new { mensaje = "Configuración guardada exitosamente." });
        }

        [Authorize(Roles = "Master")]
        [HttpGet("debug")]
        public async Task<IActionResult> Debug()
        {
            try
            {
                var config = await _configService.ObtenerConfiguracion();
                if (config == null) return BadRequest(new { error = "No hay configuración guardada." });

                var adminPhone = (await _unitOfWork.Preferencias.Query()
                    .FirstOrDefaultAsync(p => p.Clave == "Notificacion_AdminWhatsApp"))?.Valor ?? "";

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.AccessToken);

                var phoneBasicUrl = $"https://graph.facebook.com/v18.0/{config.PhoneNumberId}?fields=id,display_phone_number,verified_name";
                var phoneBasicResponse = await client.GetAsync(phoneBasicUrl);
                var phoneBasicJson = await phoneBasicResponse.Content.ReadAsStringAsync();

                var phoneDetailsUrl = $"https://graph.facebook.com/v18.0/{config.PhoneNumberId}?fields=id,display_phone_number,verified_name,whatsapp_business_account";
                var phoneResponse = await client.GetAsync(phoneDetailsUrl);
                var phoneJson = await phoneResponse.Content.ReadAsStringAsync();
                
                string wabaId = "";
                string wabaDetails = "";
                string subscribedApps = "";

                if (phoneResponse.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(phoneJson);
                    if (doc.RootElement.TryGetProperty("whatsapp_business_account", out var wabaProp) &&
                        wabaProp.TryGetProperty("id", out var wabaIdProp))
                    {
                        wabaId = wabaIdProp.GetString() ?? "";
                        if (!string.IsNullOrEmpty(wabaId))
                        {
                            var wabaDetailsUrl = $"https://graph.facebook.com/v18.0/{wabaId}";
                            var wabaRes = await client.GetAsync(wabaDetailsUrl);
                            wabaDetails = await wabaRes.Content.ReadAsStringAsync();

                            var subscribeUrl = $"https://graph.facebook.com/v18.0/{wabaId}/subscribed_apps";
                            var subRes = await client.GetAsync(subscribeUrl);
                            subscribedApps = await subRes.Content.ReadAsStringAsync();
                        }
                    }
                }

                object? phoneBasicParsed = null;
                try { phoneBasicParsed = JsonDocument.Parse(phoneBasicJson).RootElement; } catch { phoneBasicParsed = phoneBasicJson; }

                object? phoneParsed = null;
                try { phoneParsed = JsonDocument.Parse(phoneJson).RootElement; } catch { phoneParsed = phoneJson; }

                object? wabaParsed = null;
                if (!string.IsNullOrEmpty(wabaDetails))
                {
                    try { wabaParsed = JsonDocument.Parse(wabaDetails).RootElement; } catch { wabaParsed = wabaDetails; }
                }

                object? subParsed = null;
                if (!string.IsNullOrEmpty(subscribedApps))
                {
                    try { subParsed = JsonDocument.Parse(subscribedApps).RootElement; } catch { subParsed = subscribedApps; }
                }

                return Ok(new
                {
                    tokenLength = config.AccessToken?.Length ?? 0,
                    phoneNumberId = config.PhoneNumberId,
                    adminPhone = adminPhone,
                    phoneBasicStatus = phoneBasicResponse.StatusCode.ToString(),
                    phoneBasicResponse = phoneBasicParsed,
                    phoneApiStatus = phoneResponse.StatusCode.ToString(),
                    phoneResponse = phoneParsed,
                    wabaId = wabaId,
                    wabaResponse = wabaParsed,
                    subscribedApps = subParsed
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [Authorize(Roles = "Master")]
        [HttpPost("test-mensaje")]
        public async Task<IActionResult> TestMensaje([FromBody] TestMessageRequest request)
        {
            if (string.IsNullOrEmpty(request.Telefono))
            {
                return BadRequest("El número de teléfono es obligatorio.");
            }

            bool enviado = await _whatsAppService.EnviarMensajeTexto(request.Telefono, "🚀 Prueba de CrediGest: ¡Tu conexión con WhatsApp Cloud API funciona correctamente!");
            
            if (enviado)
            {
                return Ok(new { mensaje = "Mensaje de prueba enviado exitosamente." });
            }
            else
            {
                return BadRequest("Error al enviar el mensaje. Verifica la configuración y que el número destino esté registrado en Meta (si usas modo prueba).");
            }
        }
    }

    public class WhatsAppConfigRequest
    {
        public string PhoneNumberId { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public string AdminPhone { get; set; } = string.Empty;
        public string WabaId { get; set; } = string.Empty;
    }

    public class TestMessageRequest
    {
        public string Telefono { get; set; } = string.Empty;
    }
}
