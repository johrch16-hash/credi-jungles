using PrestamosCobros.BLL.DTOs;

namespace PrestamosCobros.BLL.Interfaces;

public interface INotificacionService
{
    Task<List<NotificacionListDto>> ObtenerTodasAsync(string? tipo = null, string? estado = null);
    Task GenerarRecordatoriosAsync();
    Task MarcarCuotasAtrasadasAsync();
    Task MarcarComoEnviadaAsync(int notificacionId);
    Task<string> ObtenerLinkWhatsAppAsync(int notificacionId);
    Task<string> ObtenerPlantillaAsync(string clave);
    Task ActualizarPlantillaAsync(string clave, string valor);
    Task<bool> EnviarRecordatorioManualAsync(int cuotaId, bool esSoloInteres = false);
    Task<bool> EnviarCorreoManualAsync(int cuotaId);
    Task<string> ObtenerPreferenciaAsync(string clave, string valorPorDefecto);
    Task GuardarPreferenciaAsync(string clave, string valor);
    Task EnviarRecordatoriosPendientesAsync();
    Task EliminarAsync(int notificacionId);
}
