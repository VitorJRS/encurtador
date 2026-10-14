using QRCoder;

namespace EncurtadorLink.Services;

public interface IQrCodeService
{
    byte[] GeneratePng(string text, int pixelsPerModule = 15);
}

public class QrCodeService : IQrCodeService
{
    public byte[] GeneratePng(string text, int pixelsPerModule = 15)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Texto do QR Code não pode ser vazio.", nameof(text));
        }

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrCodeData);
        return qrCode.GetGraphic(pixelsPerModule);
    }
}
