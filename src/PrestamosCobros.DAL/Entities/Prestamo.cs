namespace PrestamosCobros.DAL.Entities;

public class Prestamo
{
    public int PrestamoId { get; set; }
    public int ClienteId { get; set; }
    public string? NombreAlias { get; set; }
    public decimal MontoPrestado { get; set; }
    public decimal PorcentajeInteres { get; set; } = 20m;
    public decimal MontoTotal { get; set; }
    public decimal SaldoPendiente { get; set; }
    public DateTime FechaInicio { get; set; }
    public int PeriodicidadDias { get; set; } = 7;
    public string? NumeroSinpe { get; set; }
    public string? CuentasBancarias { get; set; }
    public string TipoCredito { get; set; } = "Cuota Completa"; // "Cuota Completa" o "Solo Interés"
    public string Estado { get; set; } = "Activo"; // Activo, Liquidado, Cancelado
    public bool Liquidado => Estado == "Liquidado" || SaldoPendiente <= 0;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    // Navigation
    public Cliente Cliente { get; set; } = null!;
    public List<Cuota> Cuotas { get; set; } = new();
}
