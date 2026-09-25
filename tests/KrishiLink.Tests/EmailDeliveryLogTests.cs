using KrishiLink.BLL.Services;
using KrishiLink.DAL;
using KrishiLink.Models.Entities;
using KrishiLink.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace KrishiLink.Tests;

/// <summary>QLT-03: every e-mail leaves a record support can find — without the address itself.</summary>
public class EmailDeliveryDescribeTests
{
    [Fact]
    public void Failure_reasons_never_keep_an_email_address()
    {
        var reason = EmailDeliveryRecorder.Describe(new System.Net.Mail.SmtpException("Mailbox unavailable. The server response was: 5.1.1 <karim.farmer@example.com> unknown"));
        Assert.DoesNotContain("karim.farmer@example.com", reason);
        Assert.Contains(PersonalDataLog.Email("karim.farmer@example.com"), reason);
        Assert.StartsWith("SmtpException:", reason);
    }

    [Fact]
    public void Long_reasons_are_capped() =>
        Assert.Equal(300, EmailDeliveryRecorder.Describe(new InvalidOperationException(new string('x', 1000)))!.Length);
}

[Collection(PostgresCollection.Name)]
public class EmailDeliveryLogTests
{
    private readonly PostgresDatabase _database;

    public EmailDeliveryLogTests(PostgresDatabase database) => _database = database;

    private sealed class FailingSender : IEmailSender
    {
        public Task SendAsync(string to, string subject, string htmlBody, EmailAttachment? attachment = null, CancellationToken ct = default) =>
            throw new System.Net.Mail.SmtpException($"Mailbox unavailable for {to}");
    }

    private sealed class WorkingSender : IEmailSender
    {
        public Task SendAsync(string to, string subject, string htmlBody, EmailAttachment? attachment = null, CancellationToken ct = default) => Task.CompletedTask;
    }

    private async Task<List<EmailDeliveryLog>> DispatchAsync(Action<IServiceCollection> sender, EmailJob job)
    {
        await using var market = new Marketplace(_database, services =>
        {
            sender(services);
            services.AddScoped<IEmailDeliveryRecorder, EmailDeliveryRecorder>();
        });
        var hash = PersonalDataLog.Email(job.To);
        await market.InScopeAsync(async sp =>
        {
            var dispatcher = new EmailDispatchService(sp, NullLogger<EmailDispatchService>.Instance);
            using var stop = new CancellationTokenSource();
            await dispatcher.StartAsync(stop.Token);
            await dispatcher.EnqueueAsync(job);
            var db = sp.GetRequiredService<ApplicationDbContext>();
            for (var i = 0; i < 100 && !await db.EmailDeliveryLogs.AnyAsync(l => l.RecipientHash == hash); i++) await Task.Delay(50);
            stop.Cancel();
            await dispatcher.StopAsync(CancellationToken.None);
        });
        return await market.InScopeAsync(sp => sp.GetRequiredService<ApplicationDbContext>().EmailDeliveryLogs.AsNoTracking().Where(l => l.RecipientHash == hash).ToListAsync());
    }

    [PostgresFact]
    public async Task A_sent_receipt_is_recorded_with_its_account_and_attachment()
    {
        var to = $"sent-{Guid.NewGuid():N}@example.test";
        var logs = await DispatchAsync(s => s.AddScoped<IEmailSender, WorkingSender>(),
            new EmailJob(to, "[KrishiLink] Payment receipt", "<p>hi</p>", new EmailAttachment("r.pdf", new byte[] { 1 }, "application/pdf"), "farmer-42"));
        var log = Assert.Single(logs);
        Assert.Equal((EmailDeliveryStatus.Sent, "farmer-42", true), (log.Status, log.UserId, log.HadAttachment));
        Assert.Null(log.Error);
    }

    [PostgresFact]
    public async Task A_refused_email_is_recorded_as_failed_without_the_address()
    {
        var to = $"failed-{Guid.NewGuid():N}@example.test";
        var log = Assert.Single(await DispatchAsync(s => s.AddScoped<IEmailSender, FailingSender>(), new EmailJob(to, "Subject", "<p>x</p>")));
        Assert.Equal(EmailDeliveryStatus.Failed, log.Status);
        Assert.DoesNotContain(to, log.Error);
        Assert.Contains(PersonalDataLog.Email(to), log.Error);
    }

    [PostgresFact]
    public async Task Without_smtp_the_record_says_it_was_never_sent()
    {
        var to = $"local-{Guid.NewGuid():N}@example.test";
        var log = Assert.Single(await DispatchAsync(
            s => s.AddScoped<IEmailSender>(_ => new LoggingEmailSender(NullLogger<LoggingEmailSender>.Instance)), new EmailJob(to, "Subject", "<p>x</p>")));
        Assert.Equal(EmailDeliveryStatus.NotConfigured, log.Status);
    }

    [PostgresFact]
    public async Task Records_past_retention_are_swept()
    {
        await using var market = new Marketplace(_database, s => s.AddScoped<IEmailDeliveryRecorder, EmailDeliveryRecorder>());
        var hash = PersonalDataLog.Email($"old-{Guid.NewGuid():N}@example.test");
        await market.InScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            db.EmailDeliveryLogs.AddRange(
                new EmailDeliveryLog { RecipientHash = hash, Subject = "old", AttemptedAt = DateTime.UtcNow.AddDays(-(EmailDeliveryRecorder.RetentionDays + 1)), QueuedAt = DateTime.UtcNow },
                new EmailDeliveryLog { RecipientHash = hash, Subject = "new", AttemptedAt = DateTime.UtcNow, QueuedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            Assert.True(await sp.GetRequiredService<IEmailDeliveryRecorder>().SweepAsync() >= 1);
            Assert.Equal(new[] { "new" }, await db.EmailDeliveryLogs.Where(l => l.RecipientHash == hash).Select(l => l.Subject).ToListAsync());
        });
    }
}

/// <summary>E-mail bodies carry text other users typed (listing names, reasons); it must arrive as text, never as markup.</summary>
public class EmailHtmlEncodingTests
{
    [Fact]
    public void A_listing_name_cannot_become_a_link_in_a_notification_email()
    {
        var html = NotificationService.BuildEmailHtml(
            "Booking accepted: <a href=\"https://evil.example/pay\">verify payment</a>",
            "Your request for <img src=x onerror=alert(1)> was accepted.",
            "https://krishilink.test/Bookings?type=Equipment&id=5");

        Assert.DoesNotContain("<a href=\"https://evil.example", html);
        Assert.DoesNotContain("<img", html);
        Assert.Contains("&lt;a href=&quot;https://evil.example/pay&quot;&gt;", html);
        Assert.Contains("href='https://krishilink.test/Bookings?type=Equipment&amp;id=5'", html);
    }
}
