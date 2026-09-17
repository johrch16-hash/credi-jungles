namespace PrestamosCobros.BLL.DTOs;

public class NotificacionListDto
{
    public int NotificacionId { get; set; }
    public string ClienteNombre { get; set; } = string.Empty;
    public string ClienteTelefono { get; set; } = string.Empty;
    public int PrestamoId { get; set; }
    public int NumeroCuota { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public string Canal { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaEnvio { get; set; }
}
