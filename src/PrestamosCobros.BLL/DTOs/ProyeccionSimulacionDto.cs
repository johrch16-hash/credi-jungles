using System.ComponentModel.DataAnnotations;

namespace PrestamosCobros.BLL.DTOs;

public class ProyeccionSimulacionDto
{
    public int ClienteId { get; set; }
    public string NombreCliente { get; set; } = string.Empty;
    public string TelefonoCliente { get; set; } = string.Empty;
    public string CapitalFormateado { get; set; } = string.Empty;
    public string InteresFormateado { get; set; } = string.Empty;
    public string CuotaFormateada { get; set; } = string.Empty;
    public string DetalleCuotas { get; set; } = string.Empty;
    public string TotalFormateado { get; set; } = string.Empty;
}
