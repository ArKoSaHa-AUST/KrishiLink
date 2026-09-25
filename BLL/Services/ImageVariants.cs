using System.Text.RegularExpressions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace KrishiLink.BLL.Services
{
    /// <summary>
    /// Small listing thumbnails for slow connections (REA-02). Each upload is stored twice: the original as
    /// <c>{guid}_o.{ext}</c> and a 400 px WebP beside it as <c>{guid}_w400.webp</c>. Grids and cards ask for the thumbnail;
    /// the gallery keeps the original. Images uploaded before thumbnails existed keep their plain <c>{guid}.{ext}</c> name,
    /// so <see cref="ThumbnailUrl"/> returns them unchanged and never points at a file that is not there.
    /// </summary>
    public static class ImageVariants
    {
        public const int ThumbnailWidth = 400;
        public const int ThumbnailQuality = 70;

        /// <summary>Refuse to decode anything larger: a 5 MB file can still claim enormous pixel dimensions.</summary>
        public const int MaxSourcePixels = 50_000_000;

        private const string OriginalMarker = "_o";
        private const string ThumbnailSuffix = "_w400.webp";

        private static readonly Regex OriginalName = new(@"_o\.(?:jpg|jpeg|png|webp)\z", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        public static string OriginalKey(string folder, string id, string extension, bool hasThumbnail) =>
            $"{folder}/{id}{(hasThumbnail ? OriginalMarker : string.Empty)}{extension}";

        /// <summary>The thumbnail beside an original, or null for a key stored without one.</summary>
        public static string? ThumbnailKey(string originalKey) =>
            OriginalName.IsMatch(originalKey) ? OriginalName.Replace(originalKey, ThumbnailSuffix) : null;

        /// <summary>The small version of a listing image when one was made at upload; otherwise the image itself.</summary>
        public static string ThumbnailUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return url ?? string.Empty;
            var queryAt = url.IndexOf('?');
            var path = queryAt < 0 ? url : url[..queryAt];
            return OriginalName.IsMatch(path) ? OriginalName.Replace(path, ThumbnailSuffix) + (queryAt < 0 ? string.Empty : url[queryAt..]) : url;
        }

        /// <summary>
        /// A 400 px wide WebP (never enlarged), upright and without metadata — so no camera GPS position is republished.
        /// Null when the image cannot be decoded safely; the upload then goes ahead without a thumbnail.
        /// </summary>
        public static byte[]? CreateThumbnail(Stream source)
        {
            try
            {
                var info = Image.Identify(source);
                if ((long)info.Width * info.Height > MaxSourcePixels) return null;
                source.Position = 0;

                using var image = Image.Load(source);
                image.Mutate(x =>
                {
                    x.AutoOrient();
                    if (image.Width > ThumbnailWidth) x.Resize(ThumbnailWidth, 0);
                });
                image.Metadata.ExifProfile = null;
                image.Metadata.XmpProfile = null;
                image.Metadata.IptcProfile = null;
                image.Metadata.IccProfile = null;

                using var output = new MemoryStream();
                image.SaveAsWebp(output, new WebpEncoder { Quality = ThumbnailQuality, FileFormat = WebpFileFormatType.Lossy });
                return output.ToArray();
            }
            catch (Exception ex) when (ex is ImageFormatException or UnknownImageFormatException or InvalidImageContentException or NotSupportedException or IOException)
            {
                return null;
            }
        }
    }
}
