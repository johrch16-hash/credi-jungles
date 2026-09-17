using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using PrestamosCobros.BLL.Interfaces;

namespace PrestamosCobros.BLL.Services
{
    public class WhatsAppService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguracionWhatsAppService _configService;

        public WhatsAppService(IConfiguracionWhatsAppService configService, HttpClient httpClient)
        {
            _httpClient = httpClient;
            _configService = configService;
        }

        public async Task<bool> EnviarMensajeTexto(string numeroCliente, string mensaje)
        {
            var config = await _configService.ObtenerConfiguracion();
            
            if (config == null || string.IsNullOrEmpty(config.PhoneNumberId) || string.IsNullOrEmpty(config.AccessToken))
            {
                throw new InvalidOperationException("La configuración de WhatsApp Cloud API no ha sido establecida.");
            }

            // Limpiar y formatear el número de teléfono
            var numeroLimpio = new string(numeroCliente.Where(char.IsDigit).ToArray());
            if (numeroLimpio.Length == 8)
            {
                numeroLimpio = "506" + numeroLimpio; // Código de país por defecto (Costa Rica)
            }

            var url = $"https://graph.facebook.com/v18.0/{config.PhoneNumberId}/messages";

            var payload = new
            {
                messaging_product = "whatsapp",
                to = numeroLimpio,
                type = "text",
                text = new { body = mensaje }
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.AccessToken);

            try
            {
                var response = await _httpClient.PostAsync(url, content);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Error de Meta: {errorBody}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"No se pudo enviar el mensaje a {numeroLimpio}. Detalle: {ex.Message}");
            }
        }

        public async Task<bool> EnviarMensajePlantillaAsync(string numeroCliente, string plantillaNombre, string idiomaCodigo, string[] parametros)
        {
            var config = await _configService.ObtenerConfiguracion();
            
            if (config == null || string.IsNullOrEmpty(config.PhoneNumberId) || string.IsNullOrEmpty(config.AccessToken))
            {
                throw new InvalidOperationException("La configuración de WhatsApp Cloud API no ha sido establecida.");
            }

            // Limpiar y formatear el número de teléfono
            var numeroLimpio = new string(numeroCliente.Where(char.IsDigit).ToArray());
            if (numeroLimpio.Length == 8)
            {
                numeroLimpio = "506" + numeroLimpio; // Código de país por defecto (Costa Rica)
            }

            var url = $"https://graph.facebook.com/v18.0/{config.PhoneNumberId}/messages";

            var listParametros = parametros.Select(p => new { type = "text", text = p }).ToArray();

            var payload = new
            {
                messaging_product = "whatsapp",
                to = numeroLimpio,
                type = "template",
                template = new
                {
                    name = plantillaNombre,
                    language = new { code = idiomaCodigo },
                    components = new[]
                    {
                        new
                        {
                            type = "body",
                            parameters = listParametros
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.AccessToken);

            try
            {
                var response = await _httpClient.PostAsync(url, content);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Error de Meta: {errorBody}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"No se pudo enviar la plantilla a {numeroLimpio}. Detalle: {ex.Message}");
            }
        }

        public async Task<string> DescargarArchivoMetaAsync(string mediaId, string webRootPath)
        {
            var config = await _configService.ObtenerConfiguracion();
            if (config == null || string.IsNullOrEmpty(config.AccessToken))
            {
                throw new InvalidOperationException("La configuración de WhatsApp Cloud API no ha sido establecida.");
            }

            // 1. Obtener la URL del archivo
            var getUrl = $"https://graph.facebook.com/v18.0/{mediaId}";
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.AccessToken);

            var response = await _httpClient.GetAsync(getUrl);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new Exception($"Error al obtener metadata del archivo en Meta: {errorBody}");
            }

            var metaJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(metaJson);
            var root = doc.RootElement;
            
            if (!root.TryGetProperty("url", out var urlProp))
            {
                throw new Exception("La respuesta de Meta no contiene una propiedad 'url'.");
            }
            var downloadUrl = urlProp.GetString();
            if (string.IsNullOrEmpty(downloadUrl))
            {
                throw new Exception("La URL de descarga de Meta está vacía.");
            }

            var mimeType = root.TryGetProperty("mime_type", out var mimeProp) ? mimeProp.GetString() : "image/jpeg";

            // 2. Descargar los bytes
            var fileResponse = await _httpClient.GetAsync(downloadUrl);
            if (!fileResponse.IsSuccessStatusCode)
            {
                throw new Exception($"Error al descargar archivo desde lookaside de Meta. Código: {fileResponse.StatusCode}");
            }

            var fileBytes = await fileResponse.Content.ReadAsByteArrayAsync();

            // 3. Guardar localmente
            var extension = mimeType switch
            {
                "image/png" => ".png",
                "image/gif" => ".gif",
                "application/pdf" => ".pdf",
                _ => ".jpg"
            };

            var uploadsFolder = System.IO.Path.Combine(webRootPath, "uploads", "comprobantes");
            if (!System.IO.Directory.Exists(uploadsFolder))
            {
                System.IO.Directory.CreateDirectory(uploadsFolder);
            }

            var fileName = $"{mediaId}{extension}";
            var filePath = System.IO.Path.Combine(uploadsFolder, fileName);
            await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);

            return $"/uploads/comprobantes/{fileName}";
        }

        public async Task<(byte[] Bytes, string MimeType)> GetArchivoBytesAsync(string mediaId)
        {
            var config = await _configService.ObtenerConfiguracion();
            if (config == null || string.IsNullOrEmpty(config.AccessToken))
            {
                throw new InvalidOperationException("La configuración de WhatsApp Cloud API no ha sido establecida.");
            }

            var getUrl = $"https://graph.facebook.com/v18.0/{mediaId}";
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.AccessToken);

            var response = await _httpClient.GetAsync(getUrl);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Error al obtener metadata del archivo: {await response.Content.ReadAsStringAsync()}");
            }

            var metaJson = await response.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(metaJson);
            var root = doc.RootElement;
            
            if (!root.TryGetProperty("url", out var urlProp))
            {
                throw new Exception("Sin URL.");
            }
            
            var downloadUrl = urlProp.GetString();
            var mimeType = root.TryGetProperty("mime_type", out var mimeProp) ? (mimeProp.GetString() ?? "image/jpeg") : "image/jpeg";

            var fileResponse = await _httpClient.GetAsync(downloadUrl);
            if (!fileResponse.IsSuccessStatusCode)
            {
                throw new Exception("Error al descargar archivo desde lookaside de Meta.");
            }

            var fileBytes = await fileResponse.Content.ReadAsByteArrayAsync();
            return (fileBytes, mimeType);
        }
    }
}
