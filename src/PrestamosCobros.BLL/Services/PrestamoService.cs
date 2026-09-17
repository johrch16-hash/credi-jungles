using Microsoft.EntityFrameworkCore;
using PrestamosCobros.BLL.DTOs;
using PrestamosCobros.BLL.Interfaces;
using PrestamosCobros.DAL.Entities;
using PrestamosCobros.DAL.Repositories;

namespace PrestamosCobros.BLL.Services;

public class PrestamoService : IPrestamoService
{
    private readonly IUnitOfWork _unitOfWork;

    public PrestamoService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<PrestamoListDto>> ObtenerTodosAsync(string? estado = null, string? busqueda = null)
    {
        var query = _unitOfWork.Prestamos.Query()
            .Include(p => p.Cliente)
            .Include(p => p.Cuotas)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(estado))
            query = query.Where(p => p.Estado == estado);

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var term = busqueda.ToLower();
            query = query.Where(p =>
                p.Cliente.NombreCompleto.ToLower().Contains(term) ||
                (p.NombreAlias != null && p.NombreAlias.ToLower().Contains(term)));
        }

        return await query.OrderByDescending(p => p.FechaCreacion)
            .Select(p => new PrestamoListDto
            {
                PrestamoId = p.PrestamoId,
                ClienteId = p.ClienteId,
                ClienteNombre = p.Cliente.NombreCompleto,
                ClienteTelefono = p.Cliente.Telefono,
                NombreAlias = p.NombreAlias,
                MontoPrestado = p.MontoPrestado,
                MontoTotal = p.MontoTotal,
                SaldoPendiente = p.SaldoPendiente,
                Estado = p.Estado,
                TipoCredito = p.TipoCredito,
                FechaInicio = p.FechaInicio,
                CuotasPagadas = p.Cuotas.Count(c => c.Estado == "Pagado"),
                CuotasTotal = p.Cuotas.Count,
                PorcentajePagado = p.Cuotas.Count == 0 ? 0 : (int)((double)p.Cuotas.Count(c => c.Estado == "Pagado") / p.Cuotas.Count * 100),
                CuotasAtrasadas = p.Cuotas.Count(c => c.Estado == "Atrasado"),
                FechaVencimientoProx = p.Cuotas.Where(c => c.Estado == "Pendiente" || c.Estado == "Atrasado").OrderBy(c => c.FechaVencimiento).Select(c => (DateTime?)c.FechaVencimiento).FirstOrDefault()
            })
            .ToListAsync();
    }

    public async Task<List<PrestamoListDto>> ObtenerPorClienteAsync(int clienteId)
    {
        return await _unitOfWork.Prestamos.Query()
            .Include(p => p.Cliente)
            .Include(p => p.Cuotas)
            .Where(p => p.ClienteId == clienteId)
            .OrderByDescending(p => p.FechaCreacion)
            .Select(p => new PrestamoListDto
            {
                PrestamoId = p.PrestamoId,
                ClienteId = p.ClienteId,
                ClienteNombre = p.Cliente.NombreCompleto,
                ClienteTelefono = p.Cliente.Telefono,
                NombreAlias = p.NombreAlias,
                MontoPrestado = p.MontoPrestado,
                MontoTotal = p.MontoTotal,
                SaldoPendiente = p.SaldoPendiente,
                Estado = p.Estado,
                TipoCredito = p.TipoCredito,
                FechaInicio = p.FechaInicio,
                CuotasPagadas = p.Cuotas.Count(c => c.Estado == "Pagado"),
                CuotasTotal = p.Cuotas.Count,
                PorcentajePagado = p.Cuotas.Count == 0 ? 0 : (int)((double)p.Cuotas.Count(c => c.Estado == "Pagado") / p.Cuotas.Count * 100),
                CuotasAtrasadas = p.Cuotas.Count(c => c.Estado == "Atrasado"),
                FechaVencimientoProx = p.Cuotas.Where(c => c.Estado == "Pendiente" || c.Estado == "Atrasado").OrderBy(c => c.FechaVencimiento).Select(c => (DateTime?)c.FechaVencimiento).FirstOrDefault()
            })
            .ToListAsync();
    }

    public async Task<PrestamoDetalleDto?> ObtenerPorIdAsync(int id)
    {
        var prestamo = await _unitOfWork.Prestamos.Query()
            .Include(p => p.Cliente)
            .Include(p => p.Cuotas)
                .ThenInclude(c => c.Pago)
            .FirstOrDefaultAsync(p => p.PrestamoId == id);

        if (prestamo == null) return null;

        return new PrestamoDetalleDto
        {
            PrestamoId = prestamo.PrestamoId,
            ClienteId = prestamo.ClienteId,
            ClienteNombre = prestamo.Cliente.NombreCompleto,
            ClienteTelefono = prestamo.Cliente.Telefono,
            NombreAlias = prestamo.NombreAlias,
            MontoPrestado = prestamo.MontoPrestado,
            PorcentajeInteres = prestamo.PorcentajeInteres,
            MontoTotal = prestamo.MontoTotal,
            SaldoPendiente = prestamo.SaldoPendiente,
            FechaInicio = prestamo.FechaInicio,
            PeriodicidadDias = prestamo.PeriodicidadDias,
            NumeroSinpe = prestamo.NumeroSinpe,
            CuentasBancarias = prestamo.CuentasBancarias,
            Estado = prestamo.Estado,
            TipoCredito = prestamo.TipoCredito,
            FechaCreacion = prestamo.FechaCreacion,
            Cuotas = prestamo.Cuotas.OrderBy(c => c.NumeroCuota).Select(c => new CuotaDto
            {
                CuotaId = c.CuotaId,
                NumeroCuota = c.NumeroCuota,
                Monto = c.Monto,
                FechaVencimiento = c.FechaVencimiento,
                Estado = c.Estado,
                FechaPago = c.FechaPago,
                MontoPagado = (c.Pago != null && c.Pago.Estado == "Activo") ? c.Pago.MontoPagado : null,
                PrestamoId = prestamo.PrestamoId,
                PrestamoAlias = prestamo.NombreAlias
            }).ToList()
        };
    }

    public Task<PrestamoPreviewDto> GenerarPreviewAsync(PrestamoCreateDto dto)
    {
        if (dto.TipoCredito == "Solo Interés")
        {
            decimal divisor = 1m;
            if (dto.PeriodicidadDias == 7) divisor = 4m;
            else if (dto.PeriodicidadDias == 14 || dto.PeriodicidadDias == 15) divisor = 2m;
            else if (dto.PeriodicidadDias == 1) divisor = 30m;

            var montoInteresSolo = Math.Round((dto.MontoPrestado * (dto.PorcentajeInteres / 100m)) / divisor, 0);
            return Task.FromResult(new PrestamoPreviewDto
            {
                MontoPrestado = dto.MontoPrestado,
                TipoCredito = dto.TipoCredito,
                PorcentajeInteres = dto.PorcentajeInteres,
                MontoInteres = montoInteresSolo,
                MontoTotal = dto.MontoPrestado,
                GastoLey = 0,
                MontoCreditoVisible = dto.MontoPrestado,
                MontoCuota = montoInteresSolo,
                NumeroCuotas = 1,
                Cuotas = new List<CuotaPreviewDto>
                {
                    new CuotaPreviewDto
                    {
                        Numero = 1,
                        Monto = montoInteresSolo,
                        FechaVencimiento = dto.FechaInicio.AddDays(dto.PeriodicidadDias)
                    }
                }
            });
        }

        // Cálculo real de interés anticipado según regla de negocio
        var totalDias = dto.PeriodicidadDias * dto.NumeroCuotas;
        var plazoMeses = Math.Round((decimal)totalDias / 30m, 0, MidpointRounding.AwayFromZero);
        if (plazoMeses < 1) plazoMeses = 1m; // Mínimo 1 mes de interés

        var interesTotal = dto.MontoPrestado * (dto.PorcentajeInteres / 100m) * plazoMeses;
        
        var montoInteres = Math.Round(interesTotal, 0);
        var montoTotal = Math.Round(dto.MontoPrestado + montoInteres, 0); // Monto Base (Capital + Interés unificados)
        var gastoLey = Math.Round(montoTotal * 0.03m, 0);
        var montoCreditoVisible = Math.Round(montoTotal - gastoLey, 0);
        // Redondear la cuota base al millar hacia arriba
        var montoCuota = Math.Ceiling((montoTotal / dto.NumeroCuotas) / 1000m) * 1000m;

        var cuotas = new List<CuotaPreviewDto>();
        decimal acumulado = 0;

        for (int i = 1; i <= dto.NumeroCuotas; i++)
        {
            // Última cuota absorbe diferencia de redondeo
            var monto = (i == dto.NumeroCuotas) ? montoTotal - acumulado : montoCuota;
            acumulado += monto;

            cuotas.Add(new CuotaPreviewDto
            {
                Numero = i,
                Monto = monto,
                FechaVencimiento = dto.FechaInicio.AddDays(dto.PeriodicidadDias * i)
            });
        }

        return Task.FromResult(new PrestamoPreviewDto
        {
            MontoPrestado = dto.MontoPrestado,
            PorcentajeInteres = dto.PorcentajeInteres,
            MontoInteres = montoInteres,
            MontoTotal = montoTotal,
            GastoLey = gastoLey,
            MontoCreditoVisible = montoCreditoVisible,
            MontoCuota = montoCuota,
            NumeroCuotas = dto.TipoCredito == "Solo Interés" ? 1 : dto.NumeroCuotas,
            Cuotas = cuotas
        });
    }

    public async Task<(bool Exito, string Mensaje, int PrestamoId)> CrearAsync(PrestamoCreateDto dto)
    {
        var cliente = await _unitOfWork.Clientes.GetByIdAsync(dto.ClienteId);
        if (cliente == null || !cliente.Activo)
            return (false, "Cliente no encontrado.", 0);

        var montoInteres = 0m;
        var montoTotal = 0m;
        var montoCuota = 0m;

        if (dto.TipoCredito == "Solo Interés")
        {
            decimal divisor = 1m;
            if (dto.PeriodicidadDias == 7) divisor = 4m;
            else if (dto.PeriodicidadDias == 14 || dto.PeriodicidadDias == 15) divisor = 2m;
            else if (dto.PeriodicidadDias == 1) divisor = 30m;

            montoTotal = dto.MontoPrestado;
            montoCuota = Math.Round((dto.MontoPrestado * (dto.PorcentajeInteres / 100m)) / divisor, 0);
        }
        else
        {
            // Cálculo real de interés anticipado según regla de negocio
            var totalDias = dto.PeriodicidadDias * dto.NumeroCuotas;
            var plazoMeses = Math.Round((decimal)totalDias / 30m, 0, MidpointRounding.AwayFromZero);
            if (plazoMeses < 1) plazoMeses = 1m;

            var interesTotal = dto.MontoPrestado * (dto.PorcentajeInteres / 100m) * plazoMeses;
            
            montoInteres = Math.Round(interesTotal, 0);
            montoTotal = Math.Round(dto.MontoPrestado + montoInteres, 0); // Monto Base (Capital + Interés unificados)
            // Redondear la cuota al millar hacia arriba
            montoCuota = Math.Ceiling((montoTotal / dto.NumeroCuotas) / 1000m) * 1000m;
        }

        var cuentasList = new System.Collections.Generic.List<string>();
        if (!string.IsNullOrWhiteSpace(dto.CuentaIban1)) cuentasList.Add(dto.CuentaIban1.Trim());
        if (!string.IsNullOrWhiteSpace(dto.CuentaIban2)) cuentasList.Add(dto.CuentaIban2.Trim());
        if (!string.IsNullOrWhiteSpace(dto.CuentaIban3)) cuentasList.Add(dto.CuentaIban3.Trim());

        var cuentasCombinadas = cuentasList.Count > 0 
            ? string.Join(" | ", cuentasList) 
            : dto.CuentasBancarias;

        var prestamo = new Prestamo
        {
            ClienteId = dto.ClienteId,
            NombreAlias = dto.NombreAlias,
            TipoCredito = dto.TipoCredito ?? "Cuota Completa",
            MontoPrestado = dto.MontoPrestado,
            PorcentajeInteres = dto.PorcentajeInteres,
            MontoTotal = montoTotal,
            SaldoPendiente = montoTotal,
            FechaInicio = DateTime.SpecifyKind(dto.FechaInicio, DateTimeKind.Utc),
            PeriodicidadDias = dto.PeriodicidadDias,
            NumeroSinpe = dto.NumeroSinpe,
            CuentasBancarias = cuentasCombinadas
        };

        // Generar cuotas
        if (dto.TipoCredito == "Solo Interés")
        {
            prestamo.Cuotas.Add(new Cuota
            {
                NumeroCuota = 1,
                Monto = montoCuota,
                FechaVencimiento = DateTime.SpecifyKind(dto.FechaInicio, DateTimeKind.Utc).AddDays(dto.PeriodicidadDias)
            });
        }
        else
        {
            decimal acumulado = 0;
            for (int i = 1; i <= dto.NumeroCuotas; i++)
            {
                var monto = (i == dto.NumeroCuotas) ? montoTotal - acumulado : montoCuota;
                acumulado += monto;

                prestamo.Cuotas.Add(new Cuota
                {
                    NumeroCuota = i,
                    Monto = monto,
                    FechaVencimiento = DateTime.SpecifyKind(dto.FechaInicio, DateTimeKind.Utc).AddDays(dto.PeriodicidadDias * i)
                });
            }
        }

        await _unitOfWork.Prestamos.AddAsync(prestamo);
        await _unitOfWork.SaveChangesAsync();

        return (true, "Préstamo creado exitosamente.", prestamo.PrestamoId);
    }

    public async Task<(bool Exito, string Mensaje)> CancelarAsync(int id)
    {
        var prestamo = await _unitOfWork.Prestamos.GetByIdAsync(id);
        if (prestamo == null)
            return (false, "Préstamo no encontrado.");

        if (prestamo.Estado != "Activo")
            return (false, "Solo se pueden cancelar préstamos activos.");

        prestamo.Estado = "Cancelado";
        _unitOfWork.Prestamos.Update(prestamo);
        await _unitOfWork.SaveChangesAsync();

        return (true, "Préstamo cancelado.");
    }

    public async Task<(bool Exito, string Mensaje)> ReactivarAsync(int id)
    {
        var prestamo = await _unitOfWork.Prestamos.GetByIdAsync(id);
        if (prestamo == null)
            return (false, "Préstamo no encontrado.");

        prestamo.Estado = "Activo";
        _unitOfWork.Prestamos.Update(prestamo);
        await _unitOfWork.SaveChangesAsync();

        return (true, "Préstamo reactivado.");
    }



    public async Task<(bool Exito, string Mensaje, int NuevoPrestamoId)> RenovarAsync(PrestamoRenovarDto dto)
    {
        var prestamoActual = await _unitOfWork.Prestamos.Query()
            .Include(p => p.Cuotas)
            .FirstOrDefaultAsync(p => p.PrestamoId == dto.PrestamoIdActual);
            
        if (prestamoActual == null) return (false, "Préstamo actual no encontrado.", 0);
        
        // 1. Cerrar préstamo actual
        prestamoActual.Estado = "Renovado";
        prestamoActual.SaldoPendiente = 0;
        foreach (var c in prestamoActual.Cuotas.Where(x => x.Estado == "Pendiente" || x.Estado == "Atrasado"))
        {
            c.Estado = "Renovado";
            _unitOfWork.Cuotas.Update(c);
        }
        _unitOfWork.Prestamos.Update(prestamoActual);
        
        // 2. Crear nuevo préstamo
        var nuevoDto = new PrestamoCreateDto
        {
            ClienteId = prestamoActual.ClienteId,
            MontoPrestado = dto.MontoNuevoSolicitado,
            PorcentajeInteres = dto.NuevoInteresPorcentaje,
            FechaInicio = dto.NuevaFechaInicio,
            PeriodicidadDias = dto.NuevoCicloDias,
            NumeroCuotas = dto.NuevasCuotas,
            NumeroSinpe = prestamoActual.NumeroSinpe,
            TipoCredito = dto.TipoCredito
        };
        
        var (exito, mensaje, nuevoId) = await CrearAsync(nuevoDto);
        if (!exito) return (false, mensaje, 0);
        
        // CrearAsync calls SaveChanges
        return (true, "Crédito renovado exitosamente.", nuevoId);
    }

    public async Task<(bool Exito, string Mensaje)> EditarCreditoCompletoAsync(CreditoEditDto dto)
    {
        try
        {
            var prestamo = await _unitOfWork.Prestamos.Query()
                .Include(p => p.Cuotas)
                    .ThenInclude(c => c.Pago)
                .FirstOrDefaultAsync(p => p.PrestamoId == dto.CreditoId);

            if (prestamo == null)
            {
                throw new ApplicationException($"El crédito con ID {dto.CreditoId} no fue encontrado.");
            }

            // Mapeo de periodicidad
            int periodicidadDias = 7;
            if (dto.FrecuenciaPago.Equals("Quincenal", StringComparison.OrdinalIgnoreCase)) periodicidadDias = 15;
            else if (dto.FrecuenciaPago.Equals("Mensual", StringComparison.OrdinalIgnoreCase)) periodicidadDias = 30;

            prestamo.MontoPrestado = dto.MontoPrincipal;
            prestamo.PorcentajeInteres = dto.TasaInternaAnual;
            prestamo.PeriodicidadDias = periodicidadDias;
            prestamo.Estado = dto.Estado;
            prestamo.TipoCredito = dto.TipoCredito;

            // Identificar cuotas ya pagadas para mantenerlas y no corromper la integridad referencial (Pagos)
            var cuotasPagadas = prestamo.Cuotas.Where(c => (c.Pago != null && c.Pago.Estado == "Activo") || c.Estado == "Pagado").ToList();
            var cuotasPendientes = prestamo.Cuotas.Where(c => (c.Pago == null || c.Pago.Estado == "Anulado") && c.Estado != "Pagado").ToList();

            // Calcular montos de lo ya pagado
            decimal totalPagado = cuotasPagadas.Sum(c => c.Monto);

            // Borrar cuotas no pagadas
            foreach (var cp in cuotasPendientes)
            {
                _unitOfWork.Cuotas.Remove(cp);
            }

            // Recalcular
            // Cálculo real de interés anticipado según regla de negocio
            var totalDias = periodicidadDias * dto.Plazo;
            var plazoMeses = Math.Round((decimal)totalDias / 30m, 0, MidpointRounding.AwayFromZero);
            if (plazoMeses < 1) plazoMeses = 1m;

            var interesTotal = dto.MontoPrincipal * (dto.TasaInternaAnual / 100m) * plazoMeses;
            
            var montoInteres = Math.Round(interesTotal, 0);
            var montoTotalNuevo = Math.Round(dto.MontoPrincipal + montoInteres, 0); // Monto Base (Capital + Interés unificados)
            // Redondear la cuota al millar hacia arriba
            var montoCuota = Math.Ceiling((montoTotalNuevo / dto.Plazo) / 1000m) * 1000m;

            // Actualizar totales del préstamo (se suma lo ya pagado si es que las nuevas cuotas representan el saldo, 
            // pero si MontoPrincipal es el total del préstamo, el nuevo total es montoTotalNuevo).
            // Asumiremos que MontoPrincipal es el total del préstamo.
            prestamo.MontoTotal = montoTotalNuevo;
            prestamo.SaldoPendiente = montoTotalNuevo - totalPagado;

            // Generar las nuevas cuotas pendientes
            int cuotasAGenerar = dto.Plazo - cuotasPagadas.Count;
            if (cuotasAGenerar > 0)
            {
                decimal acumulado = 0;
                // Ajustar fechas a partir de la última cuota pagada o de hoy si no hay
                DateTime fechaBase = cuotasPagadas.Any() 
                    ? cuotasPagadas.Max(c => c.FechaVencimiento) 
                    : DateTime.UtcNow;

                int proximoNumero = cuotasPagadas.Count + 1;

                for (int i = 1; i <= cuotasAGenerar; i++)
                {
                    var monto = (i == cuotasAGenerar) ? montoTotalNuevo - totalPagado - acumulado : montoCuota;
                    acumulado += monto;

                    var nuevaCuota = new Cuota
                    {
                        PrestamoId = prestamo.PrestamoId,
                        NumeroCuota = proximoNumero++,
                        Monto = monto,
                        FechaVencimiento = fechaBase.AddDays(periodicidadDias * i),
                        Estado = "Pendiente"
                    };
                    
                    await _unitOfWork.Cuotas.AddAsync(nuevaCuota);
                }
            }

            _unitOfWork.Prestamos.Update(prestamo);
            await _unitOfWork.SaveChangesAsync();

            return (true, "Crédito editado y recalculado exitosamente.");
        }
        catch (Exception ex)
        {
            // El DbContext descarta los cambios si falla SaveChangesAsync, actuando como rollback automático por request.
            throw new ApplicationException("Ocurrió un error al editar el crédito y recalcular las cuotas: " + ex.Message, ex);
        }
    }

    public async Task<(bool Exito, string Mensaje)> EliminarAsync(int id)
    {
        var prestamo = await _unitOfWork.Prestamos.Query()
            .Include(p => p.Cuotas)
            .FirstOrDefaultAsync(p => p.PrestamoId == id);

        if (prestamo == null)
            return (false, "Préstamo no encontrado.");

        try
        {
            var prestamoIds = new List<int> { id };
            
            var cuotas = await _unitOfWork.Cuotas.Query().Where(c => prestamoIds.Contains(c.PrestamoId)).ToListAsync();
            var cuotaIds = cuotas.Select(c => c.CuotaId).ToList();

            if (cuotaIds.Any())
            {
                var pagos = await _unitOfWork.Pagos.Query().Where(p => cuotaIds.Contains(p.CuotaId)).ToListAsync();
                foreach(var p in pagos) _unitOfWork.Pagos.Remove(p);
            }

            foreach(var c in cuotas) _unitOfWork.Cuotas.Remove(c);
            
            var notificaciones = await _unitOfWork.Notificaciones.Query().Where(n => n.PrestamoId == id).ToListAsync();
            foreach(var n in notificaciones) _unitOfWork.Notificaciones.Remove(n);

            _unitOfWork.Prestamos.Remove(prestamo);
            await _unitOfWork.SaveChangesAsync();

            return (true, "Préstamo eliminado exitosamente.");
        }
        catch (Exception ex)
        {
            return (false, $"Error al eliminar préstamo: {ex.Message}");
        }
    }

    public async Task<(bool Exito, string Mensaje)> AbonarCapitalAsync(int id, decimal montoAbono, int usuarioId)
    {
        var prestamo = await _unitOfWork.Prestamos.Query()
            .Include(p => p.Cuotas)
                .ThenInclude(c => c.Pago)
            .FirstOrDefaultAsync(p => p.PrestamoId == id);

        if (prestamo == null)
            return (false, "Préstamo no encontrado.");

        if (prestamo.Estado != "Activo")
            return (false, "El préstamo debe estar activo.");

        if (montoAbono <= 0)
            return (false, "El monto del abono debe ser mayor a cero.");

        if (montoAbono >= prestamo.MontoPrestado)
            return (false, "El abono no puede liquidar el total del capital. Utilice la opción 'Liquidación Total'.");

        // 1. Crear cuota especial para el abono a capital
        var cuotaAbono = new Cuota
        {
            PrestamoId = prestamo.PrestamoId,
            NumeroCuota = -1, // Identificador de abono extra a capital
            Monto = montoAbono,
            FechaVencimiento = DateTime.UtcNow,
            Estado = "Pagado",
            FechaPago = DateTime.UtcNow
        };
        await _unitOfWork.Cuotas.AddAsync(cuotaAbono);
        await _unitOfWork.SaveChangesAsync(); // Para obtener el CuotaId

        // 2. Crear el pago asociado
        var pago = new Pago
        {
            CuotaId = cuotaAbono.CuotaId,
            UsuarioId = usuarioId,
            MontoPagado = montoAbono,
            FechaPago = DateTime.UtcNow,
            Estado = "Activo"
        };
        await _unitOfWork.Pagos.AddAsync(pago);

        // 3. Reducir el capital prestado
        prestamo.MontoPrestado -= montoAbono;

        // 4. Identificar cuotas ya pagadas (excluyendo la que acabamos de crear)
        var cuotasPagadas = prestamo.Cuotas.Where(c => c.NumeroCuota > 0 && ((c.Pago != null && c.Pago.Estado == "Activo") || c.Estado == "Pagado")).ToList();
        var cuotasPendientes = prestamo.Cuotas.Where(c => c.NumeroCuota > 0 && ((c.Pago == null || c.Pago.Estado == "Anulado") && c.Estado != "Pagado")).ToList();
        
        if (prestamo.TipoCredito == "Solo Interés")
        {
            decimal divisor = 1m;
            if (prestamo.PeriodicidadDias == 7) divisor = 4m;
            else if (prestamo.PeriodicidadDias == 14 || prestamo.PeriodicidadDias == 15) divisor = 2m;
            else if (prestamo.PeriodicidadDias == 1) divisor = 30m;

            var nuevoMontoCuota = Math.Round((prestamo.MontoPrestado * (prestamo.PorcentajeInteres / 100m)) / divisor, 0);

            // Borrar cuota pendiente (si existe alguna)
            foreach (var cp in cuotasPendientes)
            {
                _unitOfWork.Cuotas.Remove(cp);
            }

            // Generar 1 nueva cuota de interes
            DateTime fechaBase = cuotasPagadas.Any() 
                ? cuotasPagadas.Max(c => c.FechaVencimiento) 
                : DateTime.UtcNow;

            var nuevaCuota = new Cuota
            {
                PrestamoId = prestamo.PrestamoId,
                NumeroCuota = 1,
                Monto = nuevoMontoCuota,
                FechaVencimiento = fechaBase.AddDays(prestamo.PeriodicidadDias),
                Estado = "Pendiente"
            };
            await _unitOfWork.Cuotas.AddAsync(nuevaCuota);

            prestamo.MontoTotal = prestamo.MontoPrestado;
            prestamo.SaldoPendiente = prestamo.MontoPrestado;
        }
        else
        {
            // Para "Cuota Completa", recalculamos las cuotas pendientes.
            foreach (var cp in cuotasPendientes)
            {
                _unitOfWork.Cuotas.Remove(cp);
            }

            int cuotasAGenerar = cuotasPendientes.Count;
            if (cuotasAGenerar > 0)
            {
                var totalDiasRestantes = prestamo.PeriodicidadDias * cuotasAGenerar;
                var plazoMesesRestantes = Math.Round((decimal)totalDiasRestantes / 30m, 0, MidpointRounding.AwayFromZero);
                if (plazoMesesRestantes < 1) plazoMesesRestantes = 1m;

                var interesTotalNuevo = prestamo.MontoPrestado * (prestamo.PorcentajeInteres / 100m) * plazoMesesRestantes;
                var montoInteres = Math.Round(interesTotalNuevo, 0);
                var montoTotalRestante = Math.Round(prestamo.MontoPrestado + montoInteres, 0);

                var montoCuota = Math.Ceiling((montoTotalRestante / cuotasAGenerar) / 1000m) * 1000m;

                decimal acumulado = 0;
                DateTime fechaBase = cuotasPagadas.Any() 
                    ? cuotasPagadas.Max(c => c.FechaVencimiento) 
                    : prestamo.FechaInicio;

                int proximoNumero = cuotasPagadas.Count > 0 ? cuotasPagadas.Max(c => c.NumeroCuota) + 1 : 1;

                for (int i = 1; i <= cuotasAGenerar; i++)
                {
                    var monto = (i == cuotasAGenerar) ? montoTotalRestante - acumulado : montoCuota;
                    acumulado += monto;

                    var nuevaCuota = new Cuota
                    {
                        PrestamoId = prestamo.PrestamoId,
                        NumeroCuota = proximoNumero++,
                        Monto = monto,
                        FechaVencimiento = fechaBase.AddDays(prestamo.PeriodicidadDias * i),
                        Estado = "Pendiente"
                    };
                    await _unitOfWork.Cuotas.AddAsync(nuevaCuota);
                }

                prestamo.SaldoPendiente = montoTotalRestante;
                
                decimal totalYaPagado = cuotasPagadas.Sum(c => c.Monto);
                prestamo.MontoTotal = totalYaPagado + montoTotalRestante; 
            }
        }

        _unitOfWork.Prestamos.Update(prestamo);
        await _unitOfWork.SaveChangesAsync();

        return (true, "Abono a capital registrado y cuotas recalculadas exitosamente.");
    }
}
