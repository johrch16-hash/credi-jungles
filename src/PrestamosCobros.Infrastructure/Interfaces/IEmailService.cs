namespace PrestamosCobros.Infrastructure.Interfaces;

public interface IEmailService
{
    Task EnviarCorreoAsync(string destinatario, string asunto, string cuerpo, byte[]? adjuntoPdf = null, string nombreAdjunto = "Comprobante.pdf");
}
