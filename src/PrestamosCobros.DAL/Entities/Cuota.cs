namespace PrestamosCobros.DAL.Entities;

public class Cuota
{
    public int CuotaId { get; set; }
    public int PrestamoId { get; set; }
    public int NumeroCuota { get; set; }
    public decimal Monto { get; set; }
    public DateTime FechaVencimiento { get; set; }
    public string Estado { get; set; } = "Pendiente"; // Pendiente, Atrasado, Pagado
    public DateTime? FechaPago { get; set; }

    // Navigation
    public Prestamo Prestamo { get; set; } = null!;
    public Pago? Pago { get; set; }
}
