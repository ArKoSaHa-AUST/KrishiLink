using System.Globalization;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KrishiLink.BLL.Services
{
    /// <summary>Owner identity printed on the statement header.</summary>
    public record StatementOwner(string Name, string? Business, string? Location);

    /// <summary>
    /// Bank-statement style monthly PDF built from the same <see cref="OwnerRevenueViewModel"/> that drives the
    /// Revenue page (scoped to one calendar month). Amounts use the "BDT" prefix because the bundled PDF font
    /// has no glyph for ৳.
    /// </summary>
    public class MonthlyStatementDocument : IDocument
    {
        private const string Brand = PdfStyle.Brand;
        private const string BrandLight = PdfStyle.BrandLight;
        private const string Muted = PdfStyle.Muted;
        private const string Border = PdfStyle.Border;

        private readonly OwnerRevenueViewModel _report;
        private readonly StatementOwner _owner;
        private readonly DateTime _month;
        private readonly List<RevenueTransactionItem> _completed;
        private readonly List<PayoutItem> _payouts;

        public MonthlyStatementDocument(OwnerRevenueViewModel report, StatementOwner owner, DateTime month)
        {
            _report = report;
            _owner = owner;
            _month = month;
            _completed = report.Transactions.Where(t => t.Status == BookingStatus.Completed).ToList();
            _payouts = report.Settlement.Payouts
                .Where(p => p.Date.Year == month.Year && p.Date.Month == month.Month)
                .OrderBy(p => p.Date)
                .ToList();
        }

        public string FileName => $"krishilink-statement-{_month:yyyy-MM}.pdf";

        private string Period => _month.ToString("MMMM yyyy", CultureInfo.InvariantCulture);

        private static string Money(decimal v) => PdfStyle.Money(v);

        public DocumentMetadata GetMetadata() => new()
        {
            Title = $"KrishiLink Statement — {Period}",
            Author = "KrishiLink",
            Subject = $"{_report.ListingLabel} owner monthly statement"
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
                    col.Item().Element(ComposeProfitAndLoss);
                    col.Item().Element(ComposeBreakdown);
                    col.Item().Element(ComposeTransactions);
                    if (_payouts.Count > 0) col.Item().Element(ComposePayouts);
                    col.Item().PaddingTop(6).Text(
                        "Farmers pay into KrishiLink escrow when a booking is accepted; revenue is recognised when the owner marks it completed. " +
                        "Utilization counts elapsed days only. Platform commission is deducted before payout. This statement is generated from KrishiLink booking and ledger records.")
                        .FontSize(7.5f).FontColor(Muted);
                });
                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text($"Generated {DateTime.Now:dd MMM yyyy HH:mm} · KrishiLink").FontSize(7.5f).FontColor(Muted);
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
                    col.Item().Text($"{_report.ListingLabel} Owner Monthly Statement").FontSize(11).SemiBold();
                    col.Item().PaddingTop(6).Text(_owner.Business ?? _owner.Name).SemiBold();
                    if (_owner.Business is not null) col.Item().Text(_owner.Name);
                    if (_owner.Location is not null) col.Item().Text(_owner.Location).FontColor(Muted);
                });
                row.ConstantItem(190).AlignRight().Column(col =>
                {
                    col.Item().AlignRight().Text(Period).FontSize(14).Bold();
                    col.Item().AlignRight().Text($"Statement no. ST-{_month:yyyy-MM}").FontColor(Muted);
                    col.Item().AlignRight().Text($"Period {_report.RangeStart:dd MMM} – {_report.RangeEnd:dd MMM yyyy}").FontColor(Muted);
                });
            });
        }

        private void ComposeSummary(IContainer container)
        {
            var gross = _completed.Sum(t => t.Gross);
            var commission = _completed.Sum(t => t.Commission);
            var expenses = _report.Transactions.Sum(t => t.Expenses);
            var upcoming = _report.Transactions.Where(t => BookingStatus.Confirmed.Contains(t.Status)).Sum(t => t.Gross);
            var paid = _payouts.Where(p => p.Status == PayoutStatus.Completed).Sum(p => p.Amount);

            container.Background(BrandLight).Padding(12).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Spacing(3);
                    Line(col, "Gross revenue (completed)", Money(gross));
                    Line(col, $"Platform commission ({_report.Settlement.CommissionRate * 100:0.#}%)", "- " + Money(commission));
                    Line(col, "Net earned", Money(gross - commission), bold: true);
                    Line(col, "Recorded expenses", "- " + Money(expenses));
                    Line(col, "Net profit", Money(gross - commission - expenses), bold: true, color: Brand);
                });
                row.ConstantItem(24);
                row.RelativeItem().Column(col =>
                {
                    col.Spacing(3);
                    Line(col, "Bookings completed", _completed.Count.ToString());
                    Line(col, "Bookings accepted/paid (upcoming)", Money(upcoming));
                    Line(col, "Held in escrow (paid, not completed)", Money(_report.Settlement.InEscrow));
                    Line(col, "Payouts received this month", Money(paid));
                    Line(col, "Owed to you (to date)", Money(_report.Settlement.Owed), bold: true);
                    Line(col, "Lifetime revenue", Money(_report.TotalRevenue));
                });
            });
        }

        private static void Line(ColumnDescriptor col, string label, string value, bool bold = false, string? color = null)
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Text(label).FontColor(Muted);
                var text = row.ConstantItem(110).AlignRight().Text(value);
                if (bold) text.SemiBold();
                if (color is not null) text.FontColor(color);
            });
        }

        private void ComposeProfitAndLoss(IContainer container)
        {
            var pnl = _report.ProfitAndLoss;
            if (pnl.ExpensesByCategory.Count == 0 && pnl.Gross == 0) return;

            container.Column(col =>
            {
                col.Item().Element(SectionTitle).Text("Profit & Loss / Expense Categories");
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(3);
                        c.ConstantColumn(80);
                        c.ConstantColumn(90);
                        c.ConstantColumn(90);
                    });
                    table.Header(h =>
                    {
                        h.Cell().Element(Th).Text("Category");
                        h.Cell().Element(Th).AlignRight().Text("Scope");
                        h.Cell().Element(Th).AlignRight().Text("Amount");
                        h.Cell().Element(Th).AlignRight().Text("Share");
                    });
                    foreach (var cat in pnl.ExpensesByCategory)
                    {
                        table.Cell().Element(Td).Text(cat.Category);
                        table.Cell().Element(Td).AlignRight().Text(cat.HasGeneralExpenses ? "General" : "Booking").FontColor(Muted);
                        table.Cell().Element(Td).AlignRight().Text(Money(cat.Amount));
                        table.Cell().Element(Td).AlignRight().Text($"{cat.PercentageOfExpenses:0.#}%");
                    }
                });
            });
        }

        private void ComposeBreakdown(IContainer container)
        {
            container.Column(col =>
            {
                col.Item().Element(SectionTitle).Text($"Revenue by {_report.ListingLabel}");
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(3);
                        c.ConstantColumn(60);
                        c.ConstantColumn(90);
                        c.ConstantColumn(90);
                        c.ConstantColumn(110);
                    });
                    table.Header(h =>
                    {
                        h.Cell().Element(Th).Text(_report.ListingLabel);
                        h.Cell().Element(Th).AlignRight().Text("Bookings");
                        h.Cell().Element(Th).AlignRight().Text("Revenue");
                        h.Cell().Element(Th).AlignRight().Text("Avg / booking");
                        h.Cell().Element(Th).AlignRight().Text("Utilization");
                    });
                    foreach (var b in _report.Breakdown)
                    {
                        table.Cell().Element(Td).Text(b.Name);
                        table.Cell().Element(Td).AlignRight().Text(b.Bookings.ToString());
                        table.Cell().Element(Td).AlignRight().Text(Money(b.Revenue));
                        table.Cell().Element(Td).AlignRight().Text(Money(b.AveragePerBooking));
                        table.Cell().Element(Td).AlignRight().Text($"{b.UtilizationPercent}%  ({b.BookedDays:0}/{b.PeriodDays} days)");
                    }
                });
            });
        }

        private void ComposeTransactions(IContainer container)
        {
            container.Column(col =>
            {
                col.Item().Element(SectionTitle).Text($"Transactions ({_report.Transactions.Count})");
                if (_report.Transactions.Count == 0)
                {
                    col.Item().PaddingTop(4).Text("No bookings in this period.").FontColor(Muted);
                    return;
                }
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(72);
                        c.ConstantColumn(30);
                        c.RelativeColumn(2);
                        c.RelativeColumn(3);
                        c.RelativeColumn(2);
                        c.ConstantColumn(66);
                        c.ConstantColumn(66);
                        c.ConstantColumn(52);
                        c.RelativeColumn(2);
                    });
                    table.Header(h =>
                    {
                        h.Cell().Element(Th).Text("Date");
                        h.Cell().Element(Th).Text("#");
                        h.Cell().Element(Th).Text("Customer");
                        h.Cell().Element(Th).Text(_report.ListingLabel);
                        h.Cell().Element(Th).Text("Quantity");
                        h.Cell().Element(Th).AlignRight().Text("Gross");
                        h.Cell().Element(Th).AlignRight().Text("Net");
                        h.Cell().Element(Th).Text("Status");
                        h.Cell().Element(Th).Text("Farmer payment");
                    });
                    foreach (var t in _report.Transactions.OrderBy(t => t.StartDate))
                    {
                        table.Cell().Element(Td).Text($"{t.StartDate:dd MMM} – {t.EndDate:dd MMM}");
                        table.Cell().Element(Td).Text(t.BookingId.ToString());
                        table.Cell().Element(Td).Text(t.CustomerName);
                        table.Cell().Element(Td).Text(t.ListingName);
                        table.Cell().Element(Td).Text(t.QuantityText);
                        table.Cell().Element(Td).AlignRight().Text(Money(t.Gross));
                        table.Cell().Element(Td).AlignRight().Text(Money(t.Net));
                        table.Cell().Element(Td).Text(t.Status);
                        table.Cell().Element(Td).Text(t.PaymentReference ?? (BookingStatus.Confirmed.Contains(t.Status) || t.Status == BookingStatus.Completed ? "unpaid" : "—")).FontSize(7.5f);
                    }
                });
            });
        }

        private void ComposePayouts(IContainer container)
        {
            container.Column(col =>
            {
                col.Item().Element(SectionTitle).Text("Payouts");
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(90);
                        c.RelativeColumn();
                        c.ConstantColumn(90);
                        c.ConstantColumn(110);
                    });
                    table.Header(h =>
                    {
                        h.Cell().Element(Th).Text("Date");
                        h.Cell().Element(Th).Text("Method");
                        h.Cell().Element(Th).Text("Status");
                        h.Cell().Element(Th).AlignRight().Text("Amount");
                    });
                    foreach (var p in _payouts)
                    {
                        table.Cell().Element(Td).Text(p.Date.ToString("dd MMM yyyy"));
                        table.Cell().Element(Td).Text(p.Method);
                        table.Cell().Element(Td).Text(p.Status);
                        table.Cell().Element(Td).AlignRight().Text(Money(p.Amount));
                    }
                });
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
