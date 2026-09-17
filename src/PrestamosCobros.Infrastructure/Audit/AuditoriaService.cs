using PrestamosCobros.DAL.Entities;
using PrestamosCobros.DAL.Repositories;

namespace PrestamosCobros.Infrastructure.Audit;

public class AuditoriaService : IAuditoriaService
{
    private readonly IUnitOfWork _unitOfWork;

    public AuditoriaService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task RegistrarAsync(int usuarioId, string accion, string? detalle = null, string? ip = null)
    {
        var entrada = new Bitacora
        {
            UsuarioId = usuarioId,
            Accion = accion,
            Detalle = detalle,
            Fecha = DateTime.UtcNow,
            DireccionIP = ip
        };

        await _unitOfWork.Bitacoras.AddAsync(entrada);
        await _unitOfWork.SaveChangesAsync();
    }
}
