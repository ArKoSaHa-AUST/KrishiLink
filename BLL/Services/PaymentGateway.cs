using KrishiLink.Models.Entities;
using Microsoft.Extensions.Options;

namespace KrishiLink.BLL.Services
{
    /// <summary>Bound from the "Payments" section of appsettings.json.</summary>
    public class PaymentsOptions
    {
        public const string SectionName = "Payments";

        /// <summary>Gateway implementation; "Simulated" is the only provider shipped.</summary>
        public string Provider { get; set; } = "Simulated";

        /// <summary>How long a requested payout stays "Processing" before it settles. Null = 2 min in Development, 30 min otherwise.</summary>
        public TimeSpan? SettlementDelay { get; set; }

        public int SettlementPollSeconds { get; set; } = 15;

        /// <summary>Wallet/account numbers ending with this suffix are rejected — deterministic failure hook for demos and tests.</summary>
        public string FailAccountSuffix { get; set; } = "0000";
    }

    public record GatewayInitiation(string GatewayReference, string RedirectUrl);

    public record GatewayResult(bool Succeeded, string? FailureReason = null);

    /// <summary>
    /// Provider boundary for collecting a farmer's payment. A real provider (SSLCommerz, bKash PGW) is added by
    /// implementing this interface and registering it for its <see cref="PaymentsOptions.Provider"/> name.
    /// </summary>
    public interface IPaymentGateway
    {
        /// <summary>Starts a checkout for <paramref name="payment"/>; returns the provider's transaction id and where to send the farmer.</summary>
        Task<GatewayInitiation> InitiateAsync(Payment payment, string returnUrl);

        /// <summary>Confirms the outcome the provider reports for a checkout; the caller decides what to persist.</summary>
        Task<GatewayResult> VerifyAsync(Payment payment, string outcomeToken);

        Task RefundAsync(Payment payment);
    }

    /// <summary>
    /// In-app sandbox: the "provider page" is /Bookings/Gateway, which posts back a chosen outcome.
    /// Wallets ending in <see cref="PaymentsOptions.FailAccountSuffix"/> always fail so failure paths can be demonstrated.
    /// </summary>
    public class SimulatedPaymentGateway : IPaymentGateway
    {
        public const string ProviderName = "Simulated";
        public const string OutcomeSuccess = "success";
        public const string OutcomeFail = "fail";

        private readonly PaymentsOptions _options;

        public SimulatedPaymentGateway(IOptions<PaymentsOptions> options)
        {
            _options = options.Value;
        }

        public Task<GatewayInitiation> InitiateAsync(Payment payment, string returnUrl)
        {
            var reference = "SIM-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
            return Task.FromResult(new GatewayInitiation(reference, $"/Bookings/Gateway?ref={Uri.EscapeDataString(reference)}"));
        }

        public Task<GatewayResult> VerifyAsync(Payment payment, string outcomeToken)
        {
            if (!string.Equals(outcomeToken, OutcomeSuccess, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(new GatewayResult(false, "Payment cancelled at the gateway (simulated)"));

            var rejects = !string.IsNullOrEmpty(_options.FailAccountSuffix)
                && (payment.PayerAccount ?? string.Empty).Trim().EndsWith(_options.FailAccountSuffix, StringComparison.Ordinal);
            return Task.FromResult(rejects
                ? new GatewayResult(false, "Insufficient balance (simulated)")
                : new GatewayResult(true));
        }

        public Task RefundAsync(Payment payment) => Task.CompletedTask;
    }
}
