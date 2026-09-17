namespace PrestamosCobros.DAL.Entities;

public class Cliente
{
    public int ClienteId { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? CedulaCifrada { get; set; }
    public string? Direccion { get; set; }
    public string? LugarTrabajo { get; set; }
    public int NivelAtrasos { get; set; } = 0;
    public string Estado { get; set; } = "Normal";
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public virtual ICollection<Prestamo> Prestamos { get; set; } = new List<Prestamo>();
}
