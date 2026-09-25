namespace KrishiLink.Models.Entities
{
    public enum EmailDeliveryStatus
    {
        /// <summary>Handed to the SMTP server without error.</summary>
        Sent,

        /// <summary>The SMTP server or the network refused it; <see cref="EmailDeliveryLog.Error"/> says why.</summary>
        Failed,

        /// <summary>SMTP is not configured on this server, so nothing left it (development and demo setups).</summary>
        NotConfigured
    }

    /// <summary>
    /// One outgoing e-mail and what became of it (QLT-03), so "I never got my receipt" has an answer. The address itself is
    /// never stored — only the same fingerprint the logs use (<c>PersonalDataLog.Email</c>) — so support looks a user up by
    /// fingerprinting the address on their profile. Rows older than the retention period are swept by the reminder scheduler.
    /// </summary>
    public class EmailDeliveryLog
    {
        public long Id { get; set; }

        /// <summary>"email#…" fingerprint of the recipient address.</summary>
        public string RecipientHash { get; set; } = string.Empty;

        /// <summary>The account the message was about, when the sender knew it.</summary>
        public string? UserId { get; set; }

        public string Subject { get; set; } = string.Empty;
        public EmailDeliveryStatus Status { get; set; }

        /// <summary>A short reason for a failure, with any e-mail address in it replaced by its fingerprint.</summary>
        public string? Error { get; set; }

        public bool HadAttachment { get; set; }
        public DateTime QueuedAt { get; set; }
        public DateTime AttemptedAt { get; set; }
    }
}
