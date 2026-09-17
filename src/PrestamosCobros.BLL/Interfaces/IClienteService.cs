using PrestamosCobros.BLL.DTOs;

namespace PrestamosCobros.BLL.Interfaces;

public interface IClienteService
{
    Task<List<ClienteListDto>> ObtenerTodosAsync(string? busqueda = null, string? estado = null);
    Task<ClienteDetalleDto?> ObtenerPorIdAsync(int id);
    Task<ClienteEditDto?> ObtenerParaEditarAsync(int id);
    Task<(bool Exito, string Mensaje)> CrearAsync(ClienteCreateDto dto);
    Task<(bool Exito, string Mensaje)> EditarAsync(ClienteEditDto dto);
    Task<(bool Exito, string Mensaje)> DesactivarAsync(int id);
    Task<bool> ExisteTelefonoAsync(string telefono, int? exceptoId = null);
    Task<bool> ExisteCedulaAsync(string cedula, int? exceptoId = null);
}
