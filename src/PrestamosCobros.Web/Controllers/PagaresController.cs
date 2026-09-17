using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PrestamosCobros.BLL.DTOs;
using PrestamosCobros.BLL.Interfaces;
using PrestamosCobros.DAL.Repositories;
using PrestamosCobros.Infrastructure.Security;

namespace PrestamosCobros.Web.Controllers;

[Authorize(Roles = "Admin,Master")]
public class PagaresController : Controller
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPagarePdfService _pdfService;
    private readonly ICifradoService _cifradoService;

    public PagaresController(IUnitOfWork unitOfWork, IPagarePdfService pdfService, ICifradoService cifradoService)
    {
        _unitOfWork = unitOfWork;
        _pdfService = pdfService;
        _cifradoService = cifradoService;
    }

    [HttpGet]
    public async Task<IActionResult> Crear()
    {
        await CargarClientesSelectList();
        var model = new PagareCreateDto();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(PagareCreateDto model)
    {
        if (!ModelState.IsValid)
        {
            await CargarClientesSelectList(model.ClienteId);
            return View(model);
        }

        var pdfBytes = _pdfService.GenerarPagare(model);
        var safeName = model.NombreDeudor.Replace(" ", "_");
        return File(pdfBytes, "application/pdf", $"Pagare_{safeName}_{DateTime.Now:yyyyMMdd}.pdf");
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerClienteInfo(int clienteId)
    {
        var cliente = await _unitOfWork.Clientes.Query().FirstOrDefaultAsync(c => c.ClienteId == clienteId);
        if (cliente == null) return NotFound();

        string? cedulaDescifrada = null;
        if (!string.IsNullOrEmpty(cliente.CedulaCifrada))
        {
            try
            {
                cedulaDescifrada = _cifradoService.Decrypt(cliente.CedulaCifrada);
            }
            catch { }
        }

        return Json(new
        {
            nombre = cliente.NombreCompleto,
            cedula = cedulaDescifrada ?? ""
        });
    }

    private async Task CargarClientesSelectList(int? selectedId = null)
    {
        var clientes = await _unitOfWork.Clientes.Query()
            .Where(c => c.Activo)
            .OrderBy(c => c.NombreCompleto)
            .Select(c => new { c.ClienteId, Texto = c.NombreCompleto + " - " + c.Telefono })
            .ToListAsync();

        ViewBag.Clientes = new SelectList(clientes, "ClienteId", "Texto", selectedId);
    }
}
