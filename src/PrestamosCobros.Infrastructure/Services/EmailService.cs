using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MimeKit;
using PrestamosCobros.DAL.Repositories;
using PrestamosCobros.Infrastructure.Interfaces;

namespace PrestamosCobros.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly IUnitOfWork _unitOfWork;

    public EmailService(IConfiguration configuration, IUnitOfWork unitOfWork)
    {
        _configuration = configuration;
        _unitOfWork = unitOfWork;
    }

    public async Task EnviarCorreoAsync(string destinatario, string asunto, string cuerpo, byte[]? adjuntoPdf = null, string nombreAdjunto = "Comprobante.pdf")
    {
        // Buscar configuración SMTP en base de datos primero (tabla Preferencias)
        var dbHost = (await _unitOfWork.Preferencias.Query().FirstOrDefaultAsync(p => p.Clave == "SmtpHost"))?.Valor;
        var dbPortStr = (await _unitOfWork.Preferencias.Query().FirstOrDefaultAsync(p => p.Clave == "SmtpPort"))?.Valor;
        var dbUser = (await _unitOfWork.Preferencias.Query().FirstOrDefaultAsync(p => p.Clave == "SmtpUser"))?.Valor;
        var dbPassword = (await _unitOfWork.Preferencias.Query().FirstOrDefaultAsync(p => p.Clave == "SmtpPassword"))?.Valor;

        var host = !string.IsNullOrEmpty(dbHost) ? dbHost : (_configuration["Smtp:Host"] ?? "smtp.gmail.com");
        var portStr = !string.IsNullOrEmpty(dbPortStr) ? dbPortStr : (_configuration["Smtp:Port"] ?? "587");
        if (!int.TryParse(portStr, out int port)) port = 587;
        
        var user = !string.IsNullOrEmpty(dbUser) ? dbUser : _configuration["Smtp:User"];
        var pass = !string.IsNullOrEmpty(dbPassword) ? dbPassword : _configuration["Smtp:Password"];

        if (string.IsNullOrEmpty(user))
            throw new Exception("El correo emisor (SMTP User) no está configurado.");

        var emailMessage = new MimeMessage();
        emailMessage.From.Add(new MailboxAddress("Sistema de Préstamos y Cobros", user));
        emailMessage.To.Add(new MailboxAddress("", destinatario));
        emailMessage.Subject = asunto;

        var builder = new BodyBuilder
        {
            HtmlBody = cuerpo
        };

        if (adjuntoPdf != null && adjuntoPdf.Length > 0)
        {
            builder.Attachments.Add(nombreAdjunto, adjuntoPdf, new ContentType("application", "pdf"));
        }

        emailMessage.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        client.Timeout = 10000; // Limitar el tiempo de espera a 10 segundos para evitar que se quede cargando demasiado tiempo

        await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
        
        if (!string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(pass))
        {
            await client.AuthenticateAsync(user, pass);
        }
        
        await client.SendAsync(emailMessage);
        await client.DisconnectAsync(true);
    }
}
