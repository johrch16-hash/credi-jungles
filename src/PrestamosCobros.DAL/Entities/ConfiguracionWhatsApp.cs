using System;
using System.ComponentModel.DataAnnotations;

namespace PrestamosCobros.DAL.Entities
{
    public class ConfiguracionWhatsApp
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string PhoneNumberId { get; set; } = string.Empty;

        [Required]
        public string AccessToken { get; set; } = string.Empty;

        public DateTime FechaActualizacion { get; set; } = DateTime.Now;
    }
}
