using PrestamosCobros.BLL.DTOs;

namespace PrestamosCobros.BLL.Interfaces;

public interface IPagarePdfService
{
    byte[] GenerarPagare(PagareCreateDto dto);
}
