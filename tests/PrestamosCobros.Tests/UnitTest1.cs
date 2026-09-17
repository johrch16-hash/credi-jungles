using System.Collections.Generic;
using System.IO;
using PrestamosCobros.Infrastructure.Interfaces;
using PrestamosCobros.Infrastructure.Services;
using Xunit;

public class UnitTest1
{
    [Fact]
    public void TestGenerarReporteGeneralPdf()
    {
        var service = new ReporteGeneralService();
        var items = new List<ReporteGeneralItem>
        {
            new ReporteGeneralItem
            {
                ClienteNombre = "Johryan",
                Alias = "Compras",
                MontoPrestado = 100000,
                MontoTotal = 120000,
                SaldoPendiente = 60000,
                PorcentajePagado = 50,
                Estado = "Activo"
            },
            new ReporteGeneralItem
            {
                ClienteNombre = "Byron Quiñonez",
                Alias = "Compras",
                MontoPrestado = 100000,
                MontoTotal = 120000,
                SaldoPendiente = 120000,
                PorcentajePagado = 0,
                Estado = "Activo"
            }
        };

        var pdf = service.GenerarReporteEstadoCuentaGeneral(items);
        File.WriteAllBytes("C:\\Users\\johry\\Downloads\\Reporte_General_Redesigned.pdf", pdf);
    }
}
