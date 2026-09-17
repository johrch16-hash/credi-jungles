namespace PrestamosCobros.BLL.DTOs;

public class PrestamoCreateDto
{
    public int ClienteId { get; set; }
    public string? NombreAlias { get; set; }
    public decimal MontoPrestado { get; set; }
    public decimal PorcentajeInteres { get; set; } = 20;
    public DateTime FechaInicio { get; set; } = DateTime.Today;
    public int PeriodicidadDias { get; set; } = 7;
    public int NumeroCuotas { get; set; } = 4;
    public string? NumeroSinpe { get; set; }
    public string? CuentasBancarias { get; set; }
    public string? CuentaIban1 { get; set; }
    public string? CuentaIban2 { get; set; }
    public string? CuentaIban3 { get; set; }
    public string TipoCredito { get; set; } = "Cuota Completa";
}

public class PrestamoListDto
{
    public int PrestamoId { get; set; }
    public int ClienteId { get; set; }
    public string ClienteNombre { get; set; } = string.Empty;
    public string ClienteTelefono { get; set; } = string.Empty;
    public string? NombreAlias { get; set; }
    public decimal MontoPrestado { get; set; }
    public decimal MontoTotal { get; set; }
    public decimal SaldoPendiente { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string TipoCredito { get; set; } = "Cuota Completa";
    public DateTime FechaInicio { get; set; }
    public int CuotasPagadas { get; set; }
    public int CuotasTotal { get; set; }
    public int CuotasAtrasadas { get; set; }
    public DateTime? FechaVencimientoProx { get; set; }
    public bool Liquidado { get; set; }
    public int PorcentajePagado { get; set; }
}

public class PrestamoDetalleDto
{
    public int PrestamoId { get; set; }
    public int ClienteId { get; set; }
    public string ClienteNombre { get; set; } = string.Empty;
    public string ClienteTelefono { get; set; } = string.Empty;
    public string? NombreAlias { get; set; }
    public decimal MontoPrestado { get; set; }
    public decimal PorcentajeInteres { get; set; }
    public decimal MontoTotal { get; set; }
    public decimal SaldoPendiente { get; set; }
    public DateTime FechaInicio { get; set; }
    public int PeriodicidadDias { get; set; }
    public string? NumeroSinpe { get; set; }
    public string? CuentasBancarias { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string TipoCredito { get; set; } = "Cuota Completa";
    public DateTime FechaCreacion { get; set; }
    public List<CuotaDto> Cuotas { get; set; } = new();
}

public class CuotaDto
{
    public int CuotaId { get; set; }
    public int NumeroCuota { get; set; }
    public decimal Monto { get; set; }
    public DateTime FechaVencimiento { get; set; }
    public string Estado { get; set; } = string.Empty;
    public DateTime? FechaPago { get; set; }
    public decimal? MontoPagado { get; set; }
    public int PrestamoId { get; set; }
    public string? PrestamoAlias { get; set; }
}

public class PrestamoPreviewDto
{
    public decimal MontoPrestado { get; set; }
    public string TipoCredito { get; set; } = "Cuota Completa";
    public decimal PorcentajeInteres { get; set; }
    public decimal MontoInteres { get; set; }
    public decimal MontoTotal { get; set; } // Equivalente al Monto Base
    public decimal GastoLey { get; set; }
    public decimal MontoCreditoVisible { get; set; }
    public decimal MontoCuota { get; set; }
    public int NumeroCuotas { get; set; }
    public List<CuotaPreviewDto> Cuotas { get; set; } = new();
}

public class CuotaPreviewDto
{
    public int Numero { get; set; }
    public decimal Monto { get; set; }
    public DateTime FechaVencimiento { get; set; }
}

public class CreditoEditDto
{
    public int CreditoId { get; set; }
    public decimal MontoPrincipal { get; set; }
    public decimal TasaInternaAnual { get; set; }
    public decimal TasaVisibleMensual { get; set; }
    public int Plazo { get; set; }
    public string FrecuenciaPago { get; set; } = "Semanal";
    public string TipoCredito { get; set; } = "Cuota Completa";
    public string Estado { get; set; } = "Activo";
}

public class PrestamoRenovarDto
{
    public int PrestamoIdActual { get; set; }
    public decimal MontoNuevoSolicitado { get; set; }
    public decimal NuevoInteresPorcentaje { get; set; } = 20;
    public DateTime NuevaFechaInicio { get; set; } = DateTime.Today;
    public int NuevoCicloDias { get; set; } = 7;
    public int NuevasCuotas { get; set; } = 4;
    public string TipoCredito { get; set; } = "Cuota Completa";
}
