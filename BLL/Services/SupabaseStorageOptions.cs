using System.Text.RegularExpressions;
using System.Text.Json;

namespace KrishiLink.BLL.Services
{
    public sealed class SupabaseStorageOptions
    {
        public const string SectionName = "Supabase";

        public string Url { get; set; } = string.Empty;
        public string SecretKey { get; set; } = string.Empty;
        public string PublicBucket { get; set; } = "listing-images";
        public string PrivateBucket { get; set; } = "verification-documents";

        public bool IsValid()
        {
            return Uri.TryCreate(Url, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
                && string.IsNullOrEmpty(uri.UserInfo)
                && string.IsNullOrEmpty(uri.Query)
                && string.IsNullOrEmpty(uri.Fragment)
                && uri.AbsolutePath == "/"
                && IsBackendSecret(SecretKey)
                && IsBucketName(PublicBucket)
                && IsBucketName(PrivateBucket)
                && !string.Equals(PublicBucket, PrivateBucket, StringComparison.Ordinal);
        }

        private static bool IsBucketName(string value) =>
            !string.IsNullOrEmpty(value) && Regex.IsMatch(value, @"\A[a-z0-9][a-z0-9-]{0,62}\z", RegexOptions.CultureInvariant);

        private static bool IsBackendSecret(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || key.Any(char.IsWhiteSpace)) return false;
            if (key.StartsWith("sb_secret_", StringComparison.Ordinal)) return key.Length > "sb_secret_".Length;
            var parts = key.Split('.');
            if (parts.Length != 3) return false;
            try
            {
                var payload = parts[1].Replace('-', '+').Replace('_', '/');
                payload = payload.PadRight((payload.Length + 3) / 4 * 4, '=');
                using var json = JsonDocument.Parse(Convert.FromBase64String(payload));
                return json.RootElement.ValueKind == JsonValueKind.Object
                    && json.RootElement.TryGetProperty("role", out var role)
                    && role.ValueKind == JsonValueKind.String
                    && role.GetString() == "service_role";
            }
            catch (FormatException)
            {
                return false;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }

    public static class SupabaseStorageRegistration
    {
        public static IServiceCollection AddSupabaseStorage(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddOptions<SupabaseStorageOptions>()
                .Bind(configuration.GetSection(SupabaseStorageOptions.SectionName))
                .Validate(options => options.IsValid(), "Supabase Storage requires a valid URL, backend secret, and distinct public/private bucket names.")
                .ValidateOnStart();
            services.AddHttpClient(SupabaseStorageClient.HttpClientName, client =>
            {
                client.Timeout = TimeSpan.FromSeconds(60);
            })
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
                .RedactLoggedHeaders(_ => true);
            services.AddScoped<SupabaseStorageClient>();
            services.AddScoped<IFileStorageService, FileStorageService>();
            return services;
        }
    }
}
