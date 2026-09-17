using PrestamosCobros.BLL.DTOs;

namespace PrestamosCobros.BLL.Interfaces;

public interface IDashboardService
{
    Task<DashboardDto> ObtenerDashboardAsync();
}
