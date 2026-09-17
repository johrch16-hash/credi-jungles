namespace PrestamosCobros.DAL.Entities;

public class Bitacora
{
    public int BitacoraId { get; set; }
    public int UsuarioId { get; set; }
    public string Accion { get; set; } = string.Empty;
    public string? Detalle { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public string? DireccionIP { get; set; }

    // Navigation
    public Usuario Usuario { get; set; } = null!;
}
