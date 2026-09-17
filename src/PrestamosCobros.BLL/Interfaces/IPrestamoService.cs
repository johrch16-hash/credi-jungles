using PrestamosCobros.BLL.DTOs;

namespace PrestamosCobros.BLL.Interfaces;

public interface IPrestamoService
{
    Task<List<PrestamoListDto>> ObtenerTodosAsync(string? estado = null, string? busqueda = null);
    Task<List<PrestamoListDto>> ObtenerPorClienteAsync(int clienteId);
    Task<PrestamoDetalleDto?> ObtenerPorIdAsync(int id);
    Task<PrestamoPreviewDto> GenerarPreviewAsync(PrestamoCreateDto dto);
    Task<(bool Exito, string Mensaje, int PrestamoId)> CrearAsync(PrestamoCreateDto dto);
    Task<(bool Exito, string Mensaje)> CancelarAsync(int id);
    Task<(bool Exito, string Mensaje)> ReactivarAsync(int id);
    Task<(bool Exito, string Mensaje)> EditarCreditoCompletoAsync(CreditoEditDto dto);
    Task<(bool Exito, string Mensaje)> EliminarAsync(int id);
    Task<(bool Exito, string Mensaje, int NuevoPrestamoId)> RenovarAsync(PrestamoRenovarDto dto);
    Task<(bool Exito, string Mensaje)> AbonarCapitalAsync(int id, decimal montoAbono, int usuarioId);
}
