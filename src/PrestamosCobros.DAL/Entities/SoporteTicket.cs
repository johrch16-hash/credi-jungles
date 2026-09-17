using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrestamosCobros.DAL.Entities;

public class SoporteTicket
{
    public int SoporteTicketId { get; set; }
    public int UsuarioId { get; set; }
    public string NombreUsuario { get; set; } = string.Empty;
    public string EmailUsuario { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    
    [NotMapped]
    public string Detalle
    {
        get => Mensaje;
        set => Mensaje = value;
    }

    public string? ImagenUrl { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public string Estado { get; set; } = "Pendiente"; // "Pendiente", "En Proceso", "Resuelto", "Cerrado"
    public string? RespuestaMaster { get; set; }
    public DateTime? FechaRespuesta { get; set; }

    public virtual Usuario? Usuario { get; set; }
}
