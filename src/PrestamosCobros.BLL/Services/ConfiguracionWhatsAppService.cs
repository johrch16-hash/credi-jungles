using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PrestamosCobros.BLL.Interfaces;
using PrestamosCobros.DAL.Entities;
using PrestamosCobros.DAL.Repositories;

namespace PrestamosCobros.BLL.Services
{
    public class ConfiguracionWhatsAppService : IConfiguracionWhatsAppService
    {
        private readonly IUnitOfWork _unitOfWork;

        public ConfiguracionWhatsAppService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ConfiguracionWhatsApp?> ObtenerConfiguracion()
        {
            return await _unitOfWork.ConfiguracionesWhatsApp.Query().FirstOrDefaultAsync();
        }

        public async Task GuardarConfiguracion(string phoneNumberId, string accessToken)
        {
            var config = await _unitOfWork.ConfiguracionesWhatsApp.Query().FirstOrDefaultAsync();

            if (config == null)
            {
                config = new ConfiguracionWhatsApp
                {
                    PhoneNumberId = phoneNumberId,
                    AccessToken = accessToken,
                    FechaActualizacion = DateTime.Now
                };
                await _unitOfWork.ConfiguracionesWhatsApp.AddAsync(config);
            }
            else
            {
                config.PhoneNumberId = phoneNumberId;
                config.AccessToken = accessToken;
                config.FechaActualizacion = DateTime.Now;
                _unitOfWork.ConfiguracionesWhatsApp.Update(config);
            }

            await _unitOfWork.SaveChangesAsync();
        }
    }
}
