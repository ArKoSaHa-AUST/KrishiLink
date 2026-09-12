using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace KrishiLink.BLL.Services
{
    public interface IFileStorageService
    {
        /// <summary>Validates uploaded files against allowed image extensions, MIME types, magic byte signatures, and size limits (5 MB). Returns a list of human-readable error messages, or an empty list if all are valid.</summary>
        List<string> ValidateFiles(IEnumerable<IFormFile>? files);

        /// <summary>Stores validated uploaded images under wwwroot/uploads/{folder} with unique GUID filenames and returns their public URLs.</summary>
        Task<List<string>> SaveImagesAsync(IEnumerable<IFormFile>? files, string folder);

        /// <summary>Stores validated uploaded identity documents under App_Data/{folder} with unique GUID filenames and returns their relative private paths.</summary>
        Task<List<string>> SavePrivateFilesAsync(IEnumerable<IFormFile>? files, string folder);

        /// <summary>Safely deletes an image file from the wwwroot/uploads/ directory. Returns true if the file was found and deleted.</summary>
        bool DeleteImage(string? relativeUrl);

        /// <summary>Safely deletes multiple image files from the wwwroot/uploads/ directory.</summary>
        void DeleteFiles(IEnumerable<string>? relativeUrls);

        /// <summary>Safely deletes a private file from the App_Data/ directory. Returns true if the file was found and deleted.</summary>
        bool DeletePrivateFile(string? relativePath);

        /// <summary>Safely deletes multiple private files from the App_Data/ directory.</summary>
        void DeletePrivateFiles(IEnumerable<string>? relativePaths);
    }

    public class FileStorageService : IFileStorageService
    {
        public const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
        private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/pjpeg", "image/png", "image/webp" };

        private readonly IWebHostEnvironment _env;

        public FileStorageService(IWebHostEnvironment env)
        {
            _env = env;
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

        public async Task<List<string>> SaveImagesAsync(IEnumerable<IFormFile>? files, string folder)
        {
            var urls = new List<string>();
            if (files is null) return urls;

            var uploadsRoot = Path.Combine(_env.WebRootPath, "uploads", folder);
            Directory.CreateDirectory(uploadsRoot);

            foreach (var file in files)
            {
                if (file is null || file.Length == 0 || file.Length > MaxFileSizeBytes) continue;

                var extension = Path.GetExtension(file.FileName);
                if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension)) continue;
                if (!IsValidImageHeader(file, extension)) continue;

                var uniqueFileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
                var destinationPath = Path.Combine(uploadsRoot, uniqueFileName);

                await using (var stream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await file.CopyToAsync(stream);
                }

                urls.Add($"/uploads/{folder}/{uniqueFileName}");
            }

            return urls;
        }

        public async Task<List<string>> SavePrivateFilesAsync(IEnumerable<IFormFile>? files, string folder)
        {
            var paths = new List<string>();
            if (files is null) return paths;

            var targetDir = Path.Combine(_env.ContentRootPath, "App_Data", folder);
            Directory.CreateDirectory(targetDir);

            foreach (var file in files)
            {
                if (file is null || file.Length == 0 || file.Length > MaxFileSizeBytes) continue;

                var extension = Path.GetExtension(file.FileName);
                if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension)) continue;
                if (!IsValidImageHeader(file, extension)) continue;

                var uniqueFileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
                var destinationPath = Path.Combine(targetDir, uniqueFileName);

                await using (var stream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await file.CopyToAsync(stream);
                }

                paths.Add(Path.Combine(folder, uniqueFileName).Replace('\\', '/'));
            }

            return paths;
        }

        public bool DeletePrivateFile(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return false;

            try
            {
                var cleanPath = relativePath.TrimStart('~', '/').Replace('/', Path.DirectorySeparatorChar);
                var fullPath = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "App_Data", cleanPath));
                var appDataDir = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "App_Data"));

                if (!fullPath.StartsWith(appDataDir, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    return true;
                }
            }
            catch
            {
                // Silently ignore
            }

            return false;
        }

        public void DeletePrivateFiles(IEnumerable<string>? relativePaths)
        {
            if (relativePaths is null) return;
            foreach (var path in relativePaths)
            {
                DeletePrivateFile(path);
            }
        }

        public bool DeleteImage(string? relativeUrl)
        {
            if (string.IsNullOrWhiteSpace(relativeUrl)) return false;

            try
            {
                var cleanPath = relativeUrl.TrimStart('~', '/').Replace('/', Path.DirectorySeparatorChar);
                if (!cleanPath.StartsWith("uploads" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    return false; // Only delete files inside wwwroot/uploads/
                }

                var fullPath = Path.GetFullPath(Path.Combine(_env.WebRootPath, cleanPath));
                var uploadsDir = Path.GetFullPath(Path.Combine(_env.WebRootPath, "uploads"));

                // Path traversal check
                if (!fullPath.StartsWith(uploadsDir, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    return true;
                }
            }
            catch
            {
                // Silently ignore file deletion errors (e.g. file lock or permissions)
            }

            return false;
        }

        public void DeleteFiles(IEnumerable<string>? relativeUrls)
        {
            if (relativeUrls is null) return;
            foreach (var url in relativeUrls)
            {
                DeleteImage(url);
            }
        }

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
            catch
            {
                return false;
            }

            return false;
        }
    }
}
