using PrestamosCobros.DAL.Context;
using PrestamosCobros.DAL.Entities;

namespace PrestamosCobros.DAL.Repositories;

public interface IUnitOfWork : IDisposable
{
    IRepository<Cliente> Clientes { get; }
    IRepository<Bitacora> Bitacoras { get; }
    IRepository<Prestamo> Prestamos { get; }
    IRepository<Cuota> Cuotas { get; }
    IRepository<Pago> Pagos { get; }
    IRepository<Notificacion> Notificaciones { get; }
    IRepository<Gasto> Gastos { get; }
    IRepository<Preferencias> Preferencias { get; }
    IRepository<ConfiguracionWhatsApp> ConfiguracionesWhatsApp { get; }
    IRepository<MensajeRecibido> MensajesRecibidos { get; }
    IRepository<SoporteTicket> SoporteTickets { get; }
    Task<int> SaveChangesAsync();
}

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private IRepository<Cliente>? _clientes;
    private IRepository<Bitacora>? _bitacoras;
    private IRepository<Prestamo>? _prestamos;
    private IRepository<Cuota>? _cuotas;
    private IRepository<Pago>? _pagos;
    private IRepository<Notificacion>? _notificaciones;
    private IRepository<Gasto>? _gastos;
    private IRepository<Preferencias>? _preferencias;
    private IRepository<ConfiguracionWhatsApp>? _configuracionesWhatsApp;
    private IRepository<MensajeRecibido>? _mensajesRecibidos;
    private IRepository<SoporteTicket>? _soporteTickets;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public IRepository<Cliente> Clientes =>
        _clientes ??= new Repository<Cliente>(_context);

    public IRepository<Bitacora> Bitacoras =>
        _bitacoras ??= new Repository<Bitacora>(_context);

    public IRepository<Prestamo> Prestamos =>
        _prestamos ??= new Repository<Prestamo>(_context);

    public IRepository<Cuota> Cuotas =>
        _cuotas ??= new Repository<Cuota>(_context);

    public IRepository<Pago> Pagos =>
        _pagos ??= new Repository<Pago>(_context);

    public IRepository<Notificacion> Notificaciones =>
        _notificaciones ??= new Repository<Notificacion>(_context);

    public IRepository<Gasto> Gastos =>
        _gastos ??= new Repository<Gasto>(_context);

    public IRepository<Preferencias> Preferencias =>
        _preferencias ??= new Repository<Preferencias>(_context);

    public IRepository<ConfiguracionWhatsApp> ConfiguracionesWhatsApp =>
        _configuracionesWhatsApp ??= new Repository<ConfiguracionWhatsApp>(_context);

    public IRepository<MensajeRecibido> MensajesRecibidos =>
        _mensajesRecibidos ??= new Repository<MensajeRecibido>(_context);

    public IRepository<SoporteTicket> SoporteTickets =>
        _soporteTickets ??= new Repository<SoporteTicket>(_context);

    public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();

    public void Dispose() => _context.Dispose();
}
