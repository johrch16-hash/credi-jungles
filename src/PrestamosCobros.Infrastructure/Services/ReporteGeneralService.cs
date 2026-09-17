using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using PrestamosCobros.Infrastructure.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PrestamosCobros.Infrastructure.Services;

public class ReporteGeneralService : IReporteGeneralService
{
    public ReporteGeneralService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GenerarReporteEstadoCuentaGeneral(List<ReporteGeneralItem> items)
    {
        // Cargar logotipo si existe
        byte[]? logoBytes = null;
        try
        {
            var logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "img", "logo.png");
            if (!File.Exists(logoPath))
            {
                logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "img", "logo.png");
            }
            if (File.Exists(logoPath))
            {
                logoBytes = File.ReadAllBytes(logoPath);
            }
        }
        catch (Exception)
        {
            // Omitir si hay algún problema leyendo el archivo
        }

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(9.5f).FontFamily(Fonts.Arial).FontColor("#1e293b"));

                // Header Institucional
                page.Header().Row(row =>
                {
                    row.RelativeItem().Row(r => {
                        if (logoBytes != null)
                        {
                            r.ConstantItem(35).Height(35).Image(logoBytes);
                            r.ConstantItem(10);
                        }
                        r.RelativeItem().Column(col =>
                        {
                            col.Item().Text("CrediGest").FontSize(22).ExtraBold().FontColor("#0f172a").LetterSpacing(0.02f);
                            col.Item().Text("SISTEMA DE GESTIÓN DE CRÉDITOS Y COBROS").FontSize(8).SemiBold().FontColor(Colors.Grey.Medium).LetterSpacing(0.1f);
                        });
                    });

                    row.RelativeItem().AlignRight().Column(col =>
                    {
                        col.Item().Text("REPORTE GENERAL DE CARTERA").FontSize(11).SemiBold().FontColor("#64748B");
                        col.Item().Text($"{DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(8.5f).FontColor("#94A3B8");
                    });
                });

                page.Content().PaddingVertical(0.8f, Unit.Centimetre).Column(col =>
                {
                    // Resumen Ejecutivo
                    col.Item().PaddingBottom(0.8f, Unit.Centimetre).Row(row =>
                    {
                        row.RelativeItem().Element(e => SummaryCard(e, "CLIENTES ACTIVOS", items.Count.ToString(), "#f0fdf4", "#16a34a"));
                        row.ConstantItem(15);
                        row.RelativeItem().Element(e => SummaryCard(e, "TOTAL PRESTADO", $"₡{items.Sum(i => i.MontoPrestado):N0}", "#f8fafc", "#0f172a"));
                        row.ConstantItem(15);
                        row.RelativeItem().Element(e => SummaryCard(e, "SALDO PENDIENTE", $"₡{items.Sum(i => i.SaldoPendiente):N0}", "#fff7ed", "#ea580c"));
                    });

                    // Tabla de Clientes
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);  // Cliente
                            columns.RelativeColumn(2);  // Monto Prestado
                            columns.RelativeColumn(2);  // Saldo
                            columns.RelativeColumn(2.5f);  // Avance
                            columns.ConstantColumn(85); // Estado
                        });

                        // Cabecera Profesional
                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderStyle).Text("CLIENTE / ALIAS");
                            header.Cell().Element(HeaderStyle).AlignRight().Text("PRESTADO");
                            header.Cell().Element(HeaderStyle).AlignRight().Text("SALDO");
                            header.Cell().Element(HeaderStyle).AlignCenter().Text("AVANCE DE PAGO");
                            header.Cell().Element(HeaderStyle).AlignCenter().Text("ESTADO");

                            static IContainer HeaderStyle(IContainer container)
                            {
                                return container.DefaultTextStyle(x => x.SemiBold().FontSize(8.5f).FontColor(Colors.White))
                                                .Background("#0f172a")
                                                .PaddingVertical(8)
                                                .PaddingHorizontal(8);
                            }
                        });

                        // Filas
                        for (int i = 0; i < items.Count; i++)
                        {
                            var p = items[i];
                            var bgColor = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;

                            table.Cell().Element(e => ContentStyle(e, bgColor)).Column(c => {
                                c.Item().Text(p.ClienteNombre).SemiBold().FontSize(9.5f).FontColor("#0f172a");
                                if (!string.IsNullOrEmpty(p.Alias))
                                    c.Item().Text(p.Alias).FontSize(8).FontColor("#64748B").Italic();
                            });
                            
                            table.Cell().Element(e => ContentStyle(e, bgColor)).AlignRight().Text($"₡{p.MontoPrestado:N0}").FontSize(9.5f);
                            table.Cell().Element(e => ContentStyle(e, bgColor)).AlignRight().Text($"₡{p.SaldoPendiente:N0}").SemiBold().FontSize(9.5f).FontColor(p.SaldoPendiente > 0 ? "#0f172a" : Colors.Green.Medium);
                            
                            // ProgressBar Cell
                            table.Cell().Element(e => ContentStyle(e, bgColor)).PaddingHorizontal(10).Column(c => {
                                c.Item().PaddingTop(4).Row(r => {
                                    r.RelativeItem().PaddingRight(5).Element(e => {
                                        e.Height(6).Background(Colors.Grey.Lighten3).MaxWidth(85).Row(rowProgress => {
                                            if (p.PorcentajePagado > 0)
                                                rowProgress.RelativeItem(p.PorcentajePagado).Background(p.PorcentajePagado == 100 ? Colors.Green.Medium : "#0f172a");
                                            
                                            if (p.PorcentajePagado < 100)
                                                rowProgress.RelativeItem(100 - p.PorcentajePagado);
                                        });
                                    });
                                    r.ConstantItem(30).AlignRight().Text($"{p.PorcentajePagado}%").FontSize(8).Bold();
                                });
                            });

                            table.Cell().Element(e => ContentStyle(e, bgColor)).AlignCenter().Element(e => {
                                string badgeBg = "#e2e8f0";
                                string badgeText = "#475569";
                                
                                if (p.Estado.Equals("Activo", StringComparison.OrdinalIgnoreCase))
                                {
                                    badgeBg = "#d1fae5"; // soft green
                                    badgeText = "#065f46"; // dark green
                                }
                                else if (p.Estado.Equals("Atrasado", StringComparison.OrdinalIgnoreCase) || p.Estado.Equals("Vencido", StringComparison.OrdinalIgnoreCase))
                                {
                                    badgeBg = "#fee2e2"; // soft red
                                    badgeText = "#991b1b"; // dark red
                                }
                                else if (p.Estado.Equals("Pendiente", StringComparison.OrdinalIgnoreCase))
                                {
                                    badgeBg = "#fef3c7"; // soft amber
                                    badgeText = "#92400e"; // dark amber
                                }
                                else if (p.Estado.Equals("Finalizado", StringComparison.OrdinalIgnoreCase) || p.Estado.Equals("Cancelado", StringComparison.OrdinalIgnoreCase) || p.Estado.Equals("Liquidado", StringComparison.OrdinalIgnoreCase))
                                {
                                    badgeBg = "#f1f5f9"; // soft slate/grey
                                    badgeText = "#475569"; // slate text
                                }

                                e.Background(badgeBg)
                                 .PaddingVertical(3)
                                 .PaddingHorizontal(8)
                                 .Text(p.Estado.ToUpper())
                                 .FontSize(7.5f)
                                 .Bold()
                                 .FontColor(badgeText);
                            });

                            static IContainer ContentStyle(IContainer container, string bgColor)
                            {
                                return container.Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten4).PaddingVertical(6).PaddingHorizontal(8);
                            }
                        }
                    });
                });

                page.Footer().Column(col => {
                    col.Item().PaddingTop(10).BorderTop(1).BorderColor(Colors.Grey.Lighten4).Row(row =>
                    {
                        row.RelativeItem().Text("CrediGest - Reporte de Cartera Confidencial").FontSize(8).FontColor(Colors.Grey.Medium);
                        row.RelativeItem().AlignRight().Text(x =>
                        {
                            x.Span("Página ").FontSize(8);
                            x.CurrentPageNumber().FontSize(8);
                            x.Span(" de ").FontSize(8);
                            x.TotalPages().FontSize(8);
                        });
                    });
                });
            });
        });

        return document.GeneratePdf();
    }

    public byte[] GenerarReporteEstadoCuentaIndividual(ReporteIndividualModel model)
    {
        // Cargar logotipo si existe
        byte[]? logoBytes = null;
        try
        {
            var logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "img", "logo.png");
            if (!File.Exists(logoPath))
            {
                logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "img", "logo.png");
            }
            if (File.Exists(logoPath))
            {
                logoBytes = File.ReadAllBytes(logoPath);
            }
        }
        catch (Exception)
        {
            // Omitir si hay algún problema leyendo el archivo
        }

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(9.5f).FontFamily(Fonts.Arial).FontColor("#1e293b"));

                // Header
                page.Header().Row(row =>
                {
                    row.RelativeItem().Row(r => {
                        if (logoBytes != null)
                        {
                            r.ConstantItem(35).Height(35).Image(logoBytes);
                            r.ConstantItem(10);
                        }
                        r.RelativeItem().Column(col =>
                        {
                            col.Item().Text("CrediGest").FontSize(22).ExtraBold().FontColor("#0f172a").LetterSpacing(0.02f);
                            col.Item().Text("ESTADO DE CUENTA INDIVIDUAL").FontSize(8).SemiBold().FontColor(Colors.Grey.Medium).LetterSpacing(0.1f);
                        });
                    });

                    row.RelativeItem().AlignRight().Column(col =>
                    {
                        col.Item().Text("FECHA DE EMISIÓN").FontSize(7).Bold().FontColor("#64748B");
                        col.Item().Text($"{DateTime.Now:dd/MM/yyyy}").FontSize(10).SemiBold().FontColor("#0f172a");
                    });
                });

                page.Content().PaddingVertical(0.8f, Unit.Centimetre).Column(col =>
                {
                    // Datos del Cliente
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("CLIENTE").FontSize(7.5f).Bold().FontColor("#64748B");
                            c.Item().Text(model.ClienteNombre).FontSize(16).ExtraBold().FontColor("#0f172a");
                            if (!string.IsNullOrEmpty(model.Cedula))
                                c.Item().Text($"Identificación: {model.Cedula}").FontSize(9).FontColor("#64748B");
                            if (!string.IsNullOrEmpty(model.Telefono))
                                c.Item().Text($"Teléfono: {model.Telefono}").FontSize(9).FontColor("#64748B");
                        });

                        row.RelativeItem().AlignRight().Column(c =>
                        {
                            c.Item().Text("SALDO TOTAL PENDIENTE").FontSize(7.5f).Bold().FontColor("#64748B");
                            c.Item().Text($"₡{model.SaldoTotalPendiente:N0}").FontSize(20).ExtraBold().FontColor(model.SaldoTotalPendiente > 0 ? "#ea580c" : Colors.Green.Medium);
                        });
                    });

                    col.Item().PaddingTop(0.8f, Unit.Centimetre).Column(c =>
                    {
                        foreach (var prestamo in model.Prestamos)
                        {
                            c.Item().PaddingBottom(0.6f, Unit.Centimetre).Container()
                                .Background("#f8fafc")
                                .Border(1)
                                .BorderColor("#e2e8f0")
                                .Row(row =>
                                {
                                    row.ConstantItem(4).Background("#0f172a");
                                    row.RelativeItem().Padding(15).Column(pc =>
                                    {
                                        pc.Item().Row(pr =>
                                {
                                    pr.RelativeItem().Column(pcc =>
                                    {
                                        pcc.Item().Text($"PRÉSTAMO #{prestamo.PrestamoId:D5}").FontSize(10).Bold().FontColor("#0f172a");
                                        pcc.Item().Text($"Monto Prestado: ₡{prestamo.MontoPrestado:N0} • Fecha de Inicio: {prestamo.FechaCreacion:dd/MM/yyyy}").FontSize(8.5f).FontColor("#64748B");
                                    });
                                    pr.RelativeItem().AlignRight().Column(pcc =>
                                    {
                                        pcc.Item().Text("ESTADO").FontSize(7).Bold().FontColor("#64748B");
                                        var estadoColor = prestamo.Estado == "Activo" ? Colors.Green.Medium : Colors.Grey.Medium;
                                        if (prestamo.Estado == "Atrasado") estadoColor = Colors.Red.Medium;
                                        pcc.Item().Text(prestamo.Estado.ToUpper()).FontSize(8.5f).Bold().FontColor(estadoColor);
                                    });
                                });

                                pc.Item().PaddingTop(10).Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(); // Fecha
                                        columns.RelativeColumn(0.7f); // Cuota
                                        columns.RelativeColumn(); // Monto
                                        columns.RelativeColumn(1.3f); // Concepto
                                    });

                                    table.Header(header =>
                                    {
                                        header.Cell().Element(HStyle).Text("FECHA ABONO");
                                        header.Cell().Element(HStyle).Text("Nº CUOTA");
                                        header.Cell().Element(HStyle).AlignRight().Text("MONTO PAGADO");
                                        header.Cell().Element(HStyle).Text("DETALLE");

                                        static IContainer HStyle(IContainer container) => container.BorderBottom(1).BorderColor("#cbd5e1").PaddingVertical(4).DefaultTextStyle(x => x.SemiBold().FontSize(8).FontColor("#475569"));
                                    });

                                    if (prestamo.Pagos.Any())
                                    {
                                        foreach (var pago in prestamo.Pagos)
                                        {
                                            table.Cell().Element(CStyle).Text($"{pago.Fecha:dd/MM/yyyy HH:mm}");
                                            table.Cell().Element(CStyle).Text($"Cuota #{pago.NumeroCuota}");
                                            table.Cell().Element(CStyle).AlignRight().Text($"₡{pago.Monto:N0}");
                                            table.Cell().Element(CStyle).Text("Abono registrado");

                                            static IContainer CStyle(IContainer container) => container.BorderBottom(1).BorderColor("#f1f5f9").PaddingVertical(4).DefaultTextStyle(x => x.FontSize(8.5f));
                                        }
                                    }
                                    else
                                    {
                                        table.Cell().ColumnSpan(4).PaddingVertical(8).AlignCenter().Text("No se han registrado abonos en este préstamo").FontSize(8.5f).Italic().FontColor(Colors.Grey.Medium);
                                    }
                                });

                                pc.Item().PaddingTop(10).AlignRight().Text(t =>
                                {
                                    t.Span("Saldo Pendiente del préstamo: ").FontSize(8.5f).FontColor("#64748B");
                                    t.Span($"₡{prestamo.SaldoPendiente:N0}").FontSize(10).Bold().FontColor("#0f172a");
                                });
                            });
                        });
                    }
                    });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Este documento es un estado de cuenta oficial emitido por ").FontSize(8).FontColor(Colors.Grey.Medium);
                    x.Span("CrediGest").FontSize(8).Bold().FontColor("#0f172a");
                });
            });
        });

        return document.GeneratePdf();
    }

    private void SummaryCard(IContainer container, string title, string value, string bgColor, string textColor)
    {
        container.Background(bgColor)
                 .Border(1)
                 .BorderColor("#e2e8f0")
                 .Row(row =>
                 {
                     row.ConstantItem(4).Background(textColor);
                     row.RelativeItem()
                        .PaddingVertical(10)
                        .PaddingHorizontal(14)
                        .Column(col =>
                        {
                            col.Item().Text(title).FontSize(7.5f).Bold().FontColor("#64748B").LetterSpacing(0.1f);
                            col.Item().PaddingTop(4).Text(value).FontSize(15).ExtraBold().FontColor(textColor);
                        });
                 });
    }
}
