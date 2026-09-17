using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrestamosCobros.BLL.Interfaces;
using PrestamosCobros.Infrastructure.Interfaces;

namespace PrestamosCobros.Web.Controllers;

[Authorize(Roles = "Admin")]
public class ReportesController : Controller
{
    private readonly IPrestamoService _prestamoService;
    private readonly IReporteGeneralService _reporteService;
    private readonly IClienteService _clienteService;

    public ReportesController(IPrestamoService prestamoService, IReporteGeneralService reporteService, IClienteService clienteService)
    {
        _prestamoService = prestamoService;
        _reporteService = reporteService;
        _clienteService = clienteService;
    }

    public async Task<IActionResult> Index()
    {
        var clientes = await _clienteService.ObtenerTodosAsync();
        return View(clientes);
    }

    public async Task<IActionResult> EstadoCuentaGeneral()
    {
        var prestamos = await _prestamoService.ObtenerTodosAsync();
        
        var items = prestamos.Select(p => new ReporteGeneralItem
        {
            ClienteNombre = p.ClienteNombre,
            Alias = p.NombreAlias,
            MontoPrestado = p.MontoPrestado,
            MontoTotal = p.MontoTotal,
            SaldoPendiente = p.SaldoPendiente,
            PorcentajePagado = p.PorcentajePagado,
            Estado = p.Estado
        }).ToList();

        var pdf = _reporteService.GenerarReporteEstadoCuentaGeneral(items);
        
        string fileName = $"Reporte_General_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
        return File(pdf, "application/pdf", fileName);
    }

    public async Task<IActionResult> EstadoCuentaIndividual(int clienteId)
    {
        var cliente = await _clienteService.ObtenerPorIdAsync(clienteId);
        if (cliente == null) return NotFound();

        var prestamosResumen = await _prestamoService.ObtenerPorClienteAsync(clienteId);
        var model = new ReporteIndividualModel
        {
            ClienteNombre = cliente.NombreCompleto,
            Cedula = cliente.CedulaEnmascarada,
            Telefono = cliente.Telefono,
            SaldoTotalPendiente = prestamosResumen.Sum(p => p.SaldoPendiente)
        };

        foreach (var pResumen in prestamosResumen)
        {
            var pDetalle = await _prestamoService.ObtenerPorIdAsync(pResumen.PrestamoId);
            if (pDetalle == null) continue;

            var item = new PrestamoReporteItem
            {
                PrestamoId = pDetalle.PrestamoId,
                MontoPrestado = pDetalle.MontoPrestado,
                SaldoPendiente = pDetalle.SaldoPendiente,
                Estado = pDetalle.Estado,
                FechaCreacion = pDetalle.FechaCreacion,
                Pagos = pDetalle.Cuotas
                    .Where(c => c.FechaPago.HasValue)
                    .Select(c => new PagoReporteItem
                    {
                        Fecha = c.FechaPago!.Value,
                        Monto = c.MontoPagado ?? 0,
                        NumeroCuota = c.NumeroCuota
                    }).ToList()
            };
            model.Prestamos.Add(item);
        }

        var pdf = _reporteService.GenerarReporteEstadoCuentaIndividual(model);
        string fileName = $"Estado_Cuenta_{cliente.NombreCompleto.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.pdf";
        return File(pdf, "application/pdf", fileName);
    }

    [HttpGet]
    public async Task<IActionResult> GetClienteDetalle(int clienteId)
    {
        var cliente = await _clienteService.ObtenerPorIdAsync(clienteId);
        if (cliente == null) return NotFound();

        var prestamos = await _prestamoService.ObtenerPorClienteAsync(clienteId);
        
        return Json(new {
            nombre = cliente.NombreCompleto,
            cedula = cliente.CedulaEnmascarada,
            saldoTotal = prestamos.Sum(p => p.SaldoPendiente),
            prestamos = prestamos.Select(p => new {
                p.PrestamoId,
                p.MontoPrestado,
                p.SaldoPendiente,
                p.Estado,
                p.PorcentajePagado
            })
        });
    }
}
