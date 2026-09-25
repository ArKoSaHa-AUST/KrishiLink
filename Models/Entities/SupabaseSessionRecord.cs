namespace KrishiLink.Models.Entities
{
    /// <summary>
    /// A signed-in browser session, stored so every app instance sees it and a restart signs nobody out.
    /// The cookie carries an opaque id; only its SHA-256 is stored here, and the Supabase tokens are encrypted
    /// with ASP.NET Data Protection, so a database reader can neither hijack nor read a session.
    /// </summary>
    public class SupabaseSessionRecord
    {
        /// <summary>Hex SHA-256 of the opaque session id held in the auth cookie.</summary>
        public string Id { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;
        public string SecurityStamp { get; set; } = string.Empty;

        /// <summary>Data Protection ciphertext of the Supabase access/refresh tokens.</summary>
        public string ProtectedTokens { get; set; } = string.Empty;

        public DateTime TokenExpiresAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
