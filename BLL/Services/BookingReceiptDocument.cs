using System;
using System.Globalization;
using KrishiLink.Models.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KrishiLink.BLL.Services
{
    public record BookingReceiptModel(
        string ReceiptNumber,
        string BookingCode,
        string BookingType,
        int BookingId,
        DateTime IssueDate,
        bool IsRefunded,
        DateTime? RefundedOn,
        string FarmerName,
        string? FarmerLocation,
        string? FarmerPhone,
        string? FarmerEmail,
        string OwnerName,
        string? OwnerBusiness,
        string? OwnerLocation,
        string? OwnerPhone,
        string ListingName,
        string ListingCategory,
        string? ListingLocation,
        DateTime StartDate,
        DateTime EndDate,
        string QuantityDisplay,
        string RateDescription,
        decimal AgreedGross,
        decimal AmountPaid,
        string PaymentMethod,
        string PaymentReference,
        string? GatewayReference,
        DateTime PaidOn,
        decimal DiscountAmount,
        int PointsUsed,
        string? PromoCode,
        string Status,
        string VerifyUrl,
        byte[] QrCodePngBytes
    );

    /// <summary>
    /// Official print-ready A4 PDF payment receipt generated for farmers (and listing owners)
    /// representing verified escrow payments and ledger entries.
    /// </summary>
    public class BookingReceiptDocument : IDocument
    {
        private const string Brand = PdfStyle.Brand;
        private const string BrandLight = PdfStyle.BrandLight;
        private const string Muted = PdfStyle.Muted;
        private const string Border = PdfStyle.Border;

        private readonly BookingReceiptModel _model;

        public BookingReceiptDocument(BookingReceiptModel model)
        {
            _model = model;
        }

        public string FileName => $"{_model.ReceiptNumber.ToLowerInvariant()}.pdf";

        private static string Money(decimal v) => PdfStyle.Money(v);

        public DocumentMetadata GetMetadata() => new()
        {
            Title = $"KrishiLink Payment Receipt — {_model.ReceiptNumber}",
            Author = "KrishiLink",
            Subject = $"Payment receipt for {_model.BookingCode} ({_model.ListingName})"
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
                    col.Spacing(14);
                    col.Item().Element(ComposeParties);
                    col.Item().Element(ComposeBookingDetails);
                    col.Item().Element(ComposeFinancials);
                    col.Item().Element(ComposeVerification);
                });
                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text($"Generated {DateTime.UtcNow:dd MMM yyyy HH:mm} UTC · KrishiLink Escrow Platform").FontSize(7.5f).FontColor(Muted);
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
                    col.Item().Text("Official Payment Receipt").FontSize(12).SemiBold();
                    col.Item().PaddingTop(4).Text($"Booking Reference: {_model.BookingCode}").FontSize(9.5f).FontColor(Muted);
                });

                row.ConstantItem(210).AlignRight().Column(col =>
                {
                    col.Item().AlignRight().Text(_model.ReceiptNumber).FontSize(13).Bold().FontColor(Brand);
                    col.Item().AlignRight().Text($"Issued: {_model.IssueDate:dd MMM yyyy}").FontSize(8.5f).FontColor(Muted);

                    if (_model.IsRefunded)
                    {
                        col.Item().PaddingTop(4).AlignRight().Background(Colors.Red.Lighten5).Border(1).BorderColor(Colors.Red.Medium)
                            .PaddingHorizontal(8).PaddingVertical(2)
                            .Text("REFUNDED").FontSize(10).Bold().FontColor(Colors.Red.Medium);
                    }
                    else
                    {
                        col.Item().PaddingTop(4).AlignRight().Background(BrandLight).Border(1).BorderColor(Border)
                            .PaddingHorizontal(8).PaddingVertical(2)
                            .Text("PAYMENT CONFIRMED").FontSize(8.5f).Bold().FontColor(Brand);
                    }
                });
            });
        }

        private void ComposeParties(IContainer container)
        {
            container.Background(BrandLight).Padding(12).Column(mainCol =>
            {
                mainCol.Item().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("PAID BY (FARMER)").FontSize(8).Bold().FontColor(Muted);
                        col.Item().Text(_model.FarmerName).FontSize(10).Bold();
                        if (!string.IsNullOrEmpty(_model.FarmerLocation))
                            col.Item().Text(_model.FarmerLocation).FontSize(8.5f);
                        if (!string.IsNullOrEmpty(_model.FarmerPhone))
                            col.Item().Text(_model.FarmerPhone).FontSize(8.5f).FontColor(Muted);
                        if (!string.IsNullOrEmpty(_model.FarmerEmail))
                            col.Item().Text(_model.FarmerEmail).FontSize(8.5f).FontColor(Muted);
                    });

                    row.ConstantItem(20);

                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("PAID TO (VIA KRISHILINK ESCROW)").FontSize(8).Bold().FontColor(Muted);
                        col.Item().Text(_model.OwnerBusiness ?? _model.OwnerName).FontSize(10).Bold();
                        if (_model.OwnerBusiness != null && _model.OwnerName != _model.OwnerBusiness)
                            col.Item().Text(_model.OwnerName).FontSize(8.5f);
                        if (!string.IsNullOrEmpty(_model.OwnerLocation))
                            col.Item().Text(_model.OwnerLocation).FontSize(8.5f);
                        if (!string.IsNullOrEmpty(_model.OwnerPhone))
                            col.Item().Text(_model.OwnerPhone).FontSize(8.5f).FontColor(Muted);
                    });
                });

                mainCol.Item().PaddingTop(8).BorderTop(1).BorderColor(Border).Text(
                    "Held in escrow until the owner completes the booking. Platform commission is deducted at final settlement.")
                    .FontSize(7.5f).Italic().FontColor(Muted);
            });
        }

        private void ComposeBookingDetails(IContainer container)
        {
            container.Column(col =>
            {
                col.Item().Element(SectionTitle).Text("Booking Details");
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(3);
                        c.RelativeColumn(3);
                        c.RelativeColumn(2);
                        c.RelativeColumn(3);
                    });
                    table.Header(h =>
                    {
                        h.Cell().Element(Th).Text(_model.BookingType == "Godown" ? "Warehouse / Facility" : "Machinery / Equipment");
                        h.Cell().Element(Th).Text("Booked Period");
                        h.Cell().Element(Th).Text("Quantity / Units");
                        h.Cell().Element(Th).Text("Agreed Rate");
                    });
                    table.Cell().Element(Td).Column(c =>
                    {
                        c.Item().Text(_model.ListingName).Bold();
                        c.Item().Text($"{_model.ListingCategory} · {_model.ListingLocation ?? "Location on file"}").FontSize(8).FontColor(Muted);
                    });
                    table.Cell().Element(Td).Text($"{_model.StartDate:dd MMM yyyy} – {_model.EndDate:dd MMM yyyy}");
                    table.Cell().Element(Td).Text(_model.QuantityDisplay);
                    table.Cell().Element(Td).Text(_model.RateDescription);
                });
            });
        }

        private void ComposeFinancials(IContainer container)
        {
            container.Column(col =>
            {
                col.Item().Element(SectionTitle).Text("Payment & Ledger Record");
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(3);
                        c.RelativeColumn(4);
                        c.ConstantColumn(120);
                    });
                    table.Header(h =>
                    {
                        h.Cell().Element(Th).Text("Item / Description");
                        h.Cell().Element(Th).Text("Transaction Reference / Details");
                        h.Cell().Element(Th).AlignRight().Text("Amount");
                    });

                    // Agreed Gross row
                    table.Cell().Element(Td).Text("Agreed Gross Booking Total").SemiBold();
                    table.Cell().Element(Td).Text($"Snapshot agreed upon booking acceptance ({_model.QuantityDisplay})").FontSize(8).FontColor(Muted);
                    table.Cell().Element(Td).AlignRight().Text(Money(_model.AgreedGross)).SemiBold();

                    // Amount Paid row
                    table.Cell().Element(Td).Text("Amount Paid (Platform Escrow)").Bold().FontColor(Brand);
                    table.Cell().Element(Td).Column(tc =>
                    {
                        tc.Item().Text($"{_model.PaymentMethod} · Ref: {_model.PaymentReference}").FontSize(8.5f).Bold();
                        if (!string.IsNullOrEmpty(_model.GatewayReference))
                            tc.Item().Text($"Gateway Txn: {_model.GatewayReference} · Paid {_model.PaidOn:dd MMM yyyy HH:mm} UTC").FontSize(7.5f).FontColor(Muted);
                    });
                    table.Cell().Element(Td).AlignRight().Text(Money(_model.AmountPaid)).Bold().FontColor(Brand);

                    // Loyalty benefit row (informational, never subtracted from Payment.Amount)
                    if (_model.DiscountAmount > 0)
                    {
                        table.Cell().Element(Td).Text("Loyalty Benefit Recorded").FontColor(Muted);
                        table.Cell().Element(Td).Text($"Informational: {_model.PointsUsed} points used{(string.IsNullOrEmpty(_model.PromoCode) ? "" : $", promo {_model.PromoCode}")} (promo/points)").FontSize(8).FontColor(Muted);
                        table.Cell().Element(Td).AlignRight().Text($"BDT {_model.DiscountAmount:N0} (benefit)").FontSize(8).FontColor(Muted);
                    }

                    // Refund row if refunded
                    if (_model.IsRefunded)
                    {
                        var refDate = _model.RefundedOn ?? _model.IssueDate;
                        table.Cell().Element(Td).Text("Full Escrow Refund Processed").Bold().FontColor(Colors.Red.Medium);
                        table.Cell().Element(Td).Text($"Refunded on {refDate:dd MMM yyyy HH:mm} UTC via original payment channel").FontSize(8).FontColor(Colors.Red.Medium);
                        table.Cell().Element(Td).AlignRight().Text($"- {Money(_model.AmountPaid)}").Bold().FontColor(Colors.Red.Medium);
                    }
                });

                col.Item().PaddingTop(6).Background(Colors.Grey.Lighten4).Padding(8).Row(r =>
                {
                    r.RelativeItem().Text(t =>
                    {
                        t.Span("Current Booking Status: ").Bold();
                        t.Span(_model.Status).Bold().FontColor(_model.IsRefunded ? Colors.Red.Medium : Brand);
                    });
                    r.RelativeItem().AlignRight().Text(t =>
                    {
                        t.Span("Net Escrow Balance: ").Bold();
                        t.Span(_model.IsRefunded ? "BDT 0" : Money(_model.AmountPaid)).Bold().FontColor(_model.IsRefunded ? Colors.Red.Medium : Brand);
                    });
                });
            });
        }

        private void ComposeVerification(IContainer container)
        {
            container.BorderTop(1).BorderColor(Border).PaddingTop(10).Row(row =>
            {
                if (_model.QrCodePngBytes.Length > 0)
                {
                    row.ConstantItem(68).Image(_model.QrCodePngBytes);
                }

                row.RelativeItem().PaddingLeft(12).Column(col =>
                {
                    col.Item().Text("Scan to verify this payment receipt online:").FontSize(8.5f).SemiBold();
                    col.Item().Text(_model.VerifyUrl).FontSize(8).FontColor(Brand).Underline();
                    col.Item().PaddingTop(4).Text(
                        "This receipt is an official platform record generated from KrishiLink payment and double-entry ledger postings. " +
                        "Escrow payments are strictly held by the platform until the provider marks the booking completed.")
                        .FontSize(7.5f).FontColor(Muted);
                });
            });
        }

        private static IContainer SectionTitle(IContainer c) =>
            c.PaddingBottom(4).DefaultTextStyle(x => x.FontSize(10.5f).SemiBold().FontColor(Brand));

        private static IContainer Th(IContainer c) =>
            c.Background(BrandLight).BorderBottom(1).BorderColor(Border).PaddingVertical(4).PaddingHorizontal(5)
             .DefaultTextStyle(x => x.SemiBold().FontSize(8).FontColor(Muted));

        private static IContainer Td(IContainer c) =>
            c.BorderBottom(1).BorderColor(Border).PaddingVertical(4).PaddingHorizontal(5);
    }
}
