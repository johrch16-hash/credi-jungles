using Microsoft.EntityFrameworkCore;
using PrestamosCobros.BLL.DTOs;
using PrestamosCobros.BLL.Interfaces;
using PrestamosCobros.DAL.Entities;
using PrestamosCobros.DAL.Repositories;

namespace PrestamosCobros.BLL.Services;

public class GastoService : IGastoService
{
    private readonly IUnitOfWork _unitOfWork;

    public GastoService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<GastoListDto>> ObtenerTodosAsync(string? categoria = null, DateTime? desde = null, DateTime? hasta = null)
    {
        var query = _unitOfWork.Gastos.Query()
            .Include(g => g.Usuario)
            .AsQueryable();

        if (!string.IsNullOrEmpty(categoria))
            query = query.Where(g => g.Categoria == categoria);

        if (desde.HasValue)
            query = query.Where(g => g.Fecha >= desde.Value);

        if (hasta.HasValue)
            query = query.Where(g => g.Fecha <= hasta.Value.AddDays(1));

        return await query.OrderByDescending(g => g.Fecha)
            .Select(g => new GastoListDto
            {
                GastoId = g.GastoId,
                Descripcion = g.Descripcion,
                Monto = g.Monto,
                Categoria = g.Categoria,
                Fecha = g.Fecha,
                UsuarioNombre = g.Usuario.NombreCompleto
            })
            .ToListAsync();
    }

    public async Task CrearAsync(GastoCreateDto dto, int usuarioId)
    {
        var gasto = new Gasto
        {
            Descripcion = dto.Descripcion,
            Monto = dto.Monto,
            Categoria = dto.Categoria,
            Fecha = dto.Fecha,
            UsuarioId = usuarioId
        };

        await _unitOfWork.Gastos.AddAsync(gasto);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task EliminarAsync(int gastoId)
    {
        var gasto = await _unitOfWork.Gastos.GetByIdAsync(gastoId);
        if (gasto != null)
        {
            _unitOfWork.Gastos.Remove(gasto);
            await _unitOfWork.SaveChangesAsync();
        }
    }

    public async Task<GastoCreateDto?> ObtenerPorIdAsync(int gastoId)
    {
        var gasto = await _unitOfWork.Gastos.GetByIdAsync(gastoId);
        if (gasto == null) return null;

        return new GastoCreateDto
        {
            Descripcion = gasto.Descripcion,
            Monto = gasto.Monto,
            Categoria = gasto.Categoria,
            Fecha = gasto.Fecha
        };
    }

    public async Task ActualizarAsync(int gastoId, GastoCreateDto dto)
    {
        var gasto = await _unitOfWork.Gastos.GetByIdAsync(gastoId);
        if (gasto != null)
        {
            gasto.Descripcion = dto.Descripcion;
            gasto.Monto = dto.Monto;
            gasto.Categoria = dto.Categoria;
            gasto.Fecha = dto.Fecha;

            _unitOfWork.Gastos.Update(gasto);
            await _unitOfWork.SaveChangesAsync();
        }
    }
}
