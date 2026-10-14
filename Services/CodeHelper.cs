using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace EncurtadorLink.Services;

public static class CodeHelper
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private static readonly HashSet<string> ReservedWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "api", "css", "js", "img", "favicon.ico", "assets", "static", "qr", "admin", "links"
    };

    public static string GenerateRandomCode(int length = 6)
    {
        var chars = new char[length];
        var bytes = new byte[length];
        RandomNumberGenerator.Fill(bytes);

        for (int i = 0; i < length; i++)
        {
            chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        }

        return new string(chars);
    }

    public static (bool IsValid, string? Error) ValidateCustomCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return (false, "O nome personalizado não pode ser vazio.");
        }

        code = code.Trim();

        if (code.Length < 2 || code.Length > 50)
        {
            return (false, "O nome personalizado deve ter entre 2 e 50 caracteres.");
        }

        if (!Regex.IsMatch(code, "^[a-zA-Z0-9_-]+$"))
        {
            return (false, "O nome personalizado pode conter apenas letras, números, hífens (-) e sublinhados (_).");
        }

        if (ReservedWords.Contains(code))
        {
            return (false, $"O nome '{code}' é reservado pelo sistema. Por favor, escolha outro.");
        }

        return (true, null);
    }

    public static (bool IsValid, string NormalizedUrl, string? Error) NormalizeAndValidateUrl(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return (false, string.Empty, "A URL de destino é obrigatória.");
        }

        var url = rawUrl.Trim();

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return (false, string.Empty, "URL inválida. Certifique-se de digitar um endereço válido (ex: https://seusite.com).");
        }

        return (true, url, null);
    }
}
