using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrestamosCobros.BLL.Services;
using PrestamosCobros.DAL.Entities;
using PrestamosCobros.DAL.Repositories;

namespace PrestamosCobros.Web.Controllers
{
    [AllowAnonymous]
    [Route("api/whatsapp/webhook")]
    [ApiController]
    public class WhatsAppWebhookController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly WhatsAppService _whatsAppService;
        private readonly IWebHostEnvironment _env;

        public WhatsAppWebhookController(IUnitOfWork unitOfWork, WhatsAppService whatsAppService, IWebHostEnvironment env)
        {
            _unitOfWork = unitOfWork;
            _whatsAppService = whatsAppService;
            _env = env;
        }

        // GET: api/whatsapp/webhook (Para verificación de Meta)
        [HttpGet]
        public async Task<IActionResult> VerifyWebhook(
            [FromQuery(Name = "hub.mode")] string mode,
            [FromQuery(Name = "hub.challenge")] string challenge,
            [FromQuery(Name = "hub.verify_token")] string verifyToken)
        {
            var expectedToken = (await _unitOfWork.Preferencias.Query()
                .FirstOrDefaultAsync(p => p.Clave == "Notificacion_WhatsAppWebhookVerifyToken"))?.Valor
                ?? "cred_token_secret";

            if (mode == "subscribe" && verifyToken == expectedToken)
            {
                return Content(challenge, "text/plain");
            }

            return Forbid();
        }

        // POST: api/whatsapp/webhook (Para recibir mensajes)
        [HttpPost]
        public async Task<IActionResult> ReceiveMessage()
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                var body = await reader.ReadToEndAsync();

                Console.WriteLine($"[Webhook POST] Payload recibido: {body}");

                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                // Validar estructura de Meta Webhook
                if (root.TryGetProperty("entry", out var entryArray) && entryArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var entry in entryArray.EnumerateArray())
                    {
                        if (entry.TryGetProperty("changes", out var changesArray) && changesArray.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var change in changesArray.EnumerateArray())
                            {
                                if (change.TryGetProperty("value", out var valueProp))
                                {
                                    if (valueProp.TryGetProperty("messages", out var messagesArray) && messagesArray.ValueKind == JsonValueKind.Array)
                                    {
                                        foreach (var message in messagesArray.EnumerateArray())
                                        {
                                            // 1. Obtener remitente
                                            var from = message.TryGetProperty("from", out var fromProp) ? (fromProp.GetString() ?? "") : "";
                                            // Buscar cliente
                                            var digitosRemitente = new string(from.Where(char.IsDigit).ToArray());
                                            if (digitosRemitente.Length > 8) digitosRemitente = digitosRemitente[^8..];

                                            var todosClientes = await _unitOfWork.Clientes.Query().Where(c => c.Activo).ToListAsync();
                                            var cliente = !string.IsNullOrEmpty(digitosRemitente)
                                                ? todosClientes.FirstOrDefault(c => 
                                                {
                                                    if (string.IsNullOrEmpty(c.Telefono)) return false;
                                                    var cPhone = new string(c.Telefono.Where(char.IsDigit).ToArray());
                                                    return cPhone.Contains(digitosRemitente);
                                                })
                                                : null;

                                            // 2. Determinar tipo de mensaje
                                            var type = message.TryGetProperty("type", out var typeProp) ? (typeProp.GetString() ?? "text") : "text";
                                            var content = "";
                                            var mediaId = "";

                                            if (type == "text" && message.TryGetProperty("text", out var textProp) && textProp.TryGetProperty("body", out var bodyProp))
                                            {
                                                content = bodyProp.GetString() ?? "";
                                            }
                                            else if (type == "image" && message.TryGetProperty("image", out var imageProp) && imageProp.TryGetProperty("id", out var idProp))
                                            {
                                                mediaId = idProp.GetString() ?? "";
                                                content = "[Imagen Comprobante]";
                                            }
                                            else if (type == "document" && message.TryGetProperty("document", out var docProp) && docProp.TryGetProperty("id", out var docIdProp))
                                            {
                                                mediaId = docIdProp.GetString() ?? "";
                                                content = docProp.TryGetProperty("filename", out var fn) ? fn.GetString() : "comprobante.pdf";
                                            }
                                            else
                                            {
                                                // Ignorar otros tipos (audio, stickers, etc)
                                                continue;
                                            }

                                            // 3. Ya no descargamos el archivo aquí, solo guardamos el mediaId
                                            if (!string.IsNullOrEmpty(mediaId))
                                            {
                                                content = $"[Media ID: {mediaId}]";
                                            }

                                            // 4. Registrar en base de datos
                                            var mensajeRecibido = new MensajeRecibido
                                            {
                                                ClienteId = cliente?.ClienteId,
                                                TelefonoRemitente = from,
                                                TipoMensaje = type,
                                                Contenido = content,
                                                MetaMediaId = mediaId,
                                                FechaRecibido = DateTime.UtcNow,
                                                Leido = false
                                            };

                                            await _unitOfWork.MensajesRecibidos.AddAsync(mensajeRecibido);
                                            await _unitOfWork.SaveChangesAsync();

                                            // 5. Reenvío al administrador si está configurado
                                            var adminPhone = (await _unitOfWork.Preferencias.Query()
                                                .FirstOrDefaultAsync(p => p.Clave == "Notificacion_AdminWhatsApp"))?.Valor;

                                            if (!string.IsNullOrEmpty(adminPhone))
                                            {
                                                try
                                                {
                                                    var adminMsg = $"📩 *Nuevo mensaje recibido*\n" +
                                                                   $"*Cliente*: {cliente?.NombreCompleto ?? "Desconocido"}\n" +
                                                                   $"*Teléfono*: {from}\n" +
                                                                   $"*Tipo*: {type}\n";

                                                    if (type == "text")
                                                    {
                                                        adminMsg += $"*Mensaje*: {content}";
                                                    }
                                                    else
                                                    {
                                                        var appUrl = (await _unitOfWork.Preferencias.Query()
                                                            .FirstOrDefaultAsync(p => p.Clave == "Notificacion_AppUrl"))?.Valor
                                                            ?? "https://credigestion.onrender.com";

                                                        adminMsg += $"*Comprobante*: Enviado un archivo ({type}).\nVer en panel: {appUrl}/MensajesRecibidos";
                                                    }

                                                    await _whatsAppService.EnviarMensajeTexto(adminPhone, adminMsg);
                                                }
                                                catch (Exception ex)
                                                {
                                                    Console.WriteLine($"[Webhook Reenvio Error] {ex.Message}");
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                return Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Webhook Error] {ex.Message}");
                return BadRequest(ex.Message);
            }
        }
    }
}
