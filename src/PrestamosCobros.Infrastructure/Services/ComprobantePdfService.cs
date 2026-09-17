using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using PrestamosCobros.Infrastructure.Interfaces;
using PrestamosCobros.DAL.Repositories;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PrestamosCobros.Infrastructure.Services;

public class ComprobantePdfService : IComprobantePdfService
{
    private readonly IUnitOfWork _unitOfWork;

    public ComprobantePdfService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GenerarComprobante(string clienteNombre, decimal montoPagado, DateTime fechaPago, int numeroCuota, decimal saldoRestante, int pagoId)
    {
        // Obtener información adicional del pago de forma síncrona
        var pago = _unitOfWork.Pagos.Query()
            .Include(p => p.Cuota)
            .FirstOrDefault(p => p.PagoId == pagoId);

        DateTime? fechaProximoPago = null;
        if (pago != null)
        {
            var proximaCuota = _unitOfWork.Cuotas.Query()
                .Where(c => c.PrestamoId == pago.Cuota.PrestamoId && c.Estado != "Pagado")
                .OrderBy(c => c.NumeroCuota)
                .FirstOrDefault();

            if (proximaCuota != null)
            {
                fechaProximoPago = proximaCuota.FechaVencimiento;
            }
        }

        // Obtener texto personalizado configurado por el usuario
        var textoAdicional = _unitOfWork.Preferencias.Query()
            .FirstOrDefault(p => p.Clave == "Comprobante_TextoAdicional")?.Valor ?? "";

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(1, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                page.Header().Element(ComposeHeader);
                page.Content().Element(x => ComposeContent(x, clienteNombre, montoPagado, fechaPago, numeroCuota, saldoRestante, pagoId, fechaProximoPago, textoAdicional));
                page.Footer().Element(ComposeFooter);
            });
        });

        return document.GeneratePdf();
    }

    private void ComposeHeader(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("Comprobante de Pago").FontSize(18).SemiBold().FontColor("#0f172a");
                column.Item().Text("CrediGest - Sistema de Préstamos").FontSize(9).FontColor(Colors.Grey.Medium);
            });
        });
    }

    private void ComposeContent(IContainer container, string clienteNombre, decimal montoPagado, DateTime fechaPago, int numeroCuota, decimal saldoRestante, int pagoId, DateTime? fechaProximoPago, string textoAdicional)
    {
        container.PaddingVertical(0.5f, Unit.Centimetre).Column(column =>
        {
            column.Spacing(5);

            column.Item().Row(row =>
            {
                row.RelativeItem().Text("Número de Recibo:");
                row.RelativeItem().AlignRight().Text($"PAG-{pagoId:D6}").SemiBold();
            });

            column.Item().Row(row =>
            {
                row.RelativeItem().Text("Fecha de Pago:");
                row.RelativeItem().AlignRight().Text(fechaPago.ToString("dd/MM/yyyy HH:mm"));
            });

            column.Item().Row(row =>
            {
                row.RelativeItem().Text("Cliente:");
                row.RelativeItem().AlignRight().Text(clienteNombre).SemiBold();
            });

            column.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

            column.Item().Row(row =>
            {
                row.RelativeItem().Text($"Pago Cuota #{numeroCuota}:");
                row.RelativeItem().AlignRight().Text($"₡{montoPagado:N0}").FontSize(13).SemiBold().FontColor(Colors.Green.Darken2);
            });

            column.Item().Row(row =>
            {
                row.RelativeItem().Text("Saldo Pendiente:");
                row.RelativeItem().AlignRight().Text($"₡{saldoRestante:N0}").SemiBold();
            });

            column.Item().Row(row =>
            {
                row.RelativeItem().Text("Próximo Pago:");
                if (fechaProximoPago.HasValue)
                {
                    row.RelativeItem().AlignRight().Text(fechaProximoPago.Value.ToString("dd/MM/yyyy")).SemiBold();
                }
                else
                {
                    row.RelativeItem().AlignRight().Text("N/A (Liquidado)").SemiBold().FontColor(Colors.Green.Darken2);
                }
            });

            if (!string.IsNullOrWhiteSpace(textoAdicional))
            {
                column.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                column.Item().Text("Cuentas de Pago / Notas:").FontSize(9).SemiBold().FontColor(Colors.Grey.Darken3);
                column.Item().Text(textoAdicional).FontSize(8.5f).FontColor(Colors.Grey.Medium).LineHeight(1.3f);
            }
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.AlignCenter().Text(x =>
        {
            x.Span("Gracias por su preferencia. ").FontSize(8.5f);
            x.Span("Generado automáticamente por CrediGest.").FontSize(8).FontColor(Colors.Grey.Medium);
        });
    }
}
