namespace EncurtadorLink.DTOs;

public record ShortenRequest(string Url, string? CustomCode);

public record ShortenResponse(
    string Code,
    string ShortUrl,
    string OriginalUrl,
    DateTime CreatedAt,
    string QrCodeUrl
);

public record LinkItemDto(
    int Id,
    string Code,
    string ShortUrl,
    string OriginalUrl,
    DateTime CreatedAt,
    string QrCodeUrl
);
