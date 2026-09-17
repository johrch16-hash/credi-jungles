namespace PrestamosCobros.DAL.Entities;

public class Notificacion
{
    public int NotificacionId { get; set; }
    public int PrestamoId { get; set; }
    public int CuotaId { get; set; }
    public int ClienteId { get; set; }
    public string Tipo { get; set; } = string.Empty; // Recordatorio, Atraso, PagoConfirmado
    public string Mensaje { get; set; } = string.Empty;
    public string Canal { get; set; } = "WhatsApp"; // WhatsApp, Email
    public string Estado { get; set; } = "Pendiente"; // Pendiente, Enviado
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaEnvio { get; set; }

    // Navigation
    public Prestamo Prestamo { get; set; } = null!;
    public Cuota Cuota { get; set; } = null!;
    public Cliente Cliente { get; set; } = null!;
}
