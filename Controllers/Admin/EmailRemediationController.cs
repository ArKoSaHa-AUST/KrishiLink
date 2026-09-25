using System.Data;
using KrishiLink.BLL.Services;
using KrishiLink.DAL;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Npgsql;

namespace KrishiLink.Controllers.Admin
{
    /// <summary>
    /// One-off, Development-only clean-up for accounts registered while sign-up created Supabase users as already
    /// confirmed. It can only ever <em>remove</em> a confirmation (and re-send the code); there is deliberately no
    /// action that confirms an address, so it cannot become an admin wrapper around the original hole.
    /// </summary>
    [Authorize(Roles = AppRoles.Admin)]
    [Route("Admin/[controller]")]
    public class EmailRemediationController : Controller
    {
        private const string ViewPath = "~/Views/Admin/EmailRemediation/Index.cshtml";

        // Confirmed without a confirmation e-mail ever being sent = created with email_confirm: true, never proven.
        private const string FindUnprovenSql = $"""
            SELECT u."Id", u."Email", u."FullName", u."UserRole", u."EmailConfirmed", au.email_confirmed_at, au.confirmation_sent_at
            FROM {DatabaseConfiguration.Schema}."AspNetUsers" AS u
            JOIN auth.users AS au ON au.id::text = u."Id"
            WHERE u."UserRole" <> 'Admin'
              AND (au.email_confirmed_at IS NULL OR au.confirmation_sent_at IS NULL)
              AND (u."EmailConfirmed" OR au.email_confirmed_at IS NOT NULL)
            ORDER BY u."CreatedAt"
            """;

        /// <summary>Shown on the page so it can be run in the Supabase SQL editor if this role may not write auth.users.</summary>
        public const string ResetRemoteSql = $"""
            UPDATE auth.users AS au SET email_confirmed_at = NULL
            FROM {DatabaseConfiguration.Schema}."AspNetUsers" AS u
            WHERE au.id::text = u."Id" AND au.confirmation_sent_at IS NULL AND au.email_confirmed_at IS NOT NULL
              AND u."UserRole" <> 'Admin';
            """;

        private const string ResetLocalSql = $"""
            UPDATE {DatabaseConfiguration.Schema}."AspNetUsers" AS u SET "EmailConfirmed" = FALSE
            FROM auth.users AS au
            WHERE au.id::text = u."Id" AND au.email_confirmed_at IS NULL AND u."EmailConfirmed";
            """;

        private readonly ApplicationDbContext _db;
        private readonly SupabaseAuthClient _auth;
        private readonly IWebHostEnvironment _env;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly ILogger<EmailRemediationController> _logger;

        public EmailRemediationController(ApplicationDbContext db, SupabaseAuthClient auth, IWebHostEnvironment env,
            IStringLocalizer<SharedResource> localizer, ILogger<EmailRemediationController> logger)
        {
            _db = db;
            _auth = auth;
            _env = env;
            _localizer = localizer;
            _logger = logger;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            if (!_env.IsDevelopment()) return NotFound();
            return View(ViewPath, await FindUnprovenAsync());
        }

        /// <summary>Removes the unproven confirmation in Supabase and locally, then re-sends the sign-up code.</summary>
        [HttpPost("Remediate")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remediate()
        {
            if (!_env.IsDevelopment()) return NotFound();

            var accounts = await FindUnprovenAsync();
            int reset;
            try
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
                reset = await _db.Database.ExecuteSqlRawAsync(ResetRemoteSql);
                await _db.Database.ExecuteSqlRawAsync(ResetLocalSql);
                await transaction.CommitAsync();
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InsufficientPrivilege)
            {
                TempData["ErrorMessage"] = _localizer["This database role cannot modify auth.users. Run the SQL shown on this page in the Supabase SQL editor, then use this page again to re-send the codes."].Value;
                return RedirectToAction(nameof(Index));
            }

            var sent = 0;
            foreach (var account in accounts)
            {
                try
                {
                    await _auth.SendConfirmationAsync(account.Email);
                    sent++;
                }
                catch (SupabaseAuthException ex)
                {
                    _logger.LogWarning("Confirmation code could not be re-sent to user {UserId} ({Status}).", account.UserId, ex.Status);
                }
            }

            _logger.LogWarning("E-mail remediation reset {Reset} unproven confirmation(s) and re-sent {Sent} code(s).", reset, sent);
            TempData["SuccessMessage"] = _localizer["Removed {0} unproven confirmation(s) and re-sent {1} confirmation code(s).", reset, sent].Value;
            return RedirectToAction(nameof(Index));
        }

        private async Task<List<UnprovenEmailAccount>> FindUnprovenAsync()
        {
            var accounts = new List<UnprovenEmailAccount>();
            var connection = _db.Database.GetDbConnection();
            var opened = connection.State != ConnectionState.Open;
            if (opened) await connection.OpenAsync();
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = FindUnprovenSql;
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    accounts.Add(new UnprovenEmailAccount(
                        reader.GetString(0),
                        reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        reader.GetString(2),
                        reader.GetString(3),
                        reader.GetBoolean(4),
                        reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                        reader.IsDBNull(6) ? null : reader.GetDateTime(6)));
                }
            }
            finally
            {
                if (opened) await connection.CloseAsync();
            }
            return accounts;
        }
    }
}
