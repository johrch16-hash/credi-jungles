using FluentAssertions;
using Moq;
using PrestamosCobros.BLL.DTOs;
using PrestamosCobros.BLL.Services;
using PrestamosCobros.DAL.Entities;
using PrestamosCobros.DAL.Repositories;

namespace PrestamosCobros.Tests.Services;

public class PrestamoServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly PrestamoService _service;

    public PrestamoServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _service = new PrestamoService(_unitOfWorkMock.Object);
    }

    [Fact]
    public async Task GenerarPreviewAsync_DebeCalcularMontoTotalYGenerarPlanDeCuotasCorrectamente()
    {
        // Arrange
        var dto = new PrestamoCreateDto
        {
            MontoPrestado = 100000,
            PorcentajeInteres = 20,
            NumeroCuotas = 5,
            PeriodicidadDias = 7,
            FechaInicio = new DateTime(2026, 5, 10)
        };

        // Act
        var result = await _service.GenerarPreviewAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.MontoPrestado.Should().Be(100000);
        result.PorcentajeInteres.Should().Be(20);
        
        // Base cuota = 100000 / 5 = 20000
        // Interes por cuota = 20000 * 0.20 = 4000
        // Cuota = 24000
        // Total = 120000
        // Interes = 20000
        result.MontoTotal.Should().Be(120000);
        result.MontoInteres.Should().Be(20000);
        result.MontoCuota.Should().Be(24000);
        
        result.Cuotas.Should().HaveCount(5);
        result.Cuotas.First().Monto.Should().Be(24000);
        result.Cuotas.First().FechaVencimiento.Should().Be(new DateTime(2026, 5, 17));
        result.Cuotas.Last().FechaVencimiento.Should().Be(new DateTime(2026, 6, 14));
    }
}
