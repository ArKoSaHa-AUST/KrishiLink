using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace KrishiLink.BLL.Services
{
    public sealed class SupabaseStorageException : Exception
    {
        public SupabaseStorageException(string operation, HttpStatusCode? statusCode = null)
            : base(statusCode.HasValue
                ? $"Supabase Storage {operation} failed (HTTP {(int)statusCode.Value})."
                : $"Supabase Storage {operation} failed.")
        {
        }
    }

    public sealed class SupabaseStorageClient
    {
        public const string HttpClientName = "SupabaseStorage";
        private readonly HttpClient _http;
        private readonly SupabaseStorageOptions _options;
        private readonly string _storageUrl;

        public SupabaseStorageClient(IHttpClientFactory clients, IOptions<SupabaseStorageOptions> options)
        {
            _options = options.Value;
            _http = clients.CreateClient(HttpClientName);
            _storageUrl = new Uri(_options.Url).AbsoluteUri.TrimEnd('/') + "/storage/v1/";
        }

        public string PublicObjectUrl(string key) => _storageUrl + "object/public/" + _options.PublicBucket + "/" + key;
        public string PublicObjectPrefix => _storageUrl + "object/public/" + _options.PublicBucket + "/";

        public async Task InitializeBucketsAsync(CancellationToken cancellationToken = default)
        {
            using var request = CreateRequest(HttpMethod.Get, "bucket");
            using var response = await SendAsync(request, "bucket lookup", cancellationToken);
            EnsureSuccess(response, "bucket lookup");
            using var buckets = await ReadJsonAsync(response, "bucket lookup", cancellationToken);
            if (buckets.RootElement.ValueKind != JsonValueKind.Array)
                throw new SupabaseStorageException("bucket lookup");

            var existing = buckets.RootElement.EnumerateArray().ToArray();
            // Reject an unsafe existing bucket before creating or uploading anything.
            foreach (var (name, isPublic) in new[] { (_options.PublicBucket, true), (_options.PrivateBucket, false) })
            {
                var bucket = existing.FirstOrDefault(value => value.TryGetProperty("id", out var id) && id.GetString() == name);
                if (bucket.ValueKind != JsonValueKind.Undefined)
                    ValidateBucket(bucket, name, isPublic);
            }
            await EnsureBucketAsync(_options.PublicBucket, true, existing, cancellationToken);
            await EnsureBucketAsync(_options.PrivateBucket, false, existing, cancellationToken);
        }

        private async Task EnsureBucketAsync(string name, bool isPublic, JsonElement[] existing, CancellationToken cancellationToken)
        {
            if (existing.Any(value => value.TryGetProperty("id", out var id) && id.GetString() == name)) return;
            using var create = CreateRequest(HttpMethod.Post, "bucket");
            create.Content = JsonContent.Create(new
            {
                id = name,
                name,
                @public = isPublic,
                file_size_limit = FileStorageService.MaxFileSizeBytes,
                allowed_mime_types = new[] { "image/jpeg", "image/png", "image/webp" }
            });
            using var created = await SendAsync(create, "bucket creation", cancellationToken);
            // Concurrent app instances can race to create the same bucket. Always verify its actual privacy.
            if (!created.IsSuccessStatusCode && created.StatusCode != HttpStatusCode.Conflict
                && !await HasErrorCodeAsync(created, new[] { "Duplicate", "409" }, cancellationToken))
                throw new SupabaseStorageException("bucket creation", created.StatusCode);

            using var lookup = CreateRequest(HttpMethod.Get, "bucket/" + name);
            using var found = await SendAsync(lookup, "bucket verification", cancellationToken);
            EnsureSuccess(found, "bucket verification");
            using var bucket = await ReadJsonAsync(found, "bucket verification", cancellationToken);
            ValidateBucket(bucket.RootElement, name, isPublic);
        }

        private static void ValidateBucket(JsonElement bucket, string name, bool isPublic)
        {
            if (!bucket.TryGetProperty("id", out var id) || id.GetString() != name
                || !bucket.TryGetProperty("public", out var visibility)
                || visibility.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
                || visibility.GetBoolean() != isPublic)
                throw new InvalidOperationException("Supabase Storage bucket visibility is unsafe. Listing images must be public and verification documents must be private.");
        }

        public async Task UploadAsync(string key, bool isPrivate, Stream stream, string contentType, CancellationToken cancellationToken)
        {
            using var request = CreateRequest(HttpMethod.Post, "object/" + Bucket(isPrivate) + "/" + key);
            request.Headers.Add("x-upsert", "false");
            request.Content = new StreamContent(stream);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            request.Headers.CacheControl = new CacheControlHeaderValue
            {
                MaxAge = isPrivate ? TimeSpan.Zero : TimeSpan.FromDays(365),
                NoStore = isPrivate
            };
            using var response = await SendAsync(request, "upload", cancellationToken);
            EnsureSuccess(response, "upload");
        }

        public async Task DeleteAsync(IEnumerable<string> keys, bool isPrivate, CancellationToken cancellationToken)
        {
            foreach (var batch in keys.Distinct(StringComparer.Ordinal).Chunk(100))
            {
                using var request = CreateRequest(HttpMethod.Delete, "object/" + Bucket(isPrivate));
                request.Content = JsonContent.Create(new { prefixes = batch });
                using var response = await SendAsync(request, "deletion", cancellationToken);
                EnsureSuccess(response, "deletion");
            }
        }

        public async Task<byte[]?> DownloadPrivateAsync(string key, CancellationToken cancellationToken)
        {
            using var request = CreateRequest(HttpMethod.Get, "object/authenticated/" + _options.PrivateBucket + "/" + key);
            using var response = await SendAsync(request, "private download", cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound
                || (!response.IsSuccessStatusCode && await HasErrorCodeAsync(response, new[] { "NoSuchKey", "not_found", "404" }, cancellationToken)))
                return null;
            EnsureSuccess(response, "private download");
            if (response.Content.Headers.ContentLength > FileStorageService.MaxFileSizeBytes)
                throw new SupabaseStorageException("private download size validation");
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var data = new MemoryStream();
            var buffer = new byte[81920];
            int count;
            while ((count = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                if (data.Length + count > FileStorageService.MaxFileSizeBytes)
                    throw new SupabaseStorageException("private download size validation");
                await data.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
            }
            return data.ToArray();
        }

        private string Bucket(bool isPrivate) => isPrivate ? _options.PrivateBucket : _options.PublicBucket;

        private HttpRequestMessage CreateRequest(HttpMethod method, string path)
        {
            var request = new HttpRequestMessage(method, _storageUrl + path);
            request.Headers.Add("apikey", _options.SecretKey);
            // Opaque sb_secret keys are API keys, not JWTs. Legacy service_role JWTs also use Bearer.
            if (!_options.SecretKey.StartsWith("sb_secret_", StringComparison.Ordinal))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.SecretKey);
            return request;
        }

        private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, string operation, CancellationToken cancellationToken)
        {
            try
            {
                return await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            }
            catch (HttpRequestException)
            {
                throw new SupabaseStorageException(operation);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new SupabaseStorageException(operation + " timeout");
            }
        }

        private static void EnsureSuccess(HttpResponseMessage response, string operation)
        {
            if (!response.IsSuccessStatusCode)
                throw new SupabaseStorageException(operation, response.StatusCode);
        }

        private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
        {
            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            }
            catch (JsonException)
            {
                throw new SupabaseStorageException(operation);
            }
        }

        private static async Task<bool> HasErrorCodeAsync(HttpResponseMessage response, string[] codes, CancellationToken cancellationToken)
        {
            try
            {
                using var json = await ReadJsonAsync(response, "error decoding", cancellationToken);
                foreach (var field in new[] { "code", "error", "statusCode" })
                {
                    if (json.RootElement.ValueKind == JsonValueKind.Object
                        && json.RootElement.TryGetProperty(field, out var value)
                        && codes.Contains(value.ToString(), StringComparer.Ordinal))
                        return true;
                }
                return false;
            }
            catch (SupabaseStorageException)
            {
                return false;
            }
        }
    }
}
