namespace KrishiLink.BLL.Services
{
    public interface IFileStorageService
    {
        /// <summary>Stores uploaded images under wwwroot/uploads/{folder} and returns their public URLs. Non-image or oversized files are skipped.</summary>
        Task<List<string>> SaveImagesAsync(IEnumerable<IFormFile>? files, string folder);
    }

    public class FileStorageService : IFileStorageService
    {
        private const long MaxBytes = 5 * 1024 * 1024;
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

        private readonly IWebHostEnvironment _env;

        public FileStorageService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public async Task<List<string>> SaveImagesAsync(IEnumerable<IFormFile>? files, string folder)
        {
            var urls = new List<string>();
            if (files is null) return urls;

            var directory = Path.Combine(_env.WebRootPath, "uploads", folder);
            Directory.CreateDirectory(directory);

            foreach (var file in files)
            {
                var extension = Path.GetExtension(file.FileName);
                if (file.Length == 0 || file.Length > MaxBytes || !AllowedExtensions.Contains(extension)) continue;

                var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
                await using var stream = File.Create(Path.Combine(directory, fileName));
                await file.CopyToAsync(stream);
                urls.Add($"/uploads/{folder}/{fileName}");
            }

            return urls;
        }
    }
}
