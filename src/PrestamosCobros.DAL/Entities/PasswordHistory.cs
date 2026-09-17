namespace PrestamosCobros.DAL.Entities;

public class PasswordHistory
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime FechaCambio { get; set; } = DateTime.UtcNow;

    public Usuario Usuario { get; set; } = null!;
}
