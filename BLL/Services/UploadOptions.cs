namespace KrishiLink.BLL.Services
{
    public class UploadOptions
    {
        public const string SectionName = "UploadOptions";

        public int MaxImagesPerListing { get; set; } = 8;
        public long MaxFileSizeMb { get; set; } = 5;
        public long MaxMultipartBodyLengthMb { get; set; } = 32;
    }
}
