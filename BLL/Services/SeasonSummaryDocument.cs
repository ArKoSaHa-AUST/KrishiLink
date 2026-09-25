using System.Globalization;
using KrishiLink.BLL.Helpers;
using KrishiLink.Models.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KrishiLink.BLL.Services
{
    /// <summary>
    /// One-page season summary for a farmer (ECO-01): rental and storage costs from the plan, off-platform costs, and the
    /// expected return from the seeded yield range at the farmer's own price. English only, like the other statements:
    /// the bundled PDF fonts have no Bangla or Taka glyphs.
    /// </summary>
    public sealed class SeasonSummaryDocument : IDocument
    {
        private const string Brand = PdfStyle.Brand;
        private const string BrandLight = PdfStyle.BrandLight;
        private const string Muted = PdfStyle.Muted;
        private const string Border = PdfStyle.Border;

        private readonly SeasonSheet _sheet;
        private readonly string _farmerName;
        private readonly DateTime _generatedUtc;

        public SeasonSummaryDocument(SeasonSheet sheet, string farmerName, DateTime generatedUtc)
        {
            _sheet = sheet;
            _farmerName = farmerName;
            _generatedUtc = generatedUtc;
        }

        public string FileName => $"krishilink-season-{_sheet.Plan.Id}-{_generatedUtc:yyyyMMdd}.pdf";

        private static string Money(decimal v) => PdfStyle.Money(v);
        private static string Kg(double kg) => $"{kg.ToString("N0", CultureInfo.InvariantCulture)} kg ({UnitFormat.KgToMaund(kg).ToString("N0", CultureInfo.InvariantCulture)} maund)";
        private static string Land(double decimals) =>
            $"{decimals.ToString("0.##", CultureInfo.InvariantCulture)} decimal ({UnitFormat.FromDecimals(decimals, LandUnit.Bigha).ToString("0.##", CultureInfo.InvariantCulture)} bigha, {UnitFormat.DecimalsToHectares(decimals).ToString("0.00", CultureInfo.InvariantCulture)} ha)";

        public DocumentMetadata GetMetadata() => new()
        {
            Title = $"KrishiLink Season Summary — {_sheet.Plan.Name}",
            Author = "KrishiLink",
            Subject = "Farmer season costs and expected return"
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
                    col.Item().Element(ComposeOverview);
                    col.Item().Element(ComposePlatformCosts);
                    col.Item().Element(ComposeOwnCosts);
                    col.Item().Element(ComposeReturn);
                    col.Item().Element(ComposeNote);
                });
                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text($"Generated {_generatedUtc:dd MMM yyyy HH:mm} UTC · KrishiLink").FontSize(7.5f).FontColor(Muted);
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
                    col.Item().Text("Season Summary").FontSize(12).SemiBold();
                    col.Item().PaddingTop(4).Text(_sheet.Plan.Name).SemiBold();
                    col.Item().Text(_farmerName).FontSize(8.5f).FontColor(Muted);
                });
                row.ConstantItem(230).AlignRight().Column(col =>
                {
                    col.Item().AlignRight().Text(_sheet.Crop?.Name ?? _sheet.Plan.Crop ?? "Crop not set").FontSize(11).Bold().FontColor(Brand);
                    if (_sheet.LandDecimals is { } land)
                        col.Item().AlignRight().Text(Land(land)).FontSize(8.5f).FontColor(Muted);
                    if (_sheet.Crop?.Source is { Length: > 0 } source)
                        col.Item().AlignRight().Text($"Yield data: {source} crop calendar").FontSize(8.5f).FontColor(Muted);
                });
            });
        }

        private void ComposeOverview(IContainer container)
        {
            container.Background(BrandLight).Padding(12).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Spacing(4);
                    col.Item().Text("SEASON COSTS").FontSize(8).Bold().FontColor(Muted);
                    Line(col, "Rentals and storage (KrishiLink)", Money(_sheet.PlatformTotal));
                    Line(col, "Other costs you recorded", Money(_sheet.OwnTotal));
                    Line(col, "Total season cost", Money(_sheet.TotalCost), bold: true);
                    if (_sheet.CostPerAcre is { } perAcre) Line(col, "Cost per acre", Money(perAcre));
                });
                row.ConstantItem(24);
                row.RelativeItem().Column(col =>
                {
                    col.Spacing(4);
                    col.Item().Text("EXPECTED RETURN").FontSize(8).Bold().FontColor(Muted);
                    if (_sheet.GrossMin is { } gMin && _sheet.GrossMax is { } gMax && _sheet.NetMin is { } nMin && _sheet.NetMax is { } nMax)
                    {
                        Line(col, "Expected gross", $"{Money(gMin)} – {Money(gMax)}");
                        Line(col, "Expected net", $"{Money(nMin)} – {Money(nMax)}", bold: true, color: nMax < 0 ? Colors.Red.Darken2 : Brand);
                    }
                    else
                    {
                        col.Item().Text("Add the crop, land size and your expected price to see the return.").FontColor(Muted);
                    }
                    if (_sheet.BreakEvenKg is { } breakEven) Line(col, "Break-even harvest", Kg(breakEven));
                });
            });
        }

        private void ComposePlatformCosts(IContainer container)
        {
            container.Column(col =>
            {
                col.Item().Element(SectionTitle).Text("Rentals and storage booked on KrishiLink");
                if (_sheet.PlatformCosts.Count == 0)
                {
                    col.Item().PaddingTop(6).Text("No rentals or storage in this plan.").FontColor(Muted);
                    return;
                }
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(3);
                        c.RelativeColumn(2);
                        c.ConstantColumn(110);
                        c.ConstantColumn(90);
                    });
                    table.Header(h =>
                    {
                        h.Cell().Element(Th).Text("Item");
                        h.Cell().Element(Th).Text("Type");
                        h.Cell().Element(Th).Text("Dates");
                        h.Cell().Element(Th).AlignRight().Text("Amount");
                    });
                    foreach (var c in _sheet.PlatformCosts)
                    {
                        table.Cell().Element(Td).Text(c.Title);
                        table.Cell().Element(Td).Text(c.Category).FontColor(Muted);
                        table.Cell().Element(Td).Text($"{c.Start:dd MMM} – {c.End:dd MMM yyyy}");
                        table.Cell().Element(Td).AlignRight().Text(Money(c.Amount) + (c.IsEstimate ? " *" : string.Empty));
                    }
                });
                if (_sheet.HasEstimates)
                    col.Item().PaddingTop(4).Text("* Estimate from the current price; the agreed price replaces it once the owner accepts.").FontSize(7.5f).FontColor(Muted);
            });
        }

        private void ComposeOwnCosts(IContainer container)
        {
            container.Column(col =>
            {
                col.Item().Element(SectionTitle).Text("Other costs you recorded");
                if (_sheet.OwnCosts.Count == 0)
                {
                    col.Item().PaddingTop(6).Text("No other costs recorded.").FontColor(Muted);
                    return;
                }
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(80);
                        c.RelativeColumn(2);
                        c.RelativeColumn(3);
                        c.ConstantColumn(90);
                    });
                    table.Header(h =>
                    {
                        h.Cell().Element(Th).Text("Date");
                        h.Cell().Element(Th).Text("Type");
                        h.Cell().Element(Th).Text("Note");
                        h.Cell().Element(Th).AlignRight().Text("Amount");
                    });
                    foreach (var c in _sheet.OwnCosts)
                    {
                        table.Cell().Element(Td).Text(c.IncurredOn.ToString("dd MMM yyyy", CultureInfo.InvariantCulture));
                        table.Cell().Element(Td).Text(c.Category == SeasonCostCategories.LandLease ? "Land lease" : c.Category);
                        table.Cell().Element(Td).Text(c.Note).FontColor(Muted);
                        table.Cell().Element(Td).AlignRight().Text(Money(c.Amount));
                    }
                });
            });
        }

        private void ComposeReturn(IContainer container)
        {
            container.Column(col =>
            {
                col.Item().Element(SectionTitle).Text("How the return is worked out");
                if (_sheet.YieldMinKg is { } min && _sheet.YieldMaxKg is { } max && _sheet.Price is { } price)
                {
                    col.Item().Text($"Expected harvest {Kg(min)} to {Kg(max)} (seeded range for this crop × your land) " +
                                    $"at your price of BDT {price.ToString("N2", CultureInfo.InvariantCulture)} per kg.");
                    if (_sheet.BreakEvenKgPerAcre is { } perAcre)
                        col.Item().PaddingTop(2).Text($"To cover all costs you need about {perAcre.ToString("N0", CultureInfo.InvariantCulture)} kg per acre.");
                }
                else
                {
                    col.Item().Text("Not enough information yet: the return needs one crop with a seeded yield range, your land size and your expected price.").FontColor(Muted);
                }
            });
        }

        private static void ComposeNote(IContainer container)
        {
            container.Background(Colors.Grey.Lighten4).Border(1).BorderColor(Border).Padding(10).Column(col =>
            {
                col.Item().Text("About these figures").FontSize(8.5f).Bold().FontColor(Muted);
                col.Item().PaddingTop(2).Text(
                    "The selling price is your own estimate, not a market price. Yields are the indicative range from the crop calendar; " +
                    "weather, pests and management change the real harvest. 1 maund = 40 kg; 1 bigha = 33 decimal.")
                    .FontSize(7.5f).FontColor(Muted);
            });
        }

        private static void Line(ColumnDescriptor col, string label, string value, bool bold = false, string? color = null)
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Text(label).FontColor(Muted);
                var text = row.ConstantItem(150).AlignRight().Text(value);
                if (bold) text.SemiBold();
                if (color is not null) text.FontColor(color);
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
