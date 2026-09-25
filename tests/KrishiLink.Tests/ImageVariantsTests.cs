using KrishiLink.BLL.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace KrishiLink.Tests;

/// <summary>REA-02: grids get a small WebP, and an image without one keeps working.</summary>
public class ImageVariantsTests
{
    private const string Prefix = "https://abc.supabase.co/storage/v1/object/public/listing-images/equipment/owner1/";

    [Fact]
    public void An_original_with_a_thumbnail_maps_to_its_webp_companion()
    {
        var key = ImageVariants.OriginalKey("equipment/owner1", "0123456789abcdef0123456789abcdef", ".jpg", hasThumbnail: true);
        Assert.Equal("equipment/owner1/0123456789abcdef0123456789abcdef_o.jpg", key);
        Assert.Equal("equipment/owner1/0123456789abcdef0123456789abcdef_w400.webp", ImageVariants.ThumbnailKey(key));
        Assert.Equal(Prefix + "0123456789abcdef0123456789abcdef_w400.webp", ImageVariants.ThumbnailUrl(Prefix + "0123456789abcdef0123456789abcdef_o.jpg"));
    }

    [Theory]
    [InlineData(Prefix + "0123456789abcdef0123456789abcdef.jpg")]          // uploaded before thumbnails existed
    [InlineData("https://images.example.com/tractor.png")]               // seeded or external
    [InlineData("/images/placeholder.png")]
    [InlineData("")]
    public void Images_without_a_thumbnail_are_served_unchanged(string url) => Assert.Equal(url, ImageVariants.ThumbnailUrl(url));

    [Fact]
    public void A_query_string_survives_the_mapping() =>
        Assert.Equal(Prefix + "a_w400.webp?v=2", ImageVariants.ThumbnailUrl(Prefix + "a_o.png?v=2"));

    [Fact]
    public void Keys_stored_without_a_thumbnail_have_no_companion() =>
        Assert.Null(ImageVariants.ThumbnailKey("equipment/owner1/0123456789abcdef0123456789abcdef.jpg"));

    [Fact]
    public void The_thumbnail_is_a_400px_webp_without_camera_metadata()
    {
        using var source = new Image<Rgb24>(1600, 1200, new Rgb24(40, 120, 60));
        source.Metadata.ExifProfile = new ExifProfile();
        source.Metadata.ExifProfile.SetValue(ExifTag.Make, "PhoneCam");
        using var input = new MemoryStream();
        source.SaveAsJpeg(input);
        input.Position = 0;

        var bytes = ImageVariants.CreateThumbnail(input);

        Assert.NotNull(bytes);
        using var thumb = Image.Load(bytes);
        Assert.Equal("WEBP", Image.DetectFormat(bytes).Name.ToUpperInvariant());
        Assert.Equal((400, 300), (thumb.Width, thumb.Height));
        Assert.Null(thumb.Metadata.ExifProfile);
        Assert.True(bytes!.Length < input.Length);
    }

    [Fact]
    public void A_small_image_is_never_enlarged()
    {
        using var source = new Image<Rgb24>(300, 200);
        using var input = new MemoryStream();
        source.SaveAsPng(input);
        input.Position = 0;
        using var thumb = Image.Load(ImageVariants.CreateThumbnail(input)!);
        Assert.Equal((300, 200), (thumb.Width, thumb.Height));
    }

    [Fact]
    public void Undecodable_bytes_give_no_thumbnail_instead_of_failing_the_upload()
    {
        using var input = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4, 5, 6, 7, 8 });
        Assert.Null(ImageVariants.CreateThumbnail(input));
    }
}
