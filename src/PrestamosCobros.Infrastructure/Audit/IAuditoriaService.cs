namespace PrestamosCobros.Infrastructure.Audit;

public interface IAuditoriaService
{
    Task RegistrarAsync(int usuarioId, string accion, string? detalle = null, string? ip = null);
}
