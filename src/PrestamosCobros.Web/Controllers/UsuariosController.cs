using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrestamosCobros.BLL.DTOs;
using PrestamosCobros.DAL.Entities;
using PrestamosCobros.Infrastructure.Audit;

namespace PrestamosCobros.Web.Controllers;

[Authorize(Roles = "Admin,Master")]
public class UsuariosController : Controller
{
    private readonly UserManager<Usuario> _userManager;
    private readonly RoleManager<Rol> _roleManager;
    private readonly IAuditoriaService _auditoria;

    public UsuariosController(
        UserManager<Usuario> userManager,
        RoleManager<Rol> roleManager,
        IAuditoriaService auditoria)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _auditoria = auditoria;
    }

    public async Task<IActionResult> Index()
    {
        var users = await _userManager.Users.ToListAsync();
        var lista = new List<UsuarioListDto>();

        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            if (roles.Contains("Master")) continue; // Ocultar usuarios Master

            lista.Add(new UsuarioListDto
            {
                Id = u.Id,
                NombreCompleto = u.NombreCompleto,
                Email = u.Email!,
                Telefono = u.Telefono,
                Rol = roles.FirstOrDefault() ?? "Sin rol",
                Activo = u.Activo
            });
        }

        return View(lista);
    }

    public IActionResult Crear() => View(new UsuarioCreateDto());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(UsuarioCreateDto model)
    {
        if (!ModelState.IsValid) return View(model);

        var existente = await _userManager.FindByEmailAsync(model.Email);
        if (existente != null)
        {
            ModelState.AddModelError("Email", "Ya existe un usuario con ese email.");
            return View(model);
        }

        var usuario = new Usuario
        {
            UserName = model.Email,
            Email = model.Email,
            NombreCompleto = model.NombreCompleto,
            Telefono = model.Telefono,
            EmailConfirmed = true,
            Activo = true,
            PermitirGestionClientes = model.PermitirGestionClientes,
            PermitirGestionPrestamos = model.PermitirGestionPrestamos,
            PermitirRegistroGastos = model.PermitirRegistroGastos,
            PermitirRegistroPagos = model.PermitirRegistroPagos,
            PermitirEnviarNotificaciones = model.PermitirEnviarNotificaciones,
            PermitirVerReportes = model.PermitirVerReportes,
            PermitirVerAuditoria = model.PermitirVerAuditoria,
            PermitirGestionPersonal = model.PermitirGestionPersonal,
            PermitirConfigWhatsApp = model.PermitirConfigWhatsApp,
            PermitirConfigCorreo = model.PermitirConfigCorreo,
            PermitirPapelera = model.PermitirPapelera
        };

        var result = await _userManager.CreateAsync(usuario, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);
            return View(model);
        }

        await _userManager.AddToRoleAsync(usuario, model.Rol);

        var admin = await _userManager.GetUserAsync(User);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditoria.RegistrarAsync(admin!.Id, "Crear usuario",
            $"Usuario '{model.NombreCompleto}' ({model.Email}) creado con rol {model.Rol}", ip);

        TempData["Mensaje"] = "Usuario creado exitosamente.";
        return RedirectToAction("Index");
    }

    public async Task<IActionResult> Editar(int id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null) return NotFound();

        var roles = await _userManager.GetRolesAsync(user);
        var model = new UsuarioEditDto
        {
            Id = user.Id,
            NombreCompleto = user.NombreCompleto,
            Telefono = user.Telefono,
            Rol = roles.FirstOrDefault() ?? "Asistente",
            Activo = user.Activo,
            PermitirGestionClientes = user.PermitirGestionClientes,
            PermitirGestionPrestamos = user.PermitirGestionPrestamos,
            PermitirRegistroGastos = user.PermitirRegistroGastos,
            PermitirRegistroPagos = user.PermitirRegistroPagos,
            PermitirEnviarNotificaciones = user.PermitirEnviarNotificaciones,
            PermitirVerReportes = user.PermitirVerReportes,
            PermitirVerAuditoria = user.PermitirVerAuditoria,
            PermitirGestionPersonal = user.PermitirGestionPersonal,
            PermitirConfigWhatsApp = user.PermitirConfigWhatsApp,
            PermitirConfigCorreo = user.PermitirConfigCorreo,
            PermitirPapelera = user.PermitirPapelera
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(UsuarioEditDto model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByIdAsync(model.Id.ToString());
        if (user == null) return NotFound();

        user.NombreCompleto = model.NombreCompleto;
        user.Telefono = model.Telefono;
        user.Activo = model.Activo;
        user.PermitirGestionClientes = model.PermitirGestionClientes;
        user.PermitirGestionPrestamos = model.PermitirGestionPrestamos;
        user.PermitirRegistroGastos = model.PermitirRegistroGastos;
        user.PermitirRegistroPagos = model.PermitirRegistroPagos;
        user.PermitirEnviarNotificaciones = model.PermitirEnviarNotificaciones;
        user.PermitirVerReportes = model.PermitirVerReportes;
        user.PermitirVerAuditoria = model.PermitirVerAuditoria;
        user.PermitirGestionPersonal = model.PermitirGestionPersonal;
        user.PermitirConfigWhatsApp = model.PermitirConfigWhatsApp;
        user.PermitirConfigCorreo = model.PermitirConfigCorreo;
        user.PermitirPapelera = model.PermitirPapelera;

        await _userManager.UpdateAsync(user);

        // Actualizar rol
        var rolesActuales = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, rolesActuales);
        await _userManager.AddToRoleAsync(user, model.Rol);

        var admin = await _userManager.GetUserAsync(User);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditoria.RegistrarAsync(admin!.Id, "Editar usuario",
            $"Usuario ID={model.Id} actualizado: Rol={model.Rol}, Activo={model.Activo}", ip);

        TempData["Mensaje"] = "Usuario actualizado exitosamente.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Desactivar(int id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null) return NotFound();

        user.Activo = false;
        await _userManager.UpdateAsync(user);

        var admin = await _userManager.GetUserAsync(User);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditoria.RegistrarAsync(admin!.Id, "Desactivar usuario",
            $"Usuario ID={id} '{user.NombreCompleto}' desactivado", ip);

        TempData["Mensaje"] = "Usuario desactivado.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(int id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null) return NotFound();

        var admin = await _userManager.GetUserAsync(User);
        if (user.Id == admin!.Id)
        {
            TempData["Error"] = "No puedes eliminarte a ti mismo.";
            return RedirectToAction("Index");
        }

        try
        {
            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
            {
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                await _auditoria.RegistrarAsync(admin.Id, "Eliminar personal", $"Usuario '{user.NombreCompleto}' eliminado permanentemente", ip);
                TempData["Mensaje"] = "Personal eliminado permanentemente.";
            }
            else
            {
                TempData["Error"] = "Error al eliminar el personal.";
            }
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = "El personal tiene registros asociados (pagos, bitácora) y no puede ser eliminado. Se recomienda desactivarlo.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Error inesperado al eliminar el personal: " + ex.Message;
        }

        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null) return NotFound();

        user.Activo = !user.Activo;
        await _userManager.UpdateAsync(user);

        var admin = await _userManager.GetUserAsync(User);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditoria.RegistrarAsync(admin!.Id, "Cambio de Estado",
            $"Usuario ID={id} ({user.NombreCompleto}) -> {(user.Activo ? "Activado" : "Desactivado")}", ip);

        return Ok();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditarAjax([FromForm] UsuarioEditDto model)
    {
        if (!ModelState.IsValid) return BadRequest();

        var user = await _userManager.FindByIdAsync(model.Id.ToString());
        if (user == null) return NotFound();

        user.NombreCompleto = model.NombreCompleto;
        user.Telefono = model.Telefono;
        user.Activo = model.Activo;
        user.PermitirGestionClientes = model.PermitirGestionClientes;
        user.PermitirGestionPrestamos = model.PermitirGestionPrestamos;
        user.PermitirRegistroGastos = model.PermitirRegistroGastos;
        user.PermitirRegistroPagos = model.PermitirRegistroPagos;
        user.PermitirEnviarNotificaciones = model.PermitirEnviarNotificaciones;
        user.PermitirVerReportes = model.PermitirVerReportes;
        user.PermitirVerAuditoria = model.PermitirVerAuditoria;
        user.PermitirGestionPersonal = model.PermitirGestionPersonal;
        user.PermitirConfigWhatsApp = model.PermitirConfigWhatsApp;
        user.PermitirConfigCorreo = model.PermitirConfigCorreo;
        user.PermitirPapelera = model.PermitirPapelera;

        await _userManager.UpdateAsync(user);

        var rolesActuales = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, rolesActuales);
        await _userManager.AddToRoleAsync(user, model.Rol);

        var admin = await _userManager.GetUserAsync(User);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditoria.RegistrarAsync(admin!.Id, "Edición Rápida",
            $"Personal ID={model.Id} actualizado vía Panel Lateral.", ip);

        return Ok();
    }
}

