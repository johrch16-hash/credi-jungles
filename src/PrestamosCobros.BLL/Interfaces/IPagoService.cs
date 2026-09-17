using PrestamosCobros.BLL.DTOs;

namespace PrestamosCobros.BLL.Interfaces;

public interface IPagoService
{
    Task<(bool Exito, string Mensaje, int PagoId)> RegistrarPagoAsync(PagoCreateDto dto, int usuarioId);
    Task<PagoDetalleDto?> ObtenerPagoAsync(int pagoId);
    Task<string> GenerarTextoComprobanteAsync(int pagoId);
    Task ProcesarReciboAsync(int pagoId);
    Task<(bool Exito, string Mensaje)> AnularPagoAsync(int pagoId, string motivo, int usuarioId);
}
