namespace PrestamosCobros.BLL.DTOs;

public class ClienteCreateDto
{
    public string NombreCompleto { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Cedula { get; set; }
    public string? Direccion { get; set; }
    public string? LugarTrabajo { get; set; }
}

public class ClienteEditDto : ClienteCreateDto
{
    public int ClienteId { get; set; }
}

public class ClienteListDto
{
    public int ClienteId { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public int NivelAtrasos { get; set; }
    public string Estado { get; set; } = string.Empty;
}

public class ClienteDetalleDto
{
    public int ClienteId { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? CedulaEnmascarada { get; set; }
    public string? Direccion { get; set; }
    public string? LugarTrabajo { get; set; }
    public int NivelAtrasos { get; set; }
    public string Estado { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; }
    public List<PrestamoListDto> Prestamos { get; set; } = new();
    public List<CuotaDto> CuotasPendientes { get; set; } = new();
}

public class ClienteInactivoDto
{
    public int ClienteId { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public int PrestamosActivos { get; set; }
}
