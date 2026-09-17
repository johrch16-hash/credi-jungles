using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PrestamosCobros.BLL.DTOs;
using PrestamosCobros.BLL.Interfaces;
using PrestamosCobros.DAL.Entities;
using PrestamosCobros.Infrastructure.Audit;

namespace PrestamosCobros.Web.Controllers;

[Authorize]
public class GastosController : Controller
{
    private readonly IGastoService _gastoService;
    private readonly IAuditoriaService _auditoria;
    private readonly UserManager<Usuario> _userManager;

    public GastosController(IGastoService gastoService, IAuditoriaService auditoria, UserManager<Usuario> userManager)
    {
        _gastoService = gastoService;
        _auditoria = auditoria;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(string? categoria, string? desde, string? hasta)
    {
        DateTime? desdeDt = string.IsNullOrEmpty(desde) ? null : DateTime.Parse(desde);
        DateTime? hastaDt = string.IsNullOrEmpty(hasta) ? null : DateTime.Parse(hasta);

        var gastos = await _gastoService.ObtenerTodosAsync(categoria, desdeDt, hastaDt);

        // Calcular totales globales del mes actual y anterior (sin filtros)
        var todosLosGastos = await _gastoService.ObtenerTodosAsync(null, null, null);
        var hoy = DateTime.Today;
        var primerDiaMesActual = new DateTime(hoy.Year, hoy.Month, 1);
        var primerDiaMesAnterior = primerDiaMesActual.AddMonths(-1);
        var ultimoDiaMesAnterior = primerDiaMesActual.AddDays(-1);

        decimal totalMesActual = todosLosGastos
            .Where(g => g.Fecha.Date >= primerDiaMesActual && g.Fecha.Date <= hoy)
            .Sum(g => g.Monto);

        decimal totalMesAnterior = todosLosGastos
            .Where(g => g.Fecha.Date >= primerDiaMesAnterior && g.Fecha.Date <= ultimoDiaMesAnterior)
            .Sum(g => g.Monto);

        ViewData["Categoria"] = categoria;
        ViewData["Desde"] = desde;
        ViewData["Hasta"] = hasta;
        ViewData["TotalGastos"] = gastos.Sum(g => g.Monto);
        ViewData["TotalMesActual"] = totalMesActual;
        ViewData["TotalMesAnterior"] = totalMesAnterior;

        return View(gastos);
    }

    [Authorize(Roles = "Admin")]
    public IActionResult Crear() => View(new GastoCreateDto());

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(GastoCreateDto dto)
    {
        if (!ModelState.IsValid) return View(dto);

        var userId = int.Parse(_userManager.GetUserId(User)!);
        await _gastoService.CrearAsync(dto, userId);
        await _auditoria.RegistrarAsync(userId, "Gasto registrado", $"{dto.Descripcion} — ₡{dto.Monto:N0}");

        TempData["Exito"] = "Gasto registrado correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Editar(int id)
    {
        var dto = await _gastoService.ObtenerPorIdAsync(id);
        if (dto == null)
        {
            TempData["Error"] = "El gasto no existe.";
            return RedirectToAction(nameof(Index));
        }
        ViewData["GastoId"] = id;
        return View(dto);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int id, GastoCreateDto dto)
    {
        if (!ModelState.IsValid)
        {
            ViewData["GastoId"] = id;
            return View(dto);
        }

        var exist = await _gastoService.ObtenerPorIdAsync(id);
        if (exist == null)
        {
            TempData["Error"] = "El gasto no existe.";
            return RedirectToAction(nameof(Index));
        }

        await _gastoService.ActualizarAsync(id, dto);
        var userId = int.Parse(_userManager.GetUserId(User)!);
        await _auditoria.RegistrarAsync(userId, "Gasto modificado", $"{dto.Descripcion} — ₡{dto.Monto:N0} (GastoId: {id})");

        TempData["Exito"] = "Gasto modificado correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(int id)
    {
        var userId = int.Parse(_userManager.GetUserId(User)!);
        await _gastoService.EliminarAsync(id);
        await _auditoria.RegistrarAsync(userId, "Gasto eliminado", $"GastoId: {id}");

        TempData["Exito"] = "Gasto eliminado.";
        return RedirectToAction(nameof(Index));
    }
}
