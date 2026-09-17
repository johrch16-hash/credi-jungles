using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrestamosCobros.BLL.DTOs;
using PrestamosCobros.DAL.Repositories;

namespace PrestamosCobros.Web.Controllers;

[Authorize(Roles = "Admin")]
public class BitacoraController : Controller
{
    private readonly IUnitOfWork _unitOfWork;

    public BitacoraController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IActionResult> Index(string? busqueda, string? desde, string? hasta)
    {
        var query = _unitOfWork.Bitacoras.Query()
            .Include(b => b.Usuario)
            .AsQueryable();

        if (!string.IsNullOrEmpty(busqueda))
            query = query.Where(b => b.Accion.Contains(busqueda) ||
                                     b.Detalle!.Contains(busqueda) ||
                                     b.Usuario.NombreCompleto.Contains(busqueda));

        if (!string.IsNullOrEmpty(desde))
        {
            var desdeDt = DateTime.Parse(desde);
            query = query.Where(b => b.Fecha >= desdeDt);
        }

        if (!string.IsNullOrEmpty(hasta))
        {
            var hastaDt = DateTime.Parse(hasta).AddDays(1);
            query = query.Where(b => b.Fecha <= hastaDt);
        }

        var bitacora = await query.OrderByDescending(b => b.Fecha)
            .Take(200)
            .Select(b => new BitacoraListDto
            {
                BitacoraId = b.BitacoraId,
                UsuarioNombre = b.Usuario.NombreCompleto,
                Accion = b.Accion,
                Detalle = b.Detalle ?? "",
                DireccionIP = b.DireccionIP,
                Fecha = b.Fecha
            })
            .ToListAsync();

        ViewData["Busqueda"] = busqueda;
        ViewData["Desde"] = desde;
        ViewData["Hasta"] = hasta;

        return View(bitacora);
    }
}
