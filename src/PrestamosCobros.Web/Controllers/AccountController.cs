using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PrestamosCobros.BLL.DTOs;
using PrestamosCobros.DAL.Entities;
using PrestamosCobros.Infrastructure.Audit;
using Microsoft.AspNetCore.RateLimiting;

namespace PrestamosCobros.Web.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<Usuario> _signInManager;
    private readonly UserManager<Usuario> _userManager;
    private readonly IAuditoriaService _auditoria;

    public AccountController(
        SignInManager<Usuario> signInManager,
        UserManager<Usuario> userManager,
        IAuditoriaService auditoria)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _auditoria = auditoria;
    }

    [HttpGet]
    [EnableRateLimiting("LoginPolicy")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("LoginPolicy")]
    public async Task<IActionResult> Login(LoginDto model, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return View(model);

        var identifier = (model.Email ?? "").Trim();
        var password = (model.Password ?? "").Trim();

        var user = await _userManager.FindByEmailAsync(identifier);
        if (user == null)
        {
            user = await _userManager.FindByNameAsync(identifier);
        }

        if (user == null)
        {
            var upperId = identifier.ToUpperInvariant();
            user = await _userManager.Users.FirstOrDefaultAsync(u =>
                (u.NormalizedUserName != null && u.NormalizedUserName == upperId) ||
                (u.NormalizedEmail != null && u.NormalizedEmail == upperId) ||
                (u.UserName != null && u.UserName.ToLower() == identifier.ToLower()) ||
                (u.Email != null && u.Email.ToLower() == identifier.ToLower()));
        }

        if (user == null || !user.Activo)
        {
            ModelState.AddModelError("", "Credenciales inv�lidas.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            user.UserName!, password, false, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _auditoria.RegistrarAsync(user.Id, "Inicio de sesión", $"Login exitoso desde {ip}", ip);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            var roles = await _userManager.GetRolesAsync(user);
            return roles.Contains("Admin")
                ? RedirectToAction("Index", "Home")
                : RedirectToAction("Index", "Clientes");
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError("", "Cuenta bloqueada temporalmente. Intente en 15 minutos.");
            return View(model);
        }

        ModelState.AddModelError("", "Credenciales inválidas.");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user != null)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _auditoria.RegistrarAsync(user.Id, "Cierre de sesión", null, ip);
        }

        await _signInManager.SignOutAsync();
        return RedirectToAction("Login");
    }

    public IActionResult AccessDenied() => View();
}
