using System;
using System.ComponentModel.DataAnnotations;

namespace PrestamosCobros.BLL.DTOs;

public class PagareCreateDto
{
    // Datos del Deudor
    public int? ClienteId { get; set; }
    
    [Required(ErrorMessage = "El nombre del deudor es obligatorio.")]
    public string NombreDeudor { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "La cédula del deudor es obligatoria.")]
    public string CedulaDeudor { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "El estado civil es obligatorio.")]
    public string EstadoCivil { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "La profesión u oficio es obligatoria.")]
    public string Profesion { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "La dirección exacta es obligatoria.")]
    public string DireccionHabitacion { get; set; } = string.Empty;

    // Datos Financieros
    [Required(ErrorMessage = "El monto es obligatorio.")]
    [Range(1, double.MaxValue, ErrorMessage = "El monto debe ser mayor a 0.")]
    public decimal Monto { get; set; }
    
    public string MontoLetras { get; set; } = string.Empty;

    [Required(ErrorMessage = "La tasa corriente es obligatoria.")]
    public decimal TasaCorriente { get; set; }

    [Required(ErrorMessage = "La tasa moratoria es obligatoria.")]
    public decimal TasaMoratoria { get; set; }

    [Required(ErrorMessage = "La fecha de vencimiento es obligatoria.")]
    public DateTime FechaVencimiento { get; set; } = DateTime.Today.AddMonths(1);

    // Datos del Fiador (Opcionales)
    public bool TieneFiador { get; set; }
    public string? NombreFiador { get; set; }
    public string? CedulaFiador { get; set; }
    public string? DireccionFiador { get; set; }
}
