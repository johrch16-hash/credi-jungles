namespace PrestamosCobros.DAL.Entities;

public class Pago
{
    public int PagoId { get; set; }
    public int CuotaId { get; set; }
    public int UsuarioId { get; set; }
    public decimal MontoPagado { get; set; }
    public DateTime FechaPago { get; set; } = DateTime.UtcNow;
    public bool ComprobanteEnviado { get; set; } = false;
    public string Estado { get; set; } = "Activo"; // Activo, Anulado
    public string? MotivoAnulacion { get; set; }

    // Navigation
    public Cuota Cuota { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
}
