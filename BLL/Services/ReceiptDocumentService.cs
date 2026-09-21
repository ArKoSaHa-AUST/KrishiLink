using System;
using System.Threading.Tasks;
using KrishiLink.DAL.Repositories;
using KrishiLink.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;

namespace KrishiLink.BLL.Services
{
    public interface IReceiptDocumentService
    {
        /// <summary>
        /// Generates the official A4 payment receipt PDF bytes and file name for a paid or refunded booking.
        /// Returns null if the booking or valid payment record does not exist.
        /// </summary>
        Task<(byte[] Content, string FileName)?> BuildAsync(string bookingType, int bookingId, string? requestHost = null);
    }

    public class ReceiptDocumentService : IReceiptDocumentService
    {
        private readonly IRepository<EquipmentBooking> _equipmentBookings;
        private readonly IRepository<GodownBooking> _godownBookings;
        private readonly IQrCodeService _qrCode;
        private readonly AppOptions _options;

        public ReceiptDocumentService(
            IRepository<EquipmentBooking> equipmentBookings,
            IRepository<GodownBooking> godownBookings,
            IQrCodeService qrCode,
            IOptions<AppOptions> options)
        {
            _equipmentBookings = equipmentBookings;
            _godownBookings = godownBookings;
            _qrCode = qrCode;
            _options = options.Value;
        }

