using PrestamosCobros.BLL.DTOs;

namespace PrestamosCobros.BLL.Interfaces;

public interface IGastoService
{
    Task<List<GastoListDto>> ObtenerTodosAsync(string? categoria = null, DateTime? desde = null, DateTime? hasta = null);
    Task CrearAsync(GastoCreateDto dto, int usuarioId);
    Task<GastoCreateDto?> ObtenerPorIdAsync(int gastoId);
    Task ActualizarAsync(int gastoId, GastoCreateDto dto);
    Task EliminarAsync(int gastoId);
}
