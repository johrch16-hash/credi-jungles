using System.ComponentModel.DataAnnotations;

namespace PrestamosCobros.BLL.DTOs;

public class UsuarioCreateDto
{
    [Required(ErrorMessage = "El nombre es requerido")]
    [MaxLength(100)]
    public string NombreCompleto { get; set; } = string.Empty;

    [Required(ErrorMessage = "El usuario o email es requerido")]
    [EmailAddress(ErrorMessage = "Email no válido")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Teléfono no válido")]
    [MaxLength(20)]
    public string? Telefono { get; set; }

    [Required(ErrorMessage = "La contraseña es requerida")]
    [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "El rol es requerido")]
    public string Rol { get; set; } = "Asistente";

    // ── Permisos del sistema ──
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

public class UsuarioEditDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El nombre es requerido")]
    [MaxLength(100)]
    public string NombreCompleto { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Teléfono no válido")]
    [MaxLength(20)]
    public string? Telefono { get; set; }

    [Required(ErrorMessage = "El rol es requerido")]
    public string Rol { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;

    // ── Permisos del sistema ──
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

public class UsuarioListDto
{
    public int Id { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string Rol { get; set; } = string.Empty;
    public bool Activo { get; set; }
}

public class LoginDto
{
    [Required(ErrorMessage = "El usuario o email es requerido")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es requerida")]
    public string Password { get; set; } = string.Empty;
}
