using Microsoft.AspNetCore.Identity;

namespace PrestamosCobros.DAL.Entities;

public class Usuario : IdentityUser<int>
{
    public string NombreCompleto { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    // ── Permisos granulares del sistema ──
    public bool PermitirGestionClientes { get; set; } = true;
    public bool PermitirGestionPrestamos { get; set; } = true;
    public bool PermitirRegistroGastos { get; set; } = true;
    public bool PermitirRegistroPagos { get; set; } = true;
    public bool PermitirEnviarNotificaciones { get; set; } = true;
    public bool PermitirVerReportes { get; set; } = true;
    public bool PermitirVerAuditoria { get; set; } = true;
    public bool PermitirGestionPersonal { get; set; } = true;
    public bool PermitirConfigWhatsApp { get; set; } = true;
    public bool PermitirConfigCorreo { get; set; } = true;
    public bool PermitirPapelera { get; set; } = true;
}
