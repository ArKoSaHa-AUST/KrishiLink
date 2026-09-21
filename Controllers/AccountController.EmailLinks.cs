using System.Text.RegularExpressions;
using KrishiLink.BLL.Services;
using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace KrishiLink.Controllers;

public partial class AccountController
{
    private const string EmailLinkCookie = "__Secure-KrishiLink.EmailLink";

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ServiceFilter(typeof(SupabaseAuthRateLimitFilter))]
    public IActionResult AuthCallback(string? token_hash, string? type, [FromServices] SupabaseEmailLinkStore links)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        if (type is not ("signup" or "recovery" or "email_change") ||
            token_hash == null || !Regex.IsMatch(token_hash, @"\A[a-fA-F0-9]{32,128}\z"))
        {
            TempData["ErrorMessage"] = "This email link is invalid. Request a new verification or recovery email.";
            return RedirectToAction(nameof(Login));
        }

        links.Take(Request.Cookies[EmailLinkCookie]);
        var id = links.Add(token_hash, type);
        Response.Cookies.Append(EmailLinkCookie, id, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Path = "/",
            MaxAge = TimeSpan.FromMinutes(15)
        });
        // Do not exchange on GET: security scanners commonly follow email links before the recipient.
        return RedirectToAction(nameof(EmailLink));
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult EmailLink([FromServices] SupabaseEmailLinkStore links)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        var pending = links.Get(Request.Cookies[EmailLinkCookie]);
        if (pending == null) return ExpiredEmailLink();
        return View(new EmailLinkViewModel
        {
            IsRecovery = pending.Type == "recovery",
            LinkId = Request.Cookies[EmailLinkCookie]!
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ServiceFilter(typeof(SupabaseAuthRateLimitFilter))]
    public async Task<IActionResult> EmailLink(EmailLinkViewModel model, [FromServices] SupabaseEmailLinkStore links)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        if (model.LinkId != Request.Cookies[EmailLinkCookie]) return ExpiredEmailLink();
        var pending = links.Get(Request.Cookies[EmailLinkCookie]);
        if (pending == null) return ExpiredEmailLink();
        model.IsRecovery = pending.Type == "recovery";
        if (model.IsRecovery)
        {
            if (string.IsNullOrWhiteSpace(model.NewPassword))
                ModelState.AddModelError(nameof(model.NewPassword), "Enter a new password.");
            if (string.IsNullOrWhiteSpace(model.ConfirmPassword))
                ModelState.AddModelError(nameof(model.ConfirmPassword), "Confirm your new password.");
        }
        if (!ModelState.IsValid) return View(model);

        pending = links.Take(Request.Cookies[EmailLinkCookie]);
        Response.Cookies.Delete(EmailLinkCookie, new CookieOptions { Path = "/", Secure = true });
        if (pending == null) return ExpiredEmailLink();
        SupabaseAuthTokens? tokens = null;
        try
        {
            tokens = await _auth.VerifyHashAsync(pending.TokenHash, pending.Type);
            if (string.IsNullOrWhiteSpace(tokens.AccessToken))
            {
                if (pending.Type != "email_change")
                    throw new SupabaseAuthException("The email link could not be verified.");
                TempData["SuccessMessage"] = "Confirmation accepted. Open the confirmation email sent to your other address to finish the email change.";
                return RedirectToAction(nameof(Login));
            }
            var remote = await _auth.GetUserAsync(tokens.AccessToken);
            if (remote.EmailConfirmedAt == null || string.IsNullOrWhiteSpace(remote.Email))
                throw new SupabaseAuthException("The email address has not been verified.");
            var local = await _userManager.FindByIdAsync(remote.Id);
            if (pending.Type == "recovery")
            {
                if (local == null) throw new SupabaseAuthException("No application profile exists for this account. Contact support.");
                await _auth.ChangePasswordAsync(tokens.AccessToken, model.NewPassword!);
                await InvalidatePasswordSessionsAsync(local, tokens.AccessToken);
                TempData["SuccessMessage"] = "Password reset. Sign in with your new password.";
            }
            else
            {
                if (local != null)
                {
                    local.Email = remote.Email;
                    local.UserName = remote.Email;
                    local.EmailConfirmed = true;
                    var updated = await _userManager.UpdateAsync(local);
                    if (!updated.Succeeded)
                        throw new SupabaseAuthException("Email confirmed, but your profile needs administrator attention.");
                }
                TempData["SuccessMessage"] = "Email confirmed. Sign in with your verified email and password.";
            }
            return RedirectToAction(nameof(Login));
        }
        catch (SupabaseAuthException ex)
        {
            TempData["ErrorMessage"] = ex.Message + " Reopen the email link or request a new email if it has expired.";
            return RedirectToAction(nameof(Login));
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(tokens?.AccessToken)) await RevokeProofSessionAsync(tokens.AccessToken);
        }
    }

    private IActionResult ExpiredEmailLink()
    {
        TempData["ErrorMessage"] = "This email confirmation page expired. Reopen the email link or request a new email.";
        return RedirectToAction(nameof(Login));
    }
}
