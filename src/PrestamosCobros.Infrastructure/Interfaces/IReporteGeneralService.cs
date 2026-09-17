namespace PrestamosCobros.Infrastructure.Interfaces;

public class ReporteGeneralItem
{
    public string ClienteNombre { get; set; } = string.Empty;
    public string? Alias { get; set; }
    public decimal MontoPrestado { get; set; }
    public decimal MontoTotal { get; set; }
    public decimal SaldoPendiente { get; set; }
    public int PorcentajePagado { get; set; }
    public string Estado { get; set; } = string.Empty;
}

public class ReporteIndividualModel
{
    public string ClienteNombre { get; set; } = string.Empty;
    public string? Cedula { get; set; }
    public string? Telefono { get; set; }
    public decimal SaldoTotalPendiente { get; set; }
    public List<PrestamoReporteItem> Prestamos { get; set; } = new();
}

public class PrestamoReporteItem
{
    public int PrestamoId { get; set; }
    public decimal MontoPrestado { get; set; }
    public decimal SaldoPendiente { get; set; }
    public string Estado { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; }
    public List<PagoReporteItem> Pagos { get; set; } = new();
}

public class PagoReporteItem
{
    public DateTime Fecha { get; set; }
    public decimal Monto { get; set; }
    public int NumeroCuota { get; set; }
}

public interface IReporteGeneralService
{
    byte[] GenerarReporteEstadoCuentaGeneral(List<ReporteGeneralItem> items);
    byte[] GenerarReporteEstadoCuentaIndividual(ReporteIndividualModel model);
}
