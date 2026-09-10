using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KrishiLink.BLL.Services
{
    /// <summary>
    /// Emails every owner their previous month's PDF statement once the month has rolled over.
    /// Idempotent: <see cref="ApplicationUser.LastStatementSentMonth"/> records what has already gone out,
    /// so restarts never resend and a month missed while the app was down is sent on the next run.
    /// </summary>
    public class MonthlyStatementScheduler : BackgroundService
    {
        private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);
        private readonly IServiceScopeFactory _scopes;
        private readonly IOptions<EmailOptions> _email;
        private readonly ILogger<MonthlyStatementScheduler> _logger;

        public MonthlyStatementScheduler(IServiceScopeFactory scopes, IOptions<EmailOptions> email, ILogger<MonthlyStatementScheduler> logger)
        {
            _scopes = scopes;
            _email = email;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_email.Value.IsConfigured)
            {
                _logger.LogInformation("Monthly statement emails are disabled (Email:Enabled is false or no host configured).");
                return;
            }

            // Small delay so migrations/seeding in Program.cs finish first
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await SendDueStatementsAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Monthly statement run failed; will retry next cycle.");
                }
                await Task.Delay(CheckInterval, stoppingToken);
            }
        }

        private async Task SendDueStatementsAsync(CancellationToken ct)
        {
            var lastMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1);

            using var scope = _scopes.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

            var dueOwners = await userManager.Users
                .Where(u => (u.UserRole == AppRoles.EquipmentOwner || u.UserRole == AppRoles.GodownOwner)
                    && u.Email != null && !u.Email.EndsWith("@krishilink.local")
                    && (u.LastStatementSentMonth == null || u.LastStatementSentMonth < lastMonth))
                .ToListAsync(ct);

            foreach (var owner in dueOwners)
            {
                IOwnerRevenueService revenue = owner.UserRole == AppRoles.GodownOwner
                    ? scope.ServiceProvider.GetRequiredService<IGodownRevenueService>()
                    : scope.ServiceProvider.GetRequiredService<IEquipmentRevenueService>();

                // Nothing happened last month → nothing to report, but record the month so we don't re-check forever
                var hadActivity = revenue.GetReport(owner.Id, new RevenueFilter { From = lastMonth, To = lastMonth.AddMonths(1).AddDays(-1) }).Funnel.Requested > 0;
                if (hadActivity)
                {
                    var (content, fileName) = revenue.GenerateMonthlyStatement(owner.Id, lastMonth,
                        new StatementOwner(owner.FullName, owner.BusinessOrFarmName, owner.Location));
                    await sender.SendAsync(owner.Email!,
                        $"Your KrishiLink statement for {lastMonth:MMMM yyyy}",
                        $"<p>Dear {owner.FullName},</p><p>Your revenue statement for <strong>{lastMonth:MMMM yyyy}</strong> is attached. " +
                        "You can also download it any time from the Revenue page.</p><p>— KrishiLink</p>",
                        new EmailAttachment(fileName, content, "application/pdf"), ct);
                    _logger.LogInformation("Sent {Month:yyyy-MM} statement to {Email}.", lastMonth, owner.Email);
                }

                owner.LastStatementSentMonth = lastMonth;
                await userManager.UpdateAsync(owner);
            }
        }
    }
}
