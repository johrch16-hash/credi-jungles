using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Layout.Borders;
using Microsoft.Extensions.Configuration;
using PrestamosCobros.BLL.DTOs;
using PrestamosCobros.BLL.Interfaces;
using System.IO;
using System;

namespace PrestamosCobros.Web.Services;

public class PagarePdfService : IPagarePdfService
{
    private readonly IConfiguration _configuration;

    public PagarePdfService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public byte[] GenerarPagare(PagareCreateDto dto)
    {
        var appName = _configuration["ApplicationSettings:AppName"] ?? "CrediGestión";
        var ciudad = _configuration["ApplicationSettings:City"] ?? "San José, Costa Rica";

        using var ms = new MemoryStream();
        using var pdfWriter = new PdfWriter(ms);
        using var pdfDocument = new PdfDocument(pdfWriter);
        using var document = new Document(pdfDocument);

        var fontNormal = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        var fontBold = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);

        document.SetMargins(35, 45, 35, 45);

        // Header Table
        var headerTable = new Table(UnitValue.CreatePercentArray(new float[] { 60, 40 })).UseAllAvailableWidth().SetMarginBottom(20);

        var cellTitle = new Cell().SetBorder(Border.NO_BORDER).SetVerticalAlignment(iText.Layout.Properties.VerticalAlignment.MIDDLE);
        cellTitle.Add(new Paragraph("PAGARÉ SIN PROTESTO")
            .SetFont(fontBold)
            .SetFontSize(16)
            .SetTextAlignment(TextAlignment.LEFT));
        headerTable.AddCell(cellTitle);

        var cellAmount = new Cell().SetBorder(new SolidBorder(1)).SetPadding(5).SetVerticalAlignment(iText.Layout.Properties.VerticalAlignment.MIDDLE);
        cellAmount.Add(new Paragraph($"POR: ¢{dto.Monto:N2}")
            .SetFont(fontBold)
            .SetFontSize(14)
            .SetTextAlignment(TextAlignment.CENTER));
        headerTable.AddCell(cellAmount);

        document.Add(headerTable);

        // Body Text
        var textFiador = dto.TieneFiador && !string.IsNullOrWhiteSpace(dto.NombreFiador)
            ? $" y {dto.NombreFiador.ToUpper()}, portador(a) de la cédula de identidad número {dto.CedulaFiador}, vecino(a) de {dto.DireccionFiador}, en condición de FIADOR(A) SOLIDARIO(A),"
            : "";

        var body = new Paragraph()
            .SetFont(fontNormal)
            .SetFontSize(12)
            .SetTextAlignment(TextAlignment.JUSTIFIED)
            .SetMultipliedLeading(1.3f);

        body.Add(new Text($"Yo, {dto.NombreDeudor.ToUpper()}, portador(a) de la cédula de identidad número {dto.CedulaDeudor}, estado civil {dto.EstadoCivil.ToLower()}, profesión u oficio {dto.Profesion.ToLower()}, vecino(a) de {dto.DireccionHabitacion},{textFiador} "));
        body.Add(new Text($"por este medio prometo(emos) y me(nos) obligo(amos) a pagar de forma incondicional a la orden de la empresa denominada "));
        body.Add(new Text(appName).SetFont(fontBold));
        body.Add(new Text($" (en adelante el ACREEDOR), la suma principal de "));
        body.Add(new Text($"{dto.MontoLetras.ToUpper()} colones (¢{dto.Monto:N2}).").SetFont(fontBold));

        document.Add(body);

        var terms = new Paragraph()
            .SetFont(fontNormal)
            .SetFontSize(12)
            .SetTextAlignment(TextAlignment.JUSTIFIED)
            .SetMultipliedLeading(1.3f)
            .SetMarginTop(10);

        terms.Add(new Text($"La suma adeudada devengará un interés corriente del "));
        terms.Add(new Text($"{dto.TasaCorriente}% ").SetFont(fontBold));
        terms.Add(new Text($"mensual y un interés moratorio del "));
        terms.Add(new Text($"{dto.TasaMoratoria}% ").SetFont(fontBold));
        terms.Add(new Text($"mensual en caso de atraso. "));
        terms.Add(new Text($"El vencimiento de esta obligación será el día "));
        terms.Add(new Text($"{dto.FechaVencimiento.ToString("dd 'de' MMMM 'de' yyyy", new System.Globalization.CultureInfo("es-CR"))}.").SetFont(fontBold));

        document.Add(terms);

        var disclaimer = new Paragraph("Renuncio(amos) a mi(nuestro) domicilio, a los requerimientos de pago, al protesto de este documento y a todos los trámites del juicio ejecutivo. Las costas, gastos personales y honorarios de abogado en caso de cobro judicial o extrajudicial correrán por mi(nuestra) cuenta.")
            .SetFont(fontNormal)
            .SetFontSize(12)
            .SetTextAlignment(TextAlignment.JUSTIFIED)
            .SetMultipliedLeading(1.3f)
            .SetMarginTop(10)
            .SetMarginBottom(25);
        document.Add(disclaimer);

        // Date and place
        var datePlace = new Paragraph($"Dado en {ciudad}, a los {DateTime.Now.Day} días del mes de {DateTime.Now.ToString("MMMM", new System.Globalization.CultureInfo("es-CR"))} del año {DateTime.Now.Year}.")
            .SetFont(fontNormal)
            .SetFontSize(12)
            .SetMarginBottom(45);
        document.Add(datePlace);

        // Signatures
        var sigTable = new Table(UnitValue.CreatePercentArray(new float[] { 1, 1 })).UseAllAvailableWidth();

        var cellDeudor = new Cell().SetBorder(Border.NO_BORDER).SetTextAlignment(TextAlignment.CENTER);
        cellDeudor.Add(new Paragraph("_________________________________________").SetMarginBottom(5));
        cellDeudor.Add(new Paragraph("Firma del Deudor").SetFont(fontBold));
        cellDeudor.Add(new Paragraph(dto.NombreDeudor));
        cellDeudor.Add(new Paragraph($"Céd: {dto.CedulaDeudor}"));
        sigTable.AddCell(cellDeudor);

        var cellAcreedor = new Cell().SetBorder(Border.NO_BORDER).SetTextAlignment(TextAlignment.CENTER);
        cellAcreedor.Add(new Paragraph("_________________________________________").SetMarginBottom(5));
        cellAcreedor.Add(new Paragraph("Firma del Acreedor / Representante").SetFont(fontBold));
        cellAcreedor.Add(new Paragraph(appName));
        sigTable.AddCell(cellAcreedor);

        document.Add(sigTable);

        if (dto.TieneFiador && !string.IsNullOrWhiteSpace(dto.NombreFiador))
        {
            var sigTableFiador = new Table(1).UseAllAvailableWidth().SetMarginTop(30);
            var cellFiador = new Cell().SetBorder(Border.NO_BORDER).SetTextAlignment(TextAlignment.CENTER);
            cellFiador.Add(new Paragraph("_________________________________________").SetMarginBottom(5));
            cellFiador.Add(new Paragraph("Firma del Fiador Solidario").SetFont(fontBold));
            cellFiador.Add(new Paragraph(dto.NombreFiador));
            cellFiador.Add(new Paragraph($"Céd: {dto.CedulaFiador}"));
            sigTableFiador.AddCell(cellFiador);
            document.Add(sigTableFiador);
        }

        document.Close();
        return ms.ToArray();
    }
}
