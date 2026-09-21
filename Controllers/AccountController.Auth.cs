using KrishiLink.BLL.Services;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace KrishiLink.Controllers;

public partial class AccountController
{
    [HttpGet]
    public IActionResult VerifyEmail(string purpose = "signup")
    {
        if (purpose != "signup" && purpose != "email_change") return BadRequest();
        if (purpose == "email_change" && User.Identity?.IsAuthenticated != true) return Challenge();
        return View(new VerifyEmailViewModel { Purpose = purpose });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ServiceFilter(typeof(SupabaseAuthRateLimitFilter))]
    public async Task<IActionResult> VerifyEmail(VerifyEmailViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        if (model.Purpose != "signup" && model.Purpose != "email_change") return BadRequest();
        var current = model.Purpose == "email_change" ? await _userManager.GetUserAsync(User) : null;
        if (model.Purpose == "email_change" && current == null) return Challenge();
        SupabaseAuthTokens? tokens = null;
        try
        {
            tokens = await _auth.VerifyAsync(model.Email.Trim(), model.Code.Trim(), model.Purpose);
            if (string.IsNullOrWhiteSpace(tokens.AccessToken))
            {
                TempData["SuccessMessage"] = "Code accepted. If another confirmation email was sent, enter that code too. Otherwise, log in.";
                return RedirectToAction(nameof(VerifyEmail), new { purpose = model.Purpose });
            }
            var remote = await _auth.GetUserAsync(tokens.AccessToken);
            var local = await _userManager.FindByIdAsync(remote.Id);
            if (local == null || remote.EmailConfirmedAt == null ||
                (current != null && current.Id != remote.Id))
                throw new SupabaseAuthException("The email could not be verified for this account.");
            local.Email = remote.Email;
            local.UserName = remote.Email;
            local.EmailConfirmed = true;
            var result = await _userManager.UpdateAsync(local);
            if (!result.Succeeded)
                throw new SupabaseAuthException("Email confirmed, but the profile could not be updated. Please contact support.");
            TempData["SuccessMessage"] = "Email verified. You can now sign in using your verified email or registered phone number.";
            return RedirectToAction(nameof(Login));
        }
        catch (SupabaseAuthException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(tokens?.AccessToken)) await RevokeProofSessionAsync(tokens.AccessToken);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ServiceFilter(typeof(SupabaseAuthRateLimitFilter))]
    public async Task<IActionResult> ResendConfirmation(EmailAddressViewModel model)
    {
        if (ModelState.IsValid)
        {
            try { await _auth.SendConfirmationAsync(model.Email.Trim()); }
            catch (SupabaseAuthException) { }
        }
        TempData["SuccessMessage"] = "If an unverified account exists, a new code will be emailed. Please wait before requesting another.";
        return RedirectToAction(nameof(VerifyEmail));
    }

    [HttpGet]
    public IActionResult ForgotPassword() => View(new EmailAddressViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ServiceFilter(typeof(SupabaseAuthRateLimitFilter))]
    public async Task<IActionResult> ForgotPassword(EmailAddressViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        try { await _auth.SendRecoveryAsync(model.Email.Trim()); }
        catch (SupabaseAuthException) { }
        TempData["SuccessMessage"] = "If an account exists, a password recovery code will be emailed. Check your inbox and spam folder.";
        return RedirectToAction(nameof(ResetPassword));
    }

    [HttpGet]
    public IActionResult ResetPassword() => View(new ResetPasswordViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ServiceFilter(typeof(SupabaseAuthRateLimitFilter))]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        SupabaseAuthTokens? tokens = null;
        try
        {
            tokens = await _auth.VerifyAsync(model.Email.Trim(), model.Code.Trim(), "recovery");
            var remote = await _auth.GetUserAsync(tokens.AccessToken);
            var user = await _userManager.FindByIdAsync(remote.Id);
            if (user == null || remote.EmailConfirmedAt == null)
                throw new SupabaseAuthException("This recovery request cannot be completed.");
            await _auth.ChangePasswordAsync(tokens.AccessToken, model.NewPassword);
            await InvalidatePasswordSessionsAsync(user, tokens.AccessToken);
            TempData["SuccessMessage"] = "Password reset. Sign in with your new password.";
            return RedirectToAction(nameof(Login));
        }
        catch (SupabaseAuthException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(tokens?.AccessToken)) await RevokeProofSessionAsync(tokens.AccessToken);
        }
    }

    private async Task InvalidatePasswordSessionsAsync(ApplicationUser user, string accessToken)
    {
        _sessions.RevokeLocalSessions(user.Id);
        var result = await _userManager.UpdateSecurityStampAsync(user);
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        try { await _auth.LogoutAsync(accessToken, allSessions: true); }
        catch (SupabaseAuthException)
        {
            _logger.LogWarning("Password changed but Supabase global logout failed for {UserId}.", user.Id);
            throw new SupabaseAuthException("Password changed and KrishiLink sessions signed out, but provider logout failed. Sign in with your new password and contact support.");
        }
        if (!result.Succeeded)
            throw new SupabaseAuthException("Password changed, but account session invalidation needs administrator attention.");
    }

    private async Task RevokeProofSessionAsync(string accessToken)
    {
        try { await _auth.LogoutAsync(accessToken); }
        catch (SupabaseAuthException)
        {
            _logger.LogWarning("An authentication proof session could not be revoked.");
        }
    }
}
