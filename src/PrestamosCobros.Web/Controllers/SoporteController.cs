using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrestamosCobros.DAL.Entities;
using PrestamosCobros.DAL.Repositories;
using PrestamosCobros.Infrastructure.Interfaces;

namespace PrestamosCobros.Web.Controllers;

[Authorize]
public class SoporteController : Controller
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<Usuario> _userManager;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<SoporteController> _logger;

    public SoporteController(
        IUnitOfWork unitOfWork,
        UserManager<Usuario> userManager,
        IWebHostEnvironment environment,
        ILogger<SoporteController> logger)
    {
        _unitOfWork = unitOfWork;
        _userManager = userManager;
        _environment = environment;
        _logger = logger;
    }

    /// <summary>
    /// Vista principal de soporte:
    /// - Si es usuario Master: Muestra la bandeja unificada con todos los tickets enviados por administradores.
    /// - Si no es Master: Muestra los tickets creados por el usuario actual.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(string? estado = null, bool success = false)
    {
        bool esMaster = User.IsInRole("Master");
        ViewData["EsMaster"] = esMaster;
        ViewData["EstadoFiltro"] = estado ?? "Todos";
        
        if (success)
        {
            ViewBag.SuccessMessage = "Su reporte de soporte ha sido enviado correctamente a la bandeja del usuario Master.";
        }

        List<SoporteTicket> tickets = new();

        try
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            try
            {
                var query = _unitOfWork.SoporteTickets.Query().AsNoTracking();

                if (!esMaster)
                {
                    query = query.Where(t => t.UsuarioId == user.Id);
                }

                if (!string.IsNullOrEmpty(estado) && estado != "Todos")
                {
                    query = query.Where(t => t.Estado == estado);
                }

                tickets = await query
                    .OrderByDescending(t => t.FechaCreacion)
                    .ToListAsync();
            }
            catch (Exception exDb)
            {
                _logger.LogWarning(exDb, "Advertencia al consultar SoporteTickets en DB.");
                tickets = new List<SoporteTicket>();
            }

            try
            {
                var todosQuery = _unitOfWork.SoporteTickets.Query().AsNoTracking();
                if (!esMaster) todosQuery = todosQuery.Where(t => t.UsuarioId == user.Id);

                ViewBag.TotalTickets = await todosQuery.CountAsync();
                ViewBag.Pendientes = await todosQuery.CountAsync(t => t.Estado == "Pendiente");
                ViewBag.EnProceso = await todosQuery.CountAsync(t => t.Estado == "En Proceso");
                ViewBag.Resueltos = await todosQuery.CountAsync(t => t.Estado == "Resuelto");
            }
            catch
            {
                ViewBag.TotalTickets = 0;
                ViewBag.Pendientes = 0;
                ViewBag.EnProceso = 0;
                ViewBag.Resueltos = 0;
            }

            return View(tickets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en vista de soporte.");
            ViewBag.TotalTickets = 0;
            ViewBag.Pendientes = 0;
            ViewBag.EnProceso = 0;
            ViewBag.Resueltos = 0;
            return View(new List<SoporteTicket>());
        }
    }

    /// <summary>
    /// Formulario para crear un nuevo ticket de soporte.
    /// </summary>
    [HttpGet]
    public IActionResult Nuevo()
    {
        try
        {
            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cargar la vista de nuevo ticket de soporte.");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// Procesar la creación de un nuevo ticket de soporte.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Nuevo(string titulo, string mensaje, IFormFile? imagen)
    {
        if (string.IsNullOrWhiteSpace(titulo) || string.IsNullOrWhiteSpace(mensaje))
        {
            ViewBag.ErrorMessage = "Por favor complete el título y el mensaje del reporte de soporte.";
            return View();
        }

        try
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            string? imagenUrl = null;
            if (imagen != null && imagen.Length > 0)
            {
                try
                {
                    var webRoot = _environment.WebRootPath;
                    if (string.IsNullOrEmpty(webRoot))
                    {
                        webRoot = Path.Combine(_environment.ContentRootPath, "wwwroot");
                    }

                    var uploadsFolder = Path.Combine(webRoot, "uploads", "soporte");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(imagen.FileName)}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await imagen.CopyToAsync(fileStream);
                    }

                    imagenUrl = $"/uploads/soporte/{uniqueFileName}";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al guardar la imagen de soporte.");
                }
            }

            var ticket = new SoporteTicket
            {
                UsuarioId = user.Id,
                NombreUsuario = user.NombreCompleto ?? user.UserName ?? "Usuario",
                EmailUsuario = user.Email ?? "",
                Titulo = titulo.Trim(),
                Mensaje = mensaje.Trim(),
                ImagenUrl = imagenUrl,
                FechaCreacion = DateTime.UtcNow,
                Estado = "Pendiente"
            };

            await _unitOfWork.SoporteTickets.AddAsync(ticket);
            await _unitOfWork.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar el ticket de soporte en la base de datos.");
            string det = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
            ViewBag.ErrorMessage = $"Error (Pásame captura de esto): {det}";
            return View();
        }
    }

    /// <summary>
    /// Ver detalle de un ticket de soporte.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Detalle(int id, bool success = false)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");

        if (success)
        {
            ViewBag.SuccessMessage = "Respuesta y estado actualizados correctamente.";
        }

        var ticket = await _unitOfWork.SoporteTickets.Query()
            .Include(t => t.Usuario)
            .FirstOrDefaultAsync(t => t.SoporteTicketId == id);

        if (ticket == null) return NotFound();

        bool esMaster = User.IsInRole("Master");
        if (!esMaster && ticket.UsuarioId != user.Id)
        {
            return Forbid();
        }

        ViewData["EsMaster"] = esMaster;
        return View(ticket);
    }

    /// <summary>
    /// Responder o actualizar estado de un ticket de soporte (Exclusivo para Master).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Master")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Responder(int id, string respuesta, string estado)
    {
        var ticket = await _unitOfWork.SoporteTickets.GetByIdAsync(id);
        if (ticket == null) return NotFound();

        ticket.Estado = string.IsNullOrWhiteSpace(estado) ? ticket.Estado : estado;
        if (!string.IsNullOrWhiteSpace(respuesta))
        {
            ticket.RespuestaMaster = respuesta.Trim();
            ticket.FechaRespuesta = DateTime.UtcNow;
        }

        _unitOfWork.SoporteTickets.Update(ticket);
        await _unitOfWork.SaveChangesAsync();

        return RedirectToAction(nameof(Detalle), new { id = id, success = true });
    }

    /// <summary>
    /// Eliminar ticket de soporte (Exclusivo para Master).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Master")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(int id)
    {
        var ticket = await _unitOfWork.SoporteTickets.GetByIdAsync(id);
        if (ticket != null)
        {
            _unitOfWork.SoporteTickets.Remove(ticket);
            await _unitOfWork.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { success = true });
        }
        return RedirectToAction(nameof(Index));
    }
}
