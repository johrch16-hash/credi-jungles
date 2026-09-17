using FluentAssertions;
using MockQueryable;
using MockQueryable.Moq;
using Moq;
using PrestamosCobros.BLL.DTOs;
using PrestamosCobros.BLL.Services;
using PrestamosCobros.DAL.Entities;
using PrestamosCobros.DAL.Repositories;
using Hangfire;
using PrestamosCobros.Infrastructure.Interfaces;

namespace PrestamosCobros.Tests.Services;

public class PagoServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IBackgroundJobClient> _backgroundJobClientMock;
    private readonly Mock<IComprobantePdfService> _pdfServiceMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly PagoService _service;

    public PagoServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _backgroundJobClientMock = new Mock<IBackgroundJobClient>();
        _pdfServiceMock = new Mock<IComprobantePdfService>();
        _emailServiceMock = new Mock<IEmailService>();
        
        _service = new PagoService(
            _unitOfWorkMock.Object, 
            _backgroundJobClientMock.Object, 
            _pdfServiceMock.Object, 
            _emailServiceMock.Object,
            null!);
    }

    [Fact]
    public async Task RegistrarPagoAsync_ConCuotaValida_DebeRegistrarPagoYActualizarSaldos()
    {
        // Arrange
        var cuotaMock = new Cuota
        {
            CuotaId = 1,
            PrestamoId = 1,
            Monto = 24000,
            Estado = "Pendiente",
            Prestamo = new Prestamo
            {
                PrestamoId = 1,
                Estado = "Activo",
                SaldoPendiente = 120000
            }
        };

        var cuotasList = new List<Cuota> { cuotaMock };
        var cuotasDbSetMock = cuotasList.BuildMockDbSet();

        var cuotasRepoMock = new Mock<IRepository<Cuota>>();
        cuotasRepoMock.Setup(x => x.Query()).Returns(cuotasDbSetMock.Object);

        var pagosRepoMock = new Mock<IRepository<Pago>>();
        var prestamosRepoMock = new Mock<IRepository<Prestamo>>();

        _unitOfWorkMock.Setup(u => u.Cuotas).Returns(cuotasRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Pagos).Returns(pagosRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Prestamos).Returns(prestamosRepoMock.Object);

        var dto = new PagoCreateDto
        {
            CuotaId = 1,
            MontoPagado = 24000
        };

        // Act
        var result = await _service.RegistrarPagoAsync(dto, 1);

        // Assert
        result.Exito.Should().BeTrue();
        cuotaMock.Estado.Should().Be("Pagado");
        cuotaMock.Prestamo.SaldoPendiente.Should().Be(120000 - 24000);
        
        pagosRepoMock.Verify(x => x.AddAsync(It.Is<Pago>(p => p.CuotaId == 1 && p.MontoPagado == 24000)), Times.Once);
        cuotasRepoMock.Verify(x => x.Update(cuotaMock), Times.Once);
        prestamosRepoMock.Verify(x => x.Update(cuotaMock.Prestamo), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task RegistrarPagoAsync_ConCuotaYaPagada_DebeRetornarError()
    {
        // Arrange
        var cuotaMock = new Cuota
        {
            CuotaId = 1,
            Estado = "Pagado",
            Pago = new Pago { PagoId = 1 },
            Prestamo = new Prestamo { Estado = "Activo" }
        };

        var cuotasList = new List<Cuota> { cuotaMock };
        var cuotasDbSetMock = cuotasList.BuildMockDbSet();

        var cuotasRepoMock = new Mock<IRepository<Cuota>>();
        cuotasRepoMock.Setup(x => x.Query()).Returns(cuotasDbSetMock.Object);

        _unitOfWorkMock.Setup(u => u.Cuotas).Returns(cuotasRepoMock.Object);

        var dto = new PagoCreateDto { CuotaId = 1, MontoPagado = 24000 };

        // Act
        var result = await _service.RegistrarPagoAsync(dto, 1);

        // Assert
        result.Exito.Should().BeFalse();
        result.Mensaje.Should().Be("Esta cuota ya fue pagada.");
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Never);
    }
}
