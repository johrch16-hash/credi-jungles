using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrestamosCobros.DAL.Entities;
using PrestamosCobros.DAL.Repositories;
using PrestamosCobros.Infrastructure.Interfaces;

namespace PrestamosCobros.Web.Controllers
{
    [Authorize(Roles = "Master")]
    [Route("api/configuracion/email")]
    [ApiController]
    public class ConfiguracionEmailController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailService _emailService;

        public ConfiguracionEmailController(IUnitOfWork unitOfWork, IEmailService emailService)
        {
            _unitOfWork = unitOfWork;
            _emailService = emailService;
        }

        [HttpGet("obtener")]
        public async Task<IActionResult> Obtener()
        {
            var host = (await _unitOfWork.Preferencias.Query().FirstOrDefaultAsync(p => p.Clave == "SmtpHost"))?.Valor ?? "";
            var port = (await _unitOfWork.Preferencias.Query().FirstOrDefaultAsync(p => p.Clave == "SmtpPort"))?.Valor ?? "";
            var user = (await _unitOfWork.Preferencias.Query().FirstOrDefaultAsync(p => p.Clave == "SmtpUser"))?.Valor ?? "";
            var pass = (await _unitOfWork.Preferencias.Query().FirstOrDefaultAsync(p => p.Clave == "SmtpPassword"))?.Valor ?? "";

            string maskedPass = "";
            if (!string.IsNullOrEmpty(pass))
            {
                maskedPass = pass.Length > 6
                    ? string.Concat(pass.AsSpan(0, 2), "...", pass.AsSpan()[^2..])
                    : "****";
            }

            return Ok(new
            {
                smtpHost = host,
                smtpPort = port,
                smtpUser = user,
                smtpPassword = maskedPass
            });
        }

        [HttpPost("guardar")]
        public async Task<IActionResult> Guardar([FromBody] EmailConfigRequest request)
        {
            if (string.IsNullOrEmpty(request.SmtpHost) || string.IsNullOrEmpty(request.SmtpPort) || string.IsNullOrEmpty(request.SmtpUser))
            {
                return BadRequest("El Servidor, Puerto y Usuario son obligatorios.");
            }

            await UpsertPreferencia("SmtpHost", request.SmtpHost);
            await UpsertPreferencia("SmtpPort", request.SmtpPort);
            await UpsertPreferencia("SmtpUser", request.SmtpUser);

            // Solo actualizar el password si se provee y no es el valor enmascarado
            if (!string.IsNullOrEmpty(request.SmtpPassword) && !request.SmtpPassword.Contains("...") && request.SmtpPassword != "****")
            {
                await UpsertPreferencia("SmtpPassword", request.SmtpPassword);
            }

            await _unitOfWork.SaveChangesAsync();
            return Ok(new { mensaje = "Configuración de correo guardada exitosamente." });
        }

        [HttpPost("test-correo")]
        public async Task<IActionResult> TestCorreo([FromBody] TestEmailRequest request)
        {
            if (string.IsNullOrEmpty(request.Destinatario))
            {
                return BadRequest("El correo destino es obligatorio.");
            }

            try
            {
                await _emailService.EnviarCorreoAsync(
                    request.Destinatario,
                    "🚀 Prueba de Conexión SMTP - CrediGest",
                    $"<h3>¡Conexión Exitosa!</h3><p>Este es un correo de prueba enviado desde <strong>CrediGest</strong> para verificar la configuración del servidor SMTP.</p><p>Fecha y Hora: {DateTime.Now:dd/MM/yyyy HH:mm:ss}</p>");

                return Ok(new { mensaje = "Correo de prueba enviado exitosamente." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = $"Error al enviar correo: {ex.Message}" });
            }
        }

        private async Task UpsertPreferencia(string clave, string valor)
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
        }
    }

    public class EmailConfigRequest
    {
        public string SmtpHost { get; set; } = string.Empty;
        public string SmtpPort { get; set; } = string.Empty;
        public string SmtpUser { get; set; } = string.Empty;
        public string SmtpPassword { get; set; } = string.Empty;
    }

    public class TestEmailRequest
    {
        public string Destinatario { get; set; } = string.Empty;
    }
}
