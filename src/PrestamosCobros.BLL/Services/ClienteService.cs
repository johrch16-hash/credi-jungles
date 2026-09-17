using Microsoft.EntityFrameworkCore;
using PrestamosCobros.BLL.DTOs;
using PrestamosCobros.BLL.Interfaces;
using PrestamosCobros.DAL.Entities;
using PrestamosCobros.DAL.Repositories;
using PrestamosCobros.Infrastructure.Security;

namespace PrestamosCobros.BLL.Services;

public class ClienteService : IClienteService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICifradoService _cifrado;

    public ClienteService(IUnitOfWork unitOfWork, ICifradoService cifrado)
    {
        _unitOfWork = unitOfWork;
        _cifrado = cifrado;
    }

    public async Task<List<ClienteListDto>> ObtenerTodosAsync(string? busqueda = null, string? estado = null)
    {
        var query = _unitOfWork.Clientes.Query().Where(c => c.Activo);

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var term = busqueda.ToLower();
            query = query.Where(c => c.NombreCompleto.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(estado))
            query = query.Where(c => c.Estado == estado);

        return await query.OrderBy(c => c.NombreCompleto)
            .Select(c => new ClienteListDto
            {
                ClienteId = c.ClienteId,
                NombreCompleto = c.NombreCompleto,
                Telefono = c.Telefono,
                NivelAtrasos = c.NivelAtrasos,
                Estado = c.Estado
            })
            .ToListAsync();
    }

    public async Task<ClienteDetalleDto?> ObtenerPorIdAsync(int id)
    {
        var cliente = await _unitOfWork.Clientes.Query()
            .Include(c => c.Prestamos)
            .FirstOrDefaultAsync(c => c.ClienteId == id);
            
        if (cliente == null || !cliente.Activo) return null;

        return new ClienteDetalleDto
        {
            ClienteId = cliente.ClienteId,
            NombreCompleto = cliente.NombreCompleto,
            Telefono = cliente.Telefono,
            Email = cliente.Email,
            CedulaEnmascarada = EnmascararCedula(cliente.CedulaCifrada),
            Direccion = cliente.Direccion,
            LugarTrabajo = cliente.LugarTrabajo,
            NivelAtrasos = cliente.NivelAtrasos,
            Estado = cliente.Estado,
            FechaCreacion = cliente.FechaCreacion,
            Prestamos = cliente.Prestamos.Select(p => new PrestamoListDto
            {
                PrestamoId = p.PrestamoId,
                MontoPrestado = p.MontoPrestado,
                SaldoPendiente = p.SaldoPendiente,
                Estado = p.Estado,
                NombreAlias = p.NombreAlias,
                Liquidado = p.Estado == "Liquidado" || p.SaldoPendiente <= 0,
                PorcentajePagado = p.MontoPrestado > 0 ? (int)((p.MontoPrestado - p.SaldoPendiente) / p.MontoPrestado * 100) : 0
            }).ToList(),
            CuotasPendientes = await _unitOfWork.Cuotas.Query()
                .Include(c => c.Prestamo)
                .Include(c => c.Pago)
                .Where(c => c.Prestamo.ClienteId == id && c.Estado != "Pagado" && c.Prestamo.Estado == "Activo")
                .OrderBy(c => c.FechaVencimiento)
                .Select(c => new CuotaDto
                {
                    CuotaId = c.CuotaId,
                    NumeroCuota = c.NumeroCuota,
                    Monto = c.Monto,
                    FechaVencimiento = c.FechaVencimiento,
                    Estado = c.Estado,
                    FechaPago = c.FechaPago,
                    MontoPagado = c.Pago != null ? c.Pago.MontoPagado : null,
                    PrestamoId = c.PrestamoId,
                    PrestamoAlias = c.Prestamo.NombreAlias
                })
                .ToListAsync()
        };
    }

    public async Task<ClienteEditDto?> ObtenerParaEditarAsync(int id)
    {
        var cliente = await _unitOfWork.Clientes.GetByIdAsync(id);
        if (cliente == null || !cliente.Activo) return null;

        string? cedulaDescifrada = null;
        if (!string.IsNullOrEmpty(cliente.CedulaCifrada))
        {
            try
            {
                cedulaDescifrada = _cifrado.Decrypt(cliente.CedulaCifrada);
            }
            catch
            {
                cedulaDescifrada = null;
            }
        }

        return new ClienteEditDto
        {
            ClienteId = cliente.ClienteId,
            NombreCompleto = cliente.NombreCompleto,
            Telefono = cliente.Telefono,
            Email = cliente.Email,
            Cedula = cedulaDescifrada,
            Direccion = cliente.Direccion,
            LugarTrabajo = cliente.LugarTrabajo
        };
    }

    public async Task<(bool Exito, string Mensaje)> CrearAsync(ClienteCreateDto dto)
    {
        dto.Telefono = NormalizarTelefono(dto.Telefono);

        if (await ExisteTelefonoAsync(dto.Telefono))
            return (false, "Ya existe un cliente con ese teléfono.");

        if (!string.IsNullOrEmpty(dto.Cedula) && await ExisteCedulaAsync(dto.Cedula))
            return (false, "Ya existe un cliente con esa cédula.");

        var cliente = new Cliente
        {
            NombreCompleto = dto.NombreCompleto,
            Telefono = dto.Telefono,
            Email = dto.Email,
            CedulaCifrada = !string.IsNullOrEmpty(dto.Cedula) ? _cifrado.Encrypt(dto.Cedula) : null,
            Direccion = dto.Direccion,
            LugarTrabajo = dto.LugarTrabajo
        };

        await _unitOfWork.Clientes.AddAsync(cliente);

        try
        {
            // Control absoluto de duplicados (incluyendo inactivos) para evitar HTTP ERROR 500
            var cedulaCifradaParaCheck = !string.IsNullOrEmpty(dto.Cedula) ? _cifrado.Encrypt(dto.Cedula) : null;
            var existeAbsoluto = await _unitOfWork.Clientes.Query()
                .AnyAsync(c => c.Telefono == dto.Telefono || (cedulaCifradaParaCheck != null && c.CedulaCifrada == cedulaCifradaParaCheck));

            if (existeAbsoluto)
            {
                throw new ApplicationException("El número de teléfono o la cédula ya se encuentran registrados en el sistema.");
            }

            await _unitOfWork.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (
            ex.InnerException?.Message.Contains("IX_Clientes_Telefono", StringComparison.OrdinalIgnoreCase) == true ||
            ex.InnerException?.Message.Contains("IX_Clientes_Cedula", StringComparison.OrdinalIgnoreCase) == true ||
            ex.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new ApplicationException("El número de teléfono o la cédula ya se encuentran registrados en el sistema.");
        }

        return (true, "Cliente creado exitosamente.");
    }

    public async Task<(bool Exito, string Mensaje)> EditarAsync(ClienteEditDto dto)
    {
        var cliente = await _unitOfWork.Clientes.GetByIdAsync(dto.ClienteId);
        if (cliente == null || !cliente.Activo)
            return (false, "Cliente no encontrado.");

        dto.Telefono = NormalizarTelefono(dto.Telefono);

        if (await ExisteTelefonoAsync(dto.Telefono, dto.ClienteId))
            return (false, "Ya existe otro cliente con ese teléfono.");

        if (!string.IsNullOrEmpty(dto.Cedula) && await ExisteCedulaAsync(dto.Cedula, dto.ClienteId))
            return (false, "Ya existe otro cliente con esa cédula.");

        cliente.NombreCompleto = dto.NombreCompleto;
        cliente.Telefono = dto.Telefono;
        cliente.Email = dto.Email;
        cliente.CedulaCifrada = !string.IsNullOrEmpty(dto.Cedula) ? _cifrado.Encrypt(dto.Cedula) : null;
        cliente.Direccion = dto.Direccion;
        cliente.LugarTrabajo = dto.LugarTrabajo;

        _unitOfWork.Clientes.Update(cliente);

        try
        {
            await _unitOfWork.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (
            ex.InnerException?.Message.Contains("IX_Clientes_Telefono", StringComparison.OrdinalIgnoreCase) == true ||
            ex.InnerException?.Message.Contains("IX_Clientes_Cedula", StringComparison.OrdinalIgnoreCase) == true ||
            ex.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true)
        {
            return (false, "El número de teléfono o la cédula ya se encuentran registrados en el sistema.");
        }

        return (true, "Cliente actualizado exitosamente.");
    }

    public async Task<(bool Exito, string Mensaje)> DesactivarAsync(int id)
    {
        var cliente = await _unitOfWork.Clientes.GetByIdAsync(id);
        if (cliente == null)
            return (false, "Cliente no encontrado.");

        cliente.Activo = false;
        _unitOfWork.Clientes.Update(cliente);

        var recordatorios = await _unitOfWork.Notificaciones.Query()
            .Where(n => n.ClienteId == id && n.Tipo == "Recordatorio" && n.Estado == "Pendiente")
            .ToListAsync();

        foreach (var rec in recordatorios)
        {
            _unitOfWork.Notificaciones.Remove(rec);
        }

        await _unitOfWork.SaveChangesAsync();
        return (true, "Cliente desactivado exitosamente.");
    }

    public async Task<bool> ExisteTelefonoAsync(string telefono, int? exceptoId = null)
    {
        var telNormalizado = NormalizarTelefono(telefono);
        var query = _unitOfWork.Clientes.Query()
            .Where(c => (c.Telefono == telNormalizado || c.Telefono == telefono) && c.Activo);

        if (exceptoId.HasValue)
            query = query.Where(c => c.ClienteId != exceptoId.Value);

        return await query.AnyAsync();
    }

    public async Task<bool> ExisteCedulaAsync(string cedula, int? exceptoId = null)
    {
        var cedulaCifrada = _cifrado.Encrypt(cedula);
        var query = _unitOfWork.Clientes.Query()
            .Where(c => c.CedulaCifrada == cedulaCifrada && c.Activo);

        if (exceptoId.HasValue)
            query = query.Where(c => c.ClienteId != exceptoId.Value);

        return await query.AnyAsync();
    }

    private string? EnmascararCedula(string? cedulaCifrada)
    {
        if (string.IsNullOrEmpty(cedulaCifrada)) return null;
        try
        {
            var cedula = _cifrado.Decrypt(cedulaCifrada);
            if (cedula.Length <= 4) return "****";
            return new string('*', cedula.Length - 4) + cedula[^4..];
        }
        catch
        {
            return "***";
        }
    }

    private string NormalizarTelefono(string telefono)
    {
        if (string.IsNullOrWhiteSpace(telefono)) return telefono;
        
        var digitos = new string(telefono.Where(char.IsDigit).ToArray());
        
        if (digitos.Length == 8)
        {
            // Formatear como +506 8888-8888 o similar
            return $"+506 {telefono.Trim()}";
        }
        
        if (digitos.Length == 11 && digitos.StartsWith("506"))
        {
            var sinCodigo = digitos.Substring(3);
            return $"+506 {sinCodigo.Substring(0, 4)}-{sinCodigo.Substring(4)}";
        }
        
        return telefono.Trim();
    }
}