        public async Task<(byte[] Content, string FileName)?> BuildAsync(string bookingType, int bookingId, string? requestHost = null)
        {
            var host = string.IsNullOrWhiteSpace(requestHost) ? _options.PublicBaseUrl : requestHost;

            if (string.Equals(bookingType, "Equipment", StringComparison.OrdinalIgnoreCase))
            {
                var b = await _equipmentBookings.Query()
                    .Include(x => x.Payment)
                    .Include(x => x.Farmer)
                    .Include(x => x.Equipment!).ThenInclude(e => e.Owner)
                    .FirstOrDefaultAsync(x => x.Id == bookingId);

                if (b?.Equipment is null || b.Payment is null) return null;

                var payment = b.Payment;
                var isPaidOrRefunded = payment.Status == PaymentStatus.Succeeded ||
                                       payment.Status == PaymentStatus.Refunded ||
                                       b.Status == BookingStatus.Completed ||
                                       b.Status == BookingStatus.Paid;

                if (!isPaidOrRefunded) return null;

                var code = $"KL-EQ-{b.RequestedOn.Year}-{b.Id:D3}";
                var verifyUrl = $"{host.TrimEnd('/')}/Verify/{code}";
                var qrB64 = _qrCode.GenerateBase64Png(verifyUrl, 6);
                var rawB64 = qrB64.Contains(',') ? qrB64.Split(',')[1] : qrB64;
                var qrBytes = Convert.FromBase64String(rawB64);

                var days = ListingFormat.InclusiveDays(b.StartDate, b.EndDate);
                var isRefunded = payment.Status == PaymentStatus.Refunded || (b.Status == BookingStatus.Cancelled && payment.RefundedOn != null);
                var issueDate = payment.PaidOn ?? payment.RefundedOn ?? b.UpdatedOn ?? b.RequestedOn;

                var model = new BookingReceiptModel(
                    ReceiptNumber: $"KL-RC-EQ-{b.Id:D6}",
                    BookingCode: code,
                    BookingType: "Equipment",
                    BookingId: b.Id,
                    IssueDate: issueDate,
                    IsRefunded: isRefunded,
                    RefundedOn: payment.RefundedOn,
                    FarmerName: b.Farmer?.FullName ?? "Registered Farmer",
                    FarmerLocation: b.Farmer?.Location,
                    FarmerPhone: b.Farmer?.PhoneNumber,
                    FarmerEmail: b.Farmer?.Email,
                    OwnerName: b.Equipment.Owner?.FullName ?? "Equipment Owner",
                    OwnerBusiness: b.Equipment.Owner?.BusinessOrFarmName,
                    OwnerLocation: b.Equipment.Owner?.Location,
                    OwnerPhone: b.Equipment.Owner?.PhoneNumber,
                    ListingName: b.Equipment.Name,
                    ListingCategory: b.Equipment.Category,
                    ListingLocation: b.Equipment.Location,
                    StartDate: b.StartDate,
                    EndDate: b.EndDate,
                    QuantityDisplay: b.Units > 1 ? $"{b.Units} units" : "1 unit",
                    RateDescription: b.PricingNote ?? ($"{ListingFormat.Taka(b.AgreedRate ?? b.Equipment.DailyRate)} / day" + (b.Units > 1 ? $" × {b.Units} units" : "")),
                    AgreedGross: b.AgreedGross ?? payment.Amount,
                    AmountPaid: payment.Amount,
                    PaymentMethod: payment.Method,
                    PaymentReference: payment.Reference,
                    GatewayReference: payment.GatewayReference,
                    PaidOn: payment.PaidOn ?? b.RequestedOn,
                    DiscountAmount: b.DiscountAmount,
                    PointsUsed: b.PointsUsed,
                    PromoCode: b.AppliedPromoCode,
                    Status: b.Status,
                    VerifyUrl: verifyUrl,
                    QrCodePngBytes: qrBytes
                );

                var document = new BookingReceiptDocument(model);
                return (document.GeneratePdf(), document.FileName);
            }

            if (string.Equals(bookingType, "Godown", StringComparison.OrdinalIgnoreCase))
            {
                var b = await _godownBookings.Query()
                    .Include(x => x.Payment)
                    .Include(x => x.Farmer)
                    .Include(x => x.Godown!).ThenInclude(g => g.Owner)
                    .FirstOrDefaultAsync(x => x.Id == bookingId);

                if (b?.Godown is null || b.Payment is null) return null;

                var payment = b.Payment;
                var isPaidOrRefunded = payment.Status == PaymentStatus.Succeeded ||
                                       payment.Status == PaymentStatus.Refunded ||
                                       b.Status == BookingStatus.Completed ||
                                       b.Status == BookingStatus.Paid;

                if (!isPaidOrRefunded) return null;

                var code = $"KL-GD-{b.RequestedOn.Year}-{b.Id:D3}";
                var verifyUrl = $"{host.TrimEnd('/')}/Verify/{code}";
                var qrB64 = _qrCode.GenerateBase64Png(verifyUrl, 6);
                var rawB64 = qrB64.Contains(',') ? qrB64.Split(',')[1] : qrB64;
                var qrBytes = Convert.FromBase64String(rawB64);

                var isRefunded = payment.Status == PaymentStatus.Refunded || (b.Status == BookingStatus.Cancelled && payment.RefundedOn != null);
                var issueDate = payment.PaidOn ?? payment.RefundedOn ?? b.UpdatedOn ?? b.RequestedOn;
                var months = ListingFormat.Months(b.StartDate, b.EndDate);

                var model = new BookingReceiptModel(
                    ReceiptNumber: $"KL-RC-GD-{b.Id:D6}",
                    BookingCode: code,
                    BookingType: "Godown",
                    BookingId: b.Id,
                    IssueDate: issueDate,
                    IsRefunded: isRefunded,
                    RefundedOn: payment.RefundedOn,
                    FarmerName: b.Farmer?.FullName ?? "Registered Farmer",
                    FarmerLocation: b.Farmer?.Location,
                    FarmerPhone: b.Farmer?.PhoneNumber,
                    FarmerEmail: b.Farmer?.Email,
                    OwnerName: b.Godown.Owner?.FullName ?? "Godown Owner",
                    OwnerBusiness: b.Godown.Owner?.BusinessOrFarmName,
                    OwnerLocation: b.Godown.Owner?.Location,
                    OwnerPhone: b.Godown.Owner?.PhoneNumber,
                    ListingName: b.Godown.Name,
                    ListingCategory: b.Godown.StorageType,
                    ListingLocation: b.Godown.Location,
                    StartDate: b.StartDate,
                    EndDate: b.EndDate,
                    QuantityDisplay: $"{b.StorageTons:N0} tons",
                    RateDescription: $"{ListingFormat.Taka(b.AgreedRate ?? b.Godown.PricePerTonPerMonth)} / t / mo",
                    AgreedGross: b.AgreedGross ?? payment.Amount,
                    AmountPaid: payment.Amount,
                    PaymentMethod: payment.Method,
                    PaymentReference: payment.Reference,
                    GatewayReference: payment.GatewayReference,
                    PaidOn: payment.PaidOn ?? b.RequestedOn,
                    DiscountAmount: b.DiscountAmount,
                    PointsUsed: b.PointsUsed,
                    PromoCode: b.AppliedPromoCode,
                    Status: b.Status,
                    VerifyUrl: verifyUrl,
                    QrCodePngBytes: qrBytes
                );

                var document = new BookingReceiptDocument(model);
                return (document.GeneratePdf(), document.FileName);
            }

            return null;
        }
    }
}
