using System.Security.Cryptography;
using System.Text;

namespace KrishiLink.BLL.Services;

/// <summary>
/// Log-safe stand-ins for personal data. Logs leave the database's access controls (shipping, retention), so they must
/// never carry NID numbers, tokens, passwords, full e-mail addresses or phone numbers — log a user id or this instead.
/// </summary>
public static class PersonalDataLog
{
    /// <summary>A stable, non-reversible fingerprint that lets the same address be correlated across log lines.</summary>
    public static string Email(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "(none)";
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant()));
        return "email#" + Convert.ToHexString(digest, 0, 6).ToLowerInvariant();
    }
}
