using System;
using System.Globalization;
using KrishiLink.Models.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KrishiLink.BLL.Services
{
    public record WarehouseReceiptModel(
        string ReceiptNumber,
        string Status,
        DateTime IntakeDate,
        DateTime? ReleasedOn,
        string? ReleasedTo,
        string? ReleaseRemarks,
        string GodownName,
        string StorageType,
        string GodownLocation,
        string? GodownDistrict,
        string OwnerName,
        string? OwnerBusiness,
        string OwnerPhone,
        string FarmerName,
        string FarmerLocation,
        string FarmerPhone,
        string Crop,
        string? Variety,
        int Bags,
        decimal BagWeightKg,
        decimal NetWeightKg,
        decimal? MoisturePercent,
        string Grade,
        string? Remarks,
        string BookingCode,
        double BookedStorageTons,
        DateTime BookingStartDate,
        DateTime BookingEndDate,
        decimal? AgreedRatePerTonMonth,
        string VerifyUrl,
        byte[] QrCodePngBytes
    );

    public class WarehouseReceiptDocument : IDocument
    {
        private const string Brand = "#2d6a4f";
        private const string BrandLight = "#eef7f2";
        private const string Muted = "#5e6e61";
        private const string Border = "#e2e8df";

        private readonly WarehouseReceiptModel _m;

        public WarehouseReceiptDocument(WarehouseReceiptModel model)
        {
            _m = model;
        }

        public string FileName => $"{_m.ReceiptNumber}.pdf";

        public DocumentMetadata GetMetadata() => new()
        {
            Title = $"Warehouse Receipt — {_m.ReceiptNumber}",
            Author = "KrishiLink",
            Subject = $"Storage Intake Warehouse Receipt for {_m.Crop} at {_m.GodownName}"
        };

        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Darken4));

                page.Header().Element(ComposeHeader);
                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(12);
                    col.Item().Element(ComposeParties);
                    col.Item().Element(ComposeGoodsDetails);
                    col.Item().Element(ComposeStorageTerms);
                    col.Item().Element(ComposeVerificationAndQr);
                    col.Item().Element(ComposeDisclaimer);
                });
                page.Footer().Element(ComposeFooter);
            });
        }

        private void ComposeHeader(IContainer container)
        {
            container.BorderBottom(1).BorderColor(Border).PaddingBottom(10).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("KrishiLink").FontSize(18).Bold().FontColor(Brand);
                    col.Item().Text("Warehouse Receipt (Non-Negotiable)").FontSize(12).SemiBold();
                    col.Item().PaddingTop(3).Text("Agricultural Storage Intake & Holding Certificate").FontSize(8).FontColor(Muted);
                });

                row.RelativeItem().AlignRight().Column(col =>
                {
                    var isStored = _m.Status == IntakeLotStatus.Stored;
                    var statusBg = isStored ? BrandLight : "#f8f9fa";
                    var statusText = isStored ? Brand : "#495057";

                    col.Item().AlignRight().Background(statusBg).Border(1).BorderColor(isStored ? Brand : "#ced4da")
                        .PaddingVertical(3).PaddingHorizontal(8).Text(isStored ? "STATUS: STORED" : $"STATUS: RELEASED ({_m.ReleasedOn:dd MMM yyyy})")
                        .FontSize(9).Bold().FontColor(statusText);

                    col.Item().PaddingTop(4).Text($"Receipt No: {_m.ReceiptNumber}").FontSize(10).Bold();
                    col.Item().Text($"Intake Date: {_m.IntakeDate:dd MMM yyyy}").FontSize(8).FontColor(Muted);
                });
            });
        }

        private void ComposeParties(IContainer container)
        {
            container.Row(row =>
            {
                // Godown & Warehouse Operator
                row.RelativeItem().Border(1).BorderColor(Border).Background(BrandLight).Padding(10).Column(col =>
                {
                    col.Spacing(3);
                    col.Item().Text("WAREHOUSE FACILITY & OPERATOR").FontSize(8).Bold().FontColor(Brand);
                    col.Item().Text(_m.GodownName).FontSize(11).Bold();
                    col.Item().Text($"Type: {_m.StorageType}").FontSize(8.5f);
                    col.Item().Text($"Location: {_m.GodownLocation}" + (string.IsNullOrWhiteSpace(_m.GodownDistrict) ? "" : $", {_m.GodownDistrict}")).FontSize(8.5f);
                    col.Item().PaddingTop(4).Text($"Operator: {_m.OwnerBusiness ?? _m.OwnerName}").FontSize(8.5f).SemiBold();
                    col.Item().Text($"Contact: {_m.OwnerPhone}").FontSize(8.5f).FontColor(Muted);
                });

                row.ConstantItem(12);

                // Depositor / Farmer
                row.RelativeItem().Border(1).BorderColor(Border).Background("#fafafa").Padding(10).Column(col =>
                {
                    col.Spacing(3);
                    col.Item().Text("DEPOSITOR / FARMER").FontSize(8).Bold().FontColor(Brand);
                    col.Item().Text(_m.FarmerName).FontSize(11).Bold();
                    col.Item().Text($"Location: {_m.FarmerLocation}").FontSize(8.5f);
                    col.Item().Text($"Phone: {_m.FarmerPhone}").FontSize(8.5f).FontColor(Muted);
                    col.Item().PaddingTop(4).Text($"Booking Reference: {_m.BookingCode}").FontSize(8.5f).SemiBold();
                    if (!string.IsNullOrWhiteSpace(_m.ReleasedTo))
                    {
                        col.Item().Text($"Released To: {_m.ReleasedTo}").FontSize(8.5f).FontColor(Brand);
                    }
                });
            });
        }

        private void ComposeGoodsDetails(IContainer container)
        {
            container.Border(1).BorderColor(Border).Padding(10).Column(col =>
            {
                col.Spacing(8);
                col.Item().Text("STORED PRODUCE & SPECIFICATIONS").FontSize(9).Bold().FontColor(Brand);

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background(BrandLight).Padding(4).Text("Crop & Variety").FontSize(8).Bold().FontColor(Brand);
                        header.Cell().Background(BrandLight).Padding(4).Text("Bag Count").FontSize(8).Bold().FontColor(Brand);
                        header.Cell().Background(BrandLight).Padding(4).Text("Bag Weight").FontSize(8).Bold().FontColor(Brand);
                        header.Cell().Background(BrandLight).Padding(4).Text("Net Weight").FontSize(8).Bold().FontColor(Brand);
                        header.Cell().Background(BrandLight).Padding(4).Text("Grade / Moisture").FontSize(8).Bold().FontColor(Brand);
                    });

                    table.Cell().BorderBottom(1).BorderColor(Border).Padding(4).Column(c =>
                    {
                        c.Item().Text(_m.Crop).Bold();
                        if (!string.IsNullOrWhiteSpace(_m.Variety))
                            c.Item().Text($"Variety: {_m.Variety}").FontSize(7.5f).FontColor(Muted);
                    });

                    table.Cell().BorderBottom(1).BorderColor(Border).Padding(4).Text($"{_m.Bags:N0} bags");
                    table.Cell().BorderBottom(1).BorderColor(Border).Padding(4).Text($"{_m.BagWeightKg:N1} kg");

                    table.Cell().BorderBottom(1).BorderColor(Border).Padding(4).Column(c =>
                    {
                        c.Item().Text($"{_m.NetWeightKg:N1} kg").Bold();
                        c.Item().Text($"({_m.NetWeightKg / 1000m:N3} Tons)").FontSize(7.5f).FontColor(Muted);
                    });

                    table.Cell().BorderBottom(1).BorderColor(Border).Padding(4).Column(c =>
                    {
                        c.Item().Text($"Grade: {_m.Grade}").Bold();
                        if (_m.MoisturePercent.HasValue)
                            c.Item().Text($"Moisture: {_m.MoisturePercent.Value:N1}%").FontSize(7.5f).FontColor(Muted);
                    });
                });

                if (!string.IsNullOrWhiteSpace(_m.Remarks))
                {
                    col.Item().PaddingTop(2).Text(t =>
                    {
                        t.Span("Intake Remarks: ").Bold().FontSize(8);
                        t.Span(_m.Remarks).FontSize(8).FontColor(Muted);
                    });
                }

                if (!string.IsNullOrWhiteSpace(_m.ReleaseRemarks))
                {
                    col.Item().Text(t =>
                    {
                        t.Span("Release Remarks: ").Bold().FontSize(8);
                        t.Span(_m.ReleaseRemarks).FontSize(8).FontColor(Muted);
                    });
                }
            });
        }

        private void ComposeStorageTerms(IContainer container)
        {
            container.Border(1).BorderColor(Border).Padding(10).Column(col =>
            {
                col.Spacing(6);
                col.Item().Text("STORAGE BOOKING TERMS").FontSize(9).Bold().FontColor(Brand);

                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Booked Allocation:").FontSize(8).FontColor(Muted);
                        c.Item().Text($"{_m.BookedStorageTons:N0} Metric Tons").FontSize(9).Bold();
                    });

                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Storage Period:").FontSize(8).FontColor(Muted);
                        c.Item().Text($"{_m.BookingStartDate:dd MMM yyyy} – {_m.BookingEndDate:dd MMM yyyy}").FontSize(9).Bold();
                    });

                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Agreed Rate:").FontSize(8).FontColor(Muted);
                        c.Item().Text(_m.AgreedRatePerTonMonth.HasValue
                            ? $"BDT {_m.AgreedRatePerTonMonth.Value:N0} / ton / mo"
                            : "As per contract").FontSize(9).Bold();
                    });

                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Platform Escrow:").FontSize(8).FontColor(Muted);
                        c.Item().Text("Secured via KrishiLink").FontSize(9).Bold().FontColor(Brand);
                    });
                });
            });
        }

        private void ComposeVerificationAndQr(IContainer container)
        {
            container.Border(1).BorderColor(Border).Background("#fcfdfc").Padding(10).Row(row =>
            {
                if (_m.QrCodePngBytes != null && _m.QrCodePngBytes.Length > 0)
                {
                    row.ConstantItem(75).Column(c =>
                    {
                        c.Item().Width(70).Height(70).Image(_m.QrCodePngBytes);
                    });
                }

                row.RelativeItem().PaddingLeft(10).Column(col =>
                {
                    col.Spacing(3);
                    col.Item().Text("INSTANT DIGITAL VERIFICATION").FontSize(8.5f).Bold().FontColor(Brand);
                    col.Item().Text("Scan this QR code with any smartphone or camera to verify authenticity, current holding status, and physical goods details directly on the KrishiLink public ledger.").FontSize(8).FontColor(Muted);
                    col.Item().PaddingTop(3).Text(_m.VerifyUrl).FontSize(8).SemiBold().FontColor(Brand);
                });
            });
        }

        private void ComposeDisclaimer(IContainer container)
        {
            container.Background(BrandLight).Border(1).BorderColor(Border).Padding(8).Column(col =>
            {
                col.Spacing(2);
                col.Item().Text("LEGAL NOTICE & NON-NEGOTIABLE DECLARATION").FontSize(7.5f).Bold().FontColor(Brand);
                col.Item().Text(
                    "This receipt identifies goods held in physical storage at the specified warehouse facility and is not a negotiable instrument or a document of title. " +
                    "Transfer of this document does not transfer ownership of the underlying goods. Goods are subject to warehouse lien and storage terms agreed on the KrishiLink platform. " +
                    "Verify authenticity at the QR link.")
                    .FontSize(7f).FontColor(Muted);
            });
        }

        private void ComposeFooter(IContainer container)
        {
            container.BorderTop(1).BorderColor(Border).PaddingTop(6).Row(row =>
            {
                row.RelativeItem().Text($"Generated {DateTime.UtcNow:dd MMM yyyy HH:mm} UTC · KrishiLink Agricultural Platform").FontSize(7.5f).FontColor(Muted);
                row.RelativeItem().AlignRight().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(7.5f).FontColor(Muted));
                    t.Span("Page ");
                    t.CurrentPageNumber();
                    t.Span(" of ");
                    t.TotalPages();
                });
            });
        }
    }
}
