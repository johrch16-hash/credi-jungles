using Microsoft.EntityFrameworkCore;
using PrestamosCobros.BLL.DTOs;
using PrestamosCobros.BLL.Interfaces;
using PrestamosCobros.DAL.Repositories;

namespace PrestamosCobros.BLL.Services;

public class DashboardService : IDashboardService
{
    private readonly IUnitOfWork _unitOfWork;

    public DashboardService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<DashboardDto> ObtenerDashboardAsync()
    {
        var prestamos = _unitOfWork.Prestamos.Query();
        var pagos = _unitOfWork.Pagos.Query();
        var clientes = _unitOfWork.Clientes.Query();
        var cuotas = _unitOfWork.Cuotas.Query();
        var gastos = _unitOfWork.Gastos.Query();

        var hoy = DateTime.UtcNow.Date;

        var dto = new DashboardDto
        {
            TotalPrestado = await prestamos.SumAsync(p => p.MontoPrestado),
            TotalCobrado = await pagos.SumAsync(p => p.MontoPagado),
            SaldoPendiente = await prestamos.Where(p => p.Estado == "Activo").SumAsync(p => p.SaldoPendiente),
            TotalGastos = await gastos.SumAsync(g => g.Monto),
            PrestamosActivos = await prestamos.CountAsync(p => p.Estado == "Activo"),
            PrestamosLiquidados = await prestamos.CountAsync(p => p.Estado == "Liquidado"),
            ClientesActivos = await clientes.CountAsync(c => c.Activo),
            CuotasAtrasadas = await cuotas.CountAsync(c => c.Estado == "Atrasada")
        };

        // Ganancia = interés cobrado - gastos
        var totalIntereses = await prestamos.SumAsync(p => p.MontoTotal - p.MontoPrestado);
        dto.GananciaNetaEstimada = totalIntereses - dto.TotalGastos;

        // Actividad reciente (últimos 10 pagos + préstamos)
        var pagosRecientes = await pagos
            .Include(p => p.Cuota).ThenInclude(c => c.Prestamo).ThenInclude(pr => pr.Cliente)
            .OrderByDescending(p => p.FechaPago)
            .Take(5)
            .Select(p => new ActividadRecienteDto
            {
                Icono = "bi-cash-coin",
                Descripcion = $"Pago recibido — {p.Cuota.Prestamo.Cliente.NombreCompleto}",
                Detalle = $"₡{p.MontoPagado:N0} — Cuota #{p.Cuota.NumeroCuota}",
                Fecha = p.FechaPago
            })
            .ToListAsync();

        var prestamosRecientes = await prestamos
            .Include(p => p.Cliente)
            .Include(p => p.Cuotas)
            .OrderByDescending(p => p.FechaCreacion)
            .Take(5)
            .Select(p => new ActividadRecienteDto
            {
                Icono = "bi-wallet-fill",
                Descripcion = $"Nuevo préstamo — {p.Cliente.NombreCompleto}",
                Detalle = $"₡{p.MontoPrestado:N0} a {p.Cuotas.Count} cuotas",
                Fecha = p.FechaCreacion
            })
            .ToListAsync();

        dto.ActividadReciente = pagosRecientes
            .Concat(prestamosRecientes)
            .OrderByDescending(a => a.Fecha)
            .Take(8)
            .ToList();

        // Próximos vencimientos (7 días)
        var limite = hoy.AddDays(7);
        dto.ProximosVencimientos = await cuotas
            .Where(c => c.Estado != "Pagada" && c.FechaVencimiento <= limite)
            .Include(c => c.Prestamo).ThenInclude(p => p.Cliente)
            .OrderBy(c => c.FechaVencimiento)
            .Take(10)
            .Select(c => new CuotaProximaDto
            {
                PrestamoId = c.PrestamoId,
                ClienteNombre = c.Prestamo.Cliente.NombreCompleto,
                NumeroCuota = c.NumeroCuota,
                Monto = c.Monto,
                FechaVencimiento = c.FechaVencimiento,
                EsAtrasada = c.Estado == "Atrasada"
            })
            .ToListAsync();

        return dto;
    }
}
