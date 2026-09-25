namespace KrishiLink.Models;

public class ErrorViewModel
{
    public string? RequestId { get; set; }

    /// <summary>The HTTP status being explained (404, 403, 429…), or null for an unhandled exception.</summary>
    public int? StatusCode { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
