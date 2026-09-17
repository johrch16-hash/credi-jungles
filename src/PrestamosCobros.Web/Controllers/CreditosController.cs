using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrestamosCobros.BLL.DTOs;
using PrestamosCobros.BLL.Interfaces;

namespace PrestamosCobros.Web.Controllers;

[Authorize(Roles = "Master")]
[Route("api/[controller]")]
[ApiController]
public class CreditosController : ControllerBase
{
    private readonly IPrestamoService _prestamoService;

    public CreditosController(IPrestamoService prestamoService)
    {
        _prestamoService = prestamoService;
    }

    [HttpPut("EditarCompleto")]
    public async Task<IActionResult> EditarCompleto([FromBody] CreditoEditDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var resultado = await _prestamoService.EditarCreditoCompletoAsync(dto);
            if (resultado.Exito)
            {
                return Ok(new { mensaje = resultado.Mensaje });
            }
            return BadRequest(new { mensaje = resultado.Mensaje });
        }
        catch (ApplicationException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
        catch (Exception ex)
        {
            // Retornamos 500 pero encapsulamos el mensaje para no filtrar trazas enteras en prod
            return StatusCode(500, new { mensaje = "Ocurrió un error inesperado al editar el crédito.", detalle = ex.Message });
        }
    }
}
