using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrestamosCobros.BLL.DTOs;
using PrestamosCobros.BLL.Interfaces;
using PrestamosCobros.DAL.Entities;
using PrestamosCobros.Infrastructure.Audit;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace PrestamosCobros.Web.Controllers;

[Authorize]
public class ClientesController : Controller
{
    private readonly IClienteService _clienteService;
    private readonly IPrestamoService _prestamoService;
    private readonly IAuditoriaService _auditoria;
    private readonly UserManager<Usuario> _userManager;
    private readonly PrestamosCobros.DAL.Repositories.IUnitOfWork _unitOfWork;
    private readonly PrestamosCobros.BLL.Services.WhatsAppService _whatsAppService;

    public ClientesController(
        IClienteService clienteService,
        IPrestamoService prestamoService,
        IAuditoriaService auditoria,
        UserManager<Usuario> userManager,
        PrestamosCobros.DAL.Repositories.IUnitOfWork unitOfWork,
        PrestamosCobros.BLL.Services.WhatsAppService whatsAppService)
    {
        _clienteService = clienteService;
        _prestamoService = prestamoService;
        _auditoria = auditoria;
        _userManager = userManager;
        _unitOfWork = unitOfWork;
        _whatsAppService = whatsAppService;
    }

    public async Task<IActionResult> Index(string? busqueda, string? estado)
    {
        var clientes = await _clienteService.ObtenerTodosAsync(busqueda, estado);
        ViewData["Busqueda"] = busqueda;
        ViewData["Estado"] = estado;
        return View(clientes);
    }

    // AJAX endpoint para búsqueda dinámica
    [HttpGet]
    public async Task<IActionResult> Buscar(string? busqueda, string? estado)
    {
        var clientes = await _clienteService.ObtenerTodosAsync(busqueda, estado);
        return PartialView("_ClienteTabla", clientes);
    }

    public async Task<IActionResult> Detalle(int id)
    {
        var cliente = await _clienteService.ObtenerPorIdAsync(id);
        if (cliente == null) return NotFound();

        ViewBag.Prestamos = await _prestamoService.ObtenerPorClienteAsync(id);
        return View(cliente);
    }

    public async Task<IActionResult> Historial(int id)
    {
        var cliente = await _clienteService.ObtenerPorIdAsync(id);
        if (cliente == null) return NotFound();

        ViewBag.Prestamos = await _prestamoService.ObtenerPorClienteAsync(id);
        return View(cliente);
    }

