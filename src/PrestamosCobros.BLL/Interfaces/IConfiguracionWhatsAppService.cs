using System.Threading.Tasks;
using PrestamosCobros.DAL.Entities;

namespace PrestamosCobros.BLL.Interfaces
{
    public interface IConfiguracionWhatsAppService
    {
        Task<ConfiguracionWhatsApp?> ObtenerConfiguracion();
        Task GuardarConfiguracion(string phoneNumberId, string accessToken);
    }
}
