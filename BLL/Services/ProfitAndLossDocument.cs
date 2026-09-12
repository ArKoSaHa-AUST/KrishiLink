using System;
using System.Globalization;
using KrishiLink.Models.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KrishiLink.BLL.Services
{
    /// <summary>
    /// One-page printable PDF statement summarizing an owner's gross revenue, platform commissions,
    /// categorized operating expenses, and net profit for a given period or Bangladesh fiscal year.
    /// </summary>
    public class ProfitAndLossDocument : IDocument
    {
        private const string Brand = PdfStyle.Brand;
        private const string BrandLight = PdfStyle.BrandLight;
        private const string Muted = PdfStyle.Muted;
        private const string Border = PdfStyle.Border;

        private readonly ProfitAndLossViewModel _pnl;
        private readonly StatementOwner _owner;
        private readonly string _listingLabel;

        public ProfitAndLossDocument(ProfitAndLossViewModel pnl, StatementOwner owner, string listingLabel)
        {
            _pnl = pnl;
            _owner = owner;
            _listingLabel = listingLabel;
        }

        public string FileName => $"krishilink-pnl-{_pnl.From:yyyyMMdd}-{_pnl.To:yyyyMMdd}.pdf";

        private static string Money(decimal v) => PdfStyle.Money(v);

        public DocumentMetadata GetMetadata() => new()
        {
            Title = $"KrishiLink P&L Report — {_pnl.PeriodLabel}",
            Author = "KrishiLink",
            Subject = $"{_listingLabel} Owner Profit & Loss and Tax Summary"
        };

        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Darken4));

                page.Header().Element(ComposeHeader);
                page.Content().PaddingVertical(14).Column(col =>
                {
                    col.Spacing(16);
                    col.Item().Element(ComposeSummary);
                    col.Item().Element(ComposeCategoryBreakdown);
                    col.Item().Element(ComposeTaxAdvisory);
                });
                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text($"Generated {DateTime.UtcNow:dd MMM yyyy HH:mm} UTC · KrishiLink Reporting").FontSize(7.5f).FontColor(Muted);
                    row.RelativeItem().AlignRight().Text(t =>
                    {
                        t.DefaultTextStyle(s => s.FontSize(7.5f).FontColor(Muted));
                        t.Span("Page ");
                        t.CurrentPageNumber();
                        t.Span(" of ");
                        t.TotalPages();
                    });
                });
            });
        }

        private void ComposeHeader(IContainer container)
        {
            container.BorderBottom(1).BorderColor(Border).PaddingBottom(10).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("KrishiLink").FontSize(18).Bold().FontColor(Brand);
                    col.Item().Text($"{_listingLabel} Profit & Loss / Tax Summary").FontSize(12).SemiBold();
                    col.Item().PaddingTop(4).Text(_owner.Business ?? _owner.Name).SemiBold();
                    if (_owner.Business != null && _owner.Name != _owner.Business)
                        col.Item().Text(_owner.Name).FontSize(8.5f);
                    if (!string.IsNullOrEmpty(_owner.Location))
                        col.Item().Text(_owner.Location).FontSize(8.5f).FontColor(Muted);
                });

                row.ConstantItem(220).AlignRight().Column(col =>
                {
                    col.Item().AlignRight().Text(_pnl.PeriodLabel).FontSize(11).Bold().FontColor(Brand);
                    col.Item().AlignRight().Text($"Range: {_pnl.From:dd MMM yyyy} – {_pnl.To:dd MMM yyyy}").FontSize(8.5f).FontColor(Muted);
                    col.Item().PaddingTop(4).AlignRight().Text($"Bookings Completed: {_pnl.CompletedBookingsCount}").FontSize(8.5f);
                    col.Item().AlignRight().Text($"Expenses Recorded: {_pnl.ExpenseCount}").FontSize(8.5f);
                });
            });
        }

        private void ComposeSummary(IContainer container)
        {
            container.Background(BrandLight).Padding(12).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Spacing(4);
                    col.Item().Text("FINANCIAL OVERVIEW").FontSize(8).Bold().FontColor(Muted);
                    Line(col, "Gross Revenue (completed in period)", Money(_pnl.Gross));
                    Line(col, "Platform Commission (tax-deductible)", "- " + Money(_pnl.Commission));
                    Line(col, "Net Earned Revenue", Money(_pnl.NetEarned), bold: true);
                });

                row.ConstantItem(24);

                row.RelativeItem().Column(col =>
                {
                    col.Spacing(4);
                    col.Item().Text("PROFITABILITY & MARGIN").FontSize(8).Bold().FontColor(Muted);
                    Line(col, "Total Operating Expenses", "- " + Money(_pnl.TotalExpenses));
                    Line(col, "Net Operating Profit", Money(_pnl.NetProfit), bold: true, color: Brand);
                    Line(col, "Operating Profit Margin", $"{_pnl.MarginPercent:0.#}%", bold: true);
                });
            });
        }

        private static void Line(ColumnDescriptor col, string label, string value, bool bold = false, string? color = null)
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Text(label).FontColor(Muted);
                var text = row.ConstantItem(100).AlignRight().Text(value);
                if (bold) text.SemiBold();
                if (color is not null) text.FontColor(color);
            });
        }

        private void ComposeCategoryBreakdown(IContainer container)
        {
            container.Column(col =>
            {
                col.Item().Element(SectionTitle).Text("Operating Expenses by Category");

                if (_pnl.ExpensesByCategory.Count == 0)
                {
                    col.Item().PaddingTop(6).Text("No expenses recorded within this period.").FontColor(Muted);
                    return;
                }

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(3);
                        c.ConstantColumn(80);
                        c.ConstantColumn(80);
                        c.ConstantColumn(100);
                        c.ConstantColumn(90);
                    });
                    table.Header(h =>
                    {
                        h.Cell().Element(Th).Text("Category");
                        h.Cell().Element(Th).AlignRight().Text("Count");
                        h.Cell().Element(Th).AlignRight().Text("Scope");
                        h.Cell().Element(Th).AlignRight().Text("Amount");
                        h.Cell().Element(Th).AlignRight().Text("Share");
                    });
                    foreach (var cat in _pnl.ExpensesByCategory)
                    {
                        table.Cell().Element(Td).Text(cat.Category).Bold();
                        table.Cell().Element(Td).AlignRight().Text(cat.Count.ToString());
                        table.Cell().Element(Td).AlignRight().Text(cat.HasGeneralExpenses ? "General" : "Booking").FontColor(Muted);
                        table.Cell().Element(Td).AlignRight().Text(Money(cat.Amount));
                        table.Cell().Element(Td).AlignRight().Text($"{cat.PercentageOfExpenses:0.#}%");
                    }

                    // Total row
                    table.Cell().Element(Td).Text("Total Operating Expenses").Bold();
                    table.Cell().Element(Td).AlignRight().Text(_pnl.ExpenseCount.ToString()).Bold();
                    table.Cell().Element(Td).AlignRight().Text("—");
                    table.Cell().Element(Td).AlignRight().Text(Money(_pnl.TotalExpenses)).Bold();
                    table.Cell().Element(Td).AlignRight().Text("100.0%").Bold();
                });
            });
        }

        private void ComposeTaxAdvisory(IContainer container)
        {
            container.Background(Colors.Grey.Lighten4).Border(1).BorderColor(Border).Padding(10).Column(col =>
            {
                col.Item().Text("Tax Advisory Note").FontSize(8.5f).Bold().FontColor(Muted);
                col.Item().PaddingTop(2).Text(
                    "Figures for your tax adviser: gross receipts, platform commission (deductible), operating expenses by category, net profit. " +
                    "KrishiLink does not calculate tax liability or give tax advice. Retain all supporting receipts, vouchers, and bills for tax submission.")
                    .FontSize(7.5f).FontColor(Muted);
            });
        }

        private static IContainer SectionTitle(IContainer c) =>
            c.PaddingBottom(4).DefaultTextStyle(x => x.FontSize(11).SemiBold().FontColor(Brand));

        private static IContainer Th(IContainer c) =>
            c.Background(BrandLight).BorderBottom(1).BorderColor(Border).PaddingVertical(4).PaddingHorizontal(5)
             .DefaultTextStyle(x => x.SemiBold().FontSize(8).FontColor(Muted));

        private static IContainer Td(IContainer c) =>
            c.BorderBottom(1).BorderColor(Border).PaddingVertical(4).PaddingHorizontal(5);
    }
}
