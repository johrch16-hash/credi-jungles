using System.ComponentModel.DataAnnotations;

namespace PrestamosCobros.DAL.Entities;

public class Gasto
{
    public int GastoId { get; set; }

    [Required, MaxLength(200)]
    public string Descripcion { get; set; } = string.Empty;

    public decimal Monto { get; set; }

    [Required, MaxLength(50)]
    public string Categoria { get; set; } = "General"; // General, Transporte, Comunicación, Oficina, Otro

    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
}
