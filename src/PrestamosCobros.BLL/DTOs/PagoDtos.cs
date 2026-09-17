using System.ComponentModel.DataAnnotations;

namespace PrestamosCobros.BLL.DTOs;

public class PagoCreateDto
{
    [Required]
    public int CuotaId { get; set; }

    [Required(ErrorMessage = "El monto es requerido")]
    [Range(0.01, 999999999, ErrorMessage = "El monto debe ser mayor a 0")]
    public decimal MontoPagado { get; set; }

    public bool EsPagoSoloInteres { get; set; }
}

public class PagoDetalleDto
{
    public int PagoId { get; set; }
    public int CuotaId { get; set; }
    public int NumeroCuota { get; set; }
    public decimal MontoPagado { get; set; }
    public DateTime FechaPago { get; set; }
    public string UsuarioNombre { get; set; } = string.Empty;
    public bool ComprobanteEnviado { get; set; }

    // Datos del préstamo para comprobante
    public int PrestamoId { get; set; }
    public string ClienteNombre { get; set; } = string.Empty;
    public string ClienteTelefono { get; set; } = string.Empty;
    public string? ClienteEmail { get; set; }
    public decimal SaldoPendiente { get; set; }
    public DateTime? FechaProximoPago { get; set; }
}