    [Authorize(Roles = "Admin,Asistente")]
    public IActionResult Crear() => View(new ClienteCreateDto());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Asistente")]
    public async Task<IActionResult> Crear(ClienteCreateDto model)
    {
        if (!ModelState.IsValid) return View(model);

        try
        {
            var (exito, mensaje) = await _clienteService.CrearAsync(model);
            if (!exito)
            {
                ModelState.AddModelError("", mensaje);
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            if (user != null)
            {
                await _auditoria.RegistrarAsync(user.Id, "Crear cliente",
                    $"Cliente '{model.NombreCompleto}' creado", ip);
            }

            try
            {
                var templateName = (await _unitOfWork.Preferencias.Query()
                    .FirstOrDefaultAsync(p => p.Clave == "Notificacion_CreacionClienteTemplateName"))?.Valor ?? "creacion_de_cliente";
                
                // Fetch the newly created client to get the ID
                var nuevoCliente = await _unitOfWork.Clientes.Query().OrderByDescending(c => c.ClienteId).FirstOrDefaultAsync(c => c.Telefono == model.Telefono && c.NombreCompleto == model.NombreCompleto);
                
                await _whatsAppService.EnviarMensajePlantillaAsync(
                    model.Telefono, templateName, "es", new string[] { model.NombreCompleto }
                );
                
                if (nuevoCliente != null)
                {
                    // Notification tracking for CreacionCliente removed because Notificacion table requires non-nullable PrestamoId/CuotaId
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al enviar notificación de creación de cliente: {ex.Message}");
            }

            TempData["Mensaje"] = mensaje;
            return RedirectToAction("Index");
        }
        catch (ApplicationException ex)
        {
            var isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest" || 
                         Request.Headers["Accept"].ToString().Contains("application/json");

            if (isAjax)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            
            ModelState.AddModelError("", ex.Message);
            return View(model);
        }
    }

    [Authorize(Roles = "Admin,Asistente")]
    public async Task<IActionResult> Editar(int id)
    {
        var cliente = await _clienteService.ObtenerParaEditarAsync(id);
        if (cliente == null) return NotFound();
        return View(cliente);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Asistente")]
    public async Task<IActionResult> Editar(ClienteEditDto model)
    {
        if (!ModelState.IsValid) return View(model);

        var (exito, mensaje) = await _clienteService.EditarAsync(model);
        if (!exito)
        {
            ModelState.AddModelError("", mensaje);
            return View(model);
        }

        var user = await _userManager.GetUserAsync(User);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditoria.RegistrarAsync(user!.Id, "Editar cliente",
            $"Cliente ID={model.ClienteId} '{model.NombreCompleto}' editado", ip);

        TempData["Mensaje"] = mensaje;
        return RedirectToAction("Detalle", new { id = model.ClienteId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Desactivar(int id)
    {
        var (exito, mensaje) = await _clienteService.DesactivarAsync(id);
        if (!exito)
        {
            TempData["Error"] = mensaje;
            return RedirectToAction("Index");
        }

        var user = await _userManager.GetUserAsync(User);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditoria.RegistrarAsync(user!.Id, "Desactivar cliente",
            $"Cliente ID={id} desactivado", ip);

        TempData["Mensaje"] = mensaje;
        return RedirectToAction("Index");
    }

    // AJAX: verificar teléfono
    [HttpGet]
    public async Task<IActionResult> VerificarTelefono(string telefono, int? exceptoId)
    {
        var existe = await _clienteService.ExisteTelefonoAsync(telefono, exceptoId);
        return Json(new { existe });
    }

    // AJAX: verificar cédula
    [HttpGet]
    public async Task<IActionResult> VerificarCedula(string cedula, int? exceptoId)
    {
        var existe = await _clienteService.ExisteCedulaAsync(cedula, exceptoId);
        return Json(new { existe });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Master")]
    public async Task<IActionResult> EliminarPermanente(int id)
    {
        var cliente = await _unitOfWork.Clientes.Query()
            .Include(c => c.Prestamos)
            .FirstOrDefaultAsync(c => c.ClienteId == id);

        if (cliente == null) return NotFound();

        try
        {
            var prestamoIds = cliente.Prestamos.Select(p => p.PrestamoId).ToList();
            if (prestamoIds.Any())
            {
                var cuotas = await _unitOfWork.Cuotas.Query().Where(c => prestamoIds.Contains(c.PrestamoId)).ToListAsync();
                var cuotaIds = cuotas.Select(c => c.CuotaId).ToList();

                if (cuotaIds.Any())
                {
                    var pagos = await _unitOfWork.Pagos.Query().Where(p => cuotaIds.Contains(p.CuotaId)).ToListAsync();
                    foreach(var p in pagos) _unitOfWork.Pagos.Remove(p);
                }

                foreach(var c in cuotas) _unitOfWork.Cuotas.Remove(c);
                
                var notificaciones = await _unitOfWork.Notificaciones.Query().Where(n => n.ClienteId == id).ToListAsync();
                foreach(var n in notificaciones) _unitOfWork.Notificaciones.Remove(n);

                foreach(var p in cliente.Prestamos.ToList()) _unitOfWork.Prestamos.Remove(p);
            }
            else 
            {
                var notificaciones = await _unitOfWork.Notificaciones.Query().Where(n => n.ClienteId == id).ToListAsync();
                foreach(var n in notificaciones) _unitOfWork.Notificaciones.Remove(n);
            }

            var mensajes = await _unitOfWork.MensajesRecibidos.Query().Where(m => m.ClienteId == id).ToListAsync();
            foreach(var m in mensajes) _unitOfWork.MensajesRecibidos.Remove(m);

            _unitOfWork.Clientes.Remove(cliente);
            await _unitOfWork.SaveChangesAsync();

            var user = await _userManager.GetUserAsync(User);
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _auditoria.RegistrarAsync(user!.Id, "Eliminar Cliente",
                $"Cliente '{cliente.NombreCompleto}' y sus dependencias fueron eliminados.", ip);

            TempData["Mensaje"] = "Cliente eliminado permanentemente.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Error al intentar eliminar el cliente: " + ex.Message;
        }

        return RedirectToAction("Index", "Clientes");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Asistente,Master")]
    public async Task<IActionResult> EnviarMensajeTexto(int clienteId, string mensaje)
    {
        if (string.IsNullOrWhiteSpace(mensaje))
            return Json(new { success = false, message = "El mensaje está vacío." });

        var cliente = await _unitOfWork.Clientes.Query().FirstOrDefaultAsync(c => c.ClienteId == clienteId);
        if (cliente == null)
            return Json(new { success = false, message = "Cliente no encontrado." });

        if (string.IsNullOrEmpty(cliente.Telefono))
            return Json(new { success = false, message = "El cliente no tiene teléfono." });

        try
        {
            var enviado = await _whatsAppService.EnviarMensajeTexto(cliente.Telefono, mensaje.Trim());

            if (enviado)
            {
                // Add to Inbox chat
                await _unitOfWork.MensajesRecibidos.AddAsync(new PrestamosCobros.DAL.Entities.MensajeRecibido
                {
                    ClienteId = cliente.ClienteId,
                    TelefonoRemitente = cliente.Telefono,
                    TipoMensaje = "sent_text",
                    Contenido = mensaje,
                    FechaRecibido = DateTime.UtcNow,
                    Leido = true
                });

                await _unitOfWork.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false, message = "Meta rechazó el envío del mensaje de texto." });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error enviando mensaje de texto: {ex.Message}");
            return Json(new { success = false, message = ex.Message });
        }
    }
}
