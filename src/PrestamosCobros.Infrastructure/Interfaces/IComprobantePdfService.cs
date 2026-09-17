namespace PrestamosCobros.Infrastructure.Interfaces;

public interface IComprobantePdfService
{
    byte[] GenerarComprobante(string clienteNombre, decimal montoPagado, DateTime fechaPago, int numeroCuota, decimal saldoRestante, int pagoId);
}
