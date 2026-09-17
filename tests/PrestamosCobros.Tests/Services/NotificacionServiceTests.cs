using FluentAssertions;
using MockQueryable;
using MockQueryable.Moq;
using Moq;
using PrestamosCobros.BLL.Interfaces;
using PrestamosCobros.BLL.Services;
using PrestamosCobros.DAL.Entities;
using PrestamosCobros.DAL.Repositories;
using PrestamosCobros.Infrastructure.Interfaces;

namespace PrestamosCobros.Tests.Services;

public class NotificacionServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly NotificacionService _service;

    public NotificacionServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        var configWhatsAppMock = new Mock<IConfiguracionWhatsAppService>();
        var httpClient = new System.Net.Http.HttpClient();
        var whatsAppService = new WhatsAppService(configWhatsAppMock.Object, httpClient);
        var emailServiceMock = new Mock<IEmailService>();
        _service = new NotificacionService(_unitOfWorkMock.Object, whatsAppService, emailServiceMock.Object);
    }


    [Fact]
    public async Task MarcarCuotasAtrasadasAsync_DebeActualizarCuotasVencidasYEstadoCliente()
    {
        // Arrange
        var ayer = DateTime.UtcNow.Date.AddDays(-5);
        
        var clienteMock = new Cliente
        {
            ClienteId = 1,
            NombreCompleto = "Juan Perez",
            Estado = "Activo",
            NivelAtrasos = 0
        };

        var prestamoMock = new Prestamo
        {
            PrestamoId = 1,
            ClienteId = 1,
            Estado = "Activo",
            Cliente = clienteMock
        };

        var cuotaVencidaMock = new Cuota
        {
            CuotaId = 1,
            PrestamoId = 1,
            FechaVencimiento = ayer,
            Estado = "Pendiente",
            Prestamo = prestamoMock
        };

        var cuotasList = new List<Cuota> { cuotaVencidaMock };
        var cuotasDbSetMock = cuotasList.BuildMockDbSet();

        var cuotasRepoMock = new Mock<IRepository<Cuota>>();
        cuotasRepoMock.Setup(x => x.Query()).Returns(cuotasDbSetMock.Object);

        var clientesRepoMock = new Mock<IRepository<Cliente>>();
        clientesRepoMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(clienteMock);

        _unitOfWorkMock.Setup(u => u.Cuotas).Returns(cuotasRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Clientes).Returns(clientesRepoMock.Object);

        // Act
        await _service.MarcarCuotasAtrasadasAsync();

        // Assert
        cuotaVencidaMock.Estado.Should().Be("Atrasado");
        cuotasRepoMock.Verify(x => x.Update(cuotaVencidaMock), Times.Once);
        
        // Verifica que también actualizó el cliente (1 atraso)
        clienteMock.NivelAtrasos.Should().Be(1);
        clientesRepoMock.Verify(x => x.Update(clienteMock), Times.Once);

        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }
}
