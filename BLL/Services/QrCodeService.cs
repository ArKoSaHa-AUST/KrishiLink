using System;
using QRCoder;

namespace KrishiLink.BLL.Services
{
    public interface IQrCodeService
    {
        string GenerateSvg(string payload, int pixelsPerModule = 10);
        string GenerateBase64Png(string payload, int pixelsPerModule = 10);
    }

    public class QrCodeService : IQrCodeService
    {
        public string GenerateSvg(string payload, int pixelsPerModule = 10)
        {
            if (string.IsNullOrWhiteSpace(payload)) return string.Empty;
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
            var qrCode = new SvgQRCode(data);
            return qrCode.GetGraphic(pixelsPerModule);
        }

        public string GenerateBase64Png(string payload, int pixelsPerModule = 10)
        {
            if (string.IsNullOrWhiteSpace(payload)) return string.Empty;
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
            var qrCode = new PngByteQRCode(data);
            var bytes = qrCode.GetGraphic(pixelsPerModule);
            return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
        }
    }
}
