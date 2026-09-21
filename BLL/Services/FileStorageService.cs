using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace KrishiLink.BLL.Services
{
    public interface IFileStorageService
    {
        /// <summary>Validates uploaded files against allowed image extensions, MIME types, magic byte signatures, and size limits (5 MB). Returns a list of human-readable error messages, or an empty list if all are valid.</summary>
        List<string> ValidateFiles(IEnumerable<IFormFile>? files);

        /// <summary>Stores validated images in Supabase's public bucket and returns public URLs.</summary>
        Task<List<string>> SaveImagesAsync(IEnumerable<IFormFile>? files, string folder);

        /// <summary>Stores validated identity images in the private bucket and returns object keys, never public URLs.</summary>
        Task<List<string>> SavePrivateFilesAsync(IEnumerable<IFormFile>? files, string folder);

        Task DeleteFilesAsync(IEnumerable<string>? urls, string folder);
        Task DeletePrivateFilesAsync(IEnumerable<string>? paths, string ownerId);

        /// <summary>Call only after owner/admin authorization; the key must also belong to the target owner.</summary>
        Task<StoredPrivateFile?> ReadPrivateFileAsync(string? path, string ownerId, CancellationToken cancellationToken = default);
        Task InitializeBucketsAsync(CancellationToken cancellationToken = default);
    }

    public sealed record StoredPrivateFile(byte[] Content, string ContentType);

    public class FileStorageService : IFileStorageService
    {
        public const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
        private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/pjpeg", "image/png", "image/webp" };

        private readonly SupabaseStorageClient _storage;

        public FileStorageService(SupabaseStorageClient storage)
        {
            _storage = storage;
        }

        public List<string> ValidateFiles(IEnumerable<IFormFile>? files)
        {
            var errors = new List<string>();
            if (files is null) return errors;

            foreach (var file in files)
            {
                if (file is null) continue;

                var fileName = Path.GetFileName(file.FileName);
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    errors.Add("One of the uploaded files has an invalid name.");
                    continue;
                }

                if (file.Length == 0)
                {
                    errors.Add($"File '{fileName}' is empty.");
                    continue;
                }

                if (file.Length > MaxFileSizeBytes)
                {
                    var sizeInMb = file.Length / (1024.0 * 1024.0);
                    errors.Add($"File '{fileName}' exceeds the 5 MB limit ({sizeInMb:F1} MB). Please compress or choose a smaller image.");
                    continue;
                }

                var extension = Path.GetExtension(fileName);
                if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
                {
                    errors.Add($"File '{fileName}' has an unsupported file format ('{extension}'). Only JPG, PNG, and WebP images are allowed.");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(file.ContentType) && !AllowedMimeTypes.Contains(file.ContentType))
                {
                    errors.Add($"File '{fileName}' has an invalid image MIME type ('{file.ContentType}'). Only valid JPG, PNG, and WebP images are allowed.");
                    continue;
                }

                // Verify file signature / magic bytes
                if (!IsValidImageHeader(file, extension))
                {
                    errors.Add($"File '{fileName}' does not appear to be a valid image file. Upload actual JPG, PNG, or WebP photos.");
                }
            }

            return errors;
        }

        public Task InitializeBucketsAsync(CancellationToken cancellationToken = default) =>
            _storage.InitializeBucketsAsync(cancellationToken);

        public Task<List<string>> SaveImagesAsync(IEnumerable<IFormFile>? files, string folder) =>
            SaveAsync(files, folder, false);

        public Task<List<string>> SavePrivateFilesAsync(IEnumerable<IFormFile>? files, string folder) =>
            SaveAsync(files, folder, true);

        private async Task<List<string>> SaveAsync(IEnumerable<IFormFile>? files, string folder, bool isPrivate)
        {
            ValidateFolder(folder, isPrivate);
            var uploads = files?.Where(file => file is not null).ToList() ?? new List<IFormFile>();
            var errors = ValidateFiles(uploads);
            if (errors.Count > 0)
                throw new ArgumentException(string.Join(" ", errors), nameof(files));

            var attemptedKeys = new List<string>();
            try
            {
                foreach (var file in uploads)
                {
                    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                    var key = $"{folder}/{Guid.NewGuid():N}{extension}";
                    // Include the in-flight object: a transport failure may occur after Storage persisted it.
                    attemptedKeys.Add(key);
                    await using var stream = file.OpenReadStream();
                    await _storage.UploadAsync(key, isPrivate, stream, ContentType(extension), CancellationToken.None);
                }
            }
            catch (Exception uploadError)
            {
                try
                {
                    await _storage.DeleteAsync(attemptedKeys, isPrivate, CancellationToken.None);
                }
                catch (Exception cleanupError)
                {
                    throw new AggregateException("Storage upload and cleanup failed.", uploadError, cleanupError);
                }
                throw;
            }
            return isPrivate ? attemptedKeys : attemptedKeys.Select(_storage.PublicObjectUrl).ToList();
        }

        public Task DeleteFilesAsync(IEnumerable<string>? urls, string folder)
        {
            ValidateFolder(folder, false);
            var keys = (urls ?? Enumerable.Empty<string>())
                .Where(url => url is not null && url.StartsWith(_storage.PublicObjectPrefix, StringComparison.Ordinal))
                .Select(url => url[_storage.PublicObjectPrefix.Length..])
                .Where(key => IsObjectKey(key, folder));
            return _storage.DeleteAsync(keys, false, CancellationToken.None);
        }

        public Task DeletePrivateFilesAsync(IEnumerable<string>? paths, string ownerId)
        {
            var folder = $"verifications/{ownerId}";
            ValidateFolder(folder, true);
            return _storage.DeleteAsync((paths ?? Enumerable.Empty<string>()).Where(key => IsObjectKey(key, folder)),
                true, CancellationToken.None);
        }

        public async Task<StoredPrivateFile?> ReadPrivateFileAsync(string? path, string ownerId, CancellationToken cancellationToken = default)
        {
            var folder = $"verifications/{ownerId}";
            ValidateFolder(folder, true);
            if (!IsObjectKey(path, folder)) return null;
            var content = await _storage.DownloadPrivateAsync(path!, cancellationToken);
            return content is null ? null : new StoredPrivateFile(content, ContentType(Path.GetExtension(path!)));
        }

        private static void ValidateFolder(string folder, bool isPrivate)
        {
            var pattern = isPrivate
                ? @"\Averifications/[A-Za-z0-9_-]{1,128}\z"
                : @"\A(?:equipment|godowns)/[A-Za-z0-9_-]{1,128}\z";
            if (string.IsNullOrEmpty(folder) || !Regex.IsMatch(folder, pattern, RegexOptions.CultureInvariant))
                throw new ArgumentException("Invalid storage folder.", nameof(folder));
        }

        private static bool IsObjectKey(string? key, string folder)
        {
            if (key is null || !key.StartsWith(folder + "/", StringComparison.Ordinal)) return false;
            var fileName = key[(folder.Length + 1)..];
            return Regex.IsMatch(fileName, @"\A[a-f0-9]{32}\.(?:jpg|jpeg|png|webp)\z", RegexOptions.CultureInvariant);
        }

        private static string ContentType(string extension) => extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => throw new ArgumentException("Unsupported image extension.", nameof(extension))
        };

        private static bool IsValidImageHeader(IFormFile file, string extension)
        {
            try
            {
                using var stream = file.OpenReadStream();
                if (stream.Length < 12) return false;

                var header = new byte[12];
                var bytesRead = stream.Read(header, 0, 12);
                if (bytesRead < 12) return false;

                var ext = extension.ToLowerInvariant();

                // JPEG: FF D8 FF
                if (ext is ".jpg" or ".jpeg")
                {
                    return header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
                }

                // PNG: 89 50 4E 47 (0x89 'P' 'N' 'G')
                if (ext is ".png")
                {
                    return header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47;
                }

                // WebP: 'RIFF' .... 'WEBP'
                if (ext is ".webp")
                {
                    var isRiff = header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46;
                    var isWebp = header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50;
                    return isRiff && isWebp;
                }
            }
            catch (IOException)
            {
                return false;
            }

            return false;
        }
    }
}
