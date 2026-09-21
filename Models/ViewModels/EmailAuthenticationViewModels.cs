using System.ComponentModel.DataAnnotations;

namespace KrishiLink.Models.ViewModels;

public class EmailAddressViewModel
{
    [Required, EmailAddress, StringLength(254)]
    [Display(Name = "Email address")]
    public string Email { get; set; } = "";
}

public class VerifyEmailViewModel : EmailAddressViewModel
{
    [Required, RegularExpression(@"^\d{6,10}$", ErrorMessage = "Enter the numeric code from your email.")]
    [Display(Name = "Email verification code")]
    public string Code { get; set; } = "";

    [Required, RegularExpression("^(signup|email_change)$")]
    public string Purpose { get; set; } = "signup";
}

public class ResetPasswordViewModel : EmailAddressViewModel
{
    [Required, RegularExpression(@"^\d{6,10}$", ErrorMessage = "Enter the numeric recovery code from your email.")]
    [Display(Name = "Recovery code")]
    public string Code { get; set; } = "";

    [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string NewPassword { get; set; } = "";

    [Required, Compare(nameof(NewPassword)), DataType(DataType.Password)]
    [Display(Name = "Confirm new password")]
    public string ConfirmPassword { get; set; } = "";
}

public class EmailLinkViewModel
{
    public bool IsRecovery { get; set; }
    public string LinkId { get; set; } = "";

    [StringLength(100, MinimumLength = 8), DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string? NewPassword { get; set; }

    [Compare(nameof(NewPassword)), DataType(DataType.Password)]
    [Display(Name = "Confirm new password")]
    public string? ConfirmPassword { get; set; }
}
