namespace PrestamosCobros.DAL.Entities;

public class MensajeRecibido
{
    public int MensajeRecibidoId { get; set; }
    public int? ClienteId { get; set; }
    public string TelefonoRemitente { get; set; } = string.Empty;
    public string TipoMensaje { get; set; } = "text";
    public string Contenido { get; set; } = string.Empty;
    public string MetaMediaId { get; set; } = string.Empty;
    public DateTime FechaRecibido { get; set; } = DateTime.UtcNow;
    public bool Leido { get; set; } = false;

    // Navigation property
    public Cliente? Cliente { get; set; }
}
