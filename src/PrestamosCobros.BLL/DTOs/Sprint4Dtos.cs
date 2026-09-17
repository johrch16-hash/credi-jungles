using System.ComponentModel.DataAnnotations;

namespace PrestamosCobros.BLL.DTOs;

// --- Gastos ---
public class GastoListDto
{
    public int GastoId { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public string Categoria { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public string UsuarioNombre { get; set; } = string.Empty;
}

public class GastoCreateDto
{
    [Required(ErrorMessage = "La descripción es requerida.")]
    [MaxLength(200)]
    public string Descripcion { get; set; } = string.Empty;

    [Required(ErrorMessage = "El monto es requerido.")]
    [Range(0.01, 999999999, ErrorMessage = "El monto debe ser mayor a 0.")]
    public decimal Monto { get; set; }

    [Required]
    public string Categoria { get; set; } = "General";

    public DateTime Fecha { get; set; } = DateTime.Today;
}

// --- Dashboard ---
public class DashboardDto
{
    // Cartera
    public decimal TotalPrestado { get; set; }
    public decimal TotalCobrado { get; set; }
    public decimal SaldoPendiente { get; set; }
    public decimal TotalGastos { get; set; }
    public decimal GananciaNetaEstimada { get; set; }

    // Contadores
    public int PrestamosActivos { get; set; }
    public int PrestamosLiquidados { get; set; }
    public int ClientesActivos { get; set; }
    public int CuotasAtrasadas { get; set; }

    // Actividad reciente
    public List<ActividadRecienteDto> ActividadReciente { get; set; } = new();

    // Próximos vencimientos
    public List<CuotaProximaDto> ProximosVencimientos { get; set; } = new();
}

public class ActividadRecienteDto
{
    public string Icono { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Detalle { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
}

public class CuotaProximaDto
{
    public int PrestamoId { get; set; }
    public string ClienteNombre { get; set; } = string.Empty;
    public int NumeroCuota { get; set; }
    public decimal Monto { get; set; }
    public DateTime FechaVencimiento { get; set; }
    public bool EsAtrasada { get; set; }
}

// --- Auditoría ---
public class BitacoraListDto
{
    public int BitacoraId { get; set; }
    public string UsuarioNombre { get; set; } = string.Empty;
    public string Accion { get; set; } = string.Empty;
    public string Detalle { get; set; } = string.Empty;
    public string? DireccionIP { get; set; }
    public DateTime Fecha { get; set; }
}
