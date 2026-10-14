using EncurtadorLink.Data;
using EncurtadorLink.DTOs;
using EncurtadorLink.Models;
using EncurtadorLink.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configuração do SQLite permanente
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                       ?? "Data Source=links.db";
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

// Serviço de geração de QR Code
builder.Services.AddSingleton<IQrCodeService, QrCodeService>();

var app = builder.Build();

// Garante que o banco de dados SQLite e tabelas existam
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseDefaultFiles();
app.UseStaticFiles();

// Helper para obter a URL base atual da aplicação
string GetBaseUrl(HttpContext context)
{
    var configuredBase = app.Configuration["BaseUrl"];
    if (!string.IsNullOrWhiteSpace(configuredBase))
    {
        return configuredBase.TrimEnd('/');
    }
    return $"{context.Request.Scheme}://{context.Request.Host}";
}

// 1. Criar novo link encurtado (com ou sem nome personalizado)
app.MapPost("/api/shorten", async (
    [FromBody] ShortenRequest request, 
    AppDbContext db, 
    HttpContext context) =>
{
    // Validação da URL de destino
    var (isUrlValid, normalizedUrl, urlError) = CodeHelper.NormalizeAndValidateUrl(request.Url);
    if (!isUrlValid)
    {
        return Results.BadRequest(new { error = urlError });
    }

    string code;

    // Se o usuário digitou um nome/slug personalizado
    if (!string.IsNullOrWhiteSpace(request.CustomCode))
    {
        code = request.CustomCode.Trim();
        var (isCodeValid, codeError) = CodeHelper.ValidateCustomCode(code);
        if (!isCodeValid)
        {
            return Results.BadRequest(new { error = codeError });
        }

        // Verifica se o código já está em uso
        var exists = await db.ShortenedUrls.AnyAsync(u => u.Code.ToLower() == code.ToLower());
        if (exists)
        {
            return Results.Conflict(new { error = $"O nome '{code}' já está sendo utilizado. Por favor, escolha outro nome." });
        }
    }
    else
    {
        // Gera um código aleatório e garante que não haja colisão
        int attempts = 0;
        do
        {
            code = CodeHelper.GenerateRandomCode(6);
            attempts++;
            if (attempts > 10)
            {
                code = CodeHelper.GenerateRandomCode(8);
            }
        } while (await db.ShortenedUrls.AnyAsync(u => u.Code == code));
    }

    var shortened = new ShortenedUrl
    {
        Code = code,
        OriginalUrl = normalizedUrl,
        CreatedAt = DateTime.UtcNow
    };

    db.ShortenedUrls.Add(shortened);
    await db.SaveChangesAsync();

    var baseUrl = GetBaseUrl(context);
    var shortUrl = $"{baseUrl}/{shortened.Code}";
    var qrCodeUrl = $"{baseUrl}/api/qr/{shortened.Code}";

    var response = new ShortenResponse(
        shortened.Code,
        shortUrl,
        shortened.OriginalUrl,
        shortened.CreatedAt,
        qrCodeUrl
    );

    return Results.Created($"/{shortened.Code}", response);
});

// 2. Listar todos os links cadastrados (do mais recente para o mais antigo)
app.MapGet("/api/links", async (AppDbContext db, HttpContext context) =>
{
    var baseUrl = GetBaseUrl(context);
    var links = await db.ShortenedUrls
        .OrderByDescending(u => u.CreatedAt)
        .Select(u => new LinkItemDto(
            u.Id,
            u.Code,
            $"{baseUrl}/{u.Code}",
            u.OriginalUrl,
            u.CreatedAt,
            $"{baseUrl}/api/qr/{u.Code}"
        ))
        .ToListAsync();

    return Results.Ok(links);
});

// 3. Excluir um link por código (se o usuário desejar liberar o nome)
app.MapDelete("/api/links/{code}", async (string code, AppDbContext db) =>
{
    var link = await db.ShortenedUrls.FirstOrDefaultAsync(u => u.Code.ToLower() == code.ToLower());
    if (link is null)
    {
        return Results.NotFound(new { error = "Link não encontrado." });
    }

    db.ShortenedUrls.Remove(link);
    await db.SaveChangesAsync();

    return Results.Ok(new { message = $"O link '{code}' foi removido com sucesso." });
});

// 4. Gerar e retornar imagem do QR Code em PNG
app.MapGet("/api/qr/{code}", async (
    string code, 
    AppDbContext db, 
    IQrCodeService qrService, 
    HttpContext context,
    [FromQuery] int? size) =>
{
    var link = await db.ShortenedUrls.FirstOrDefaultAsync(u => u.Code.ToLower() == code.ToLower());
    if (link is null)
    {
        return Results.NotFound("Link não encontrado.");
    }

    var baseUrl = GetBaseUrl(context);
    var shortUrl = $"{baseUrl}/{link.Code}";

    int pixels = size.HasValue && size.Value >= 5 && size.Value <= 50 ? size.Value : 15;
    var pngBytes = qrService.GeneratePng(shortUrl, pixels);

    return Results.File(pngBytes, "image/png", fileDownloadName: $"qrcode-{link.Code}.png");
});

// 5. Redirecionamento de link encurtador permanente /{code}
app.MapGet("/{code}", async (string code, AppDbContext db) =>
{
    // Ignora requisições a arquivos estáticos óbvios se chegarem aqui
    if (code.Contains('.'))
    {
        return Results.NotFound();
    }

    var link = await db.ShortenedUrls.FirstOrDefaultAsync(u => u.Code.ToLower() == code.ToLower());
    if (link is null)
    {
        // Retorna página 404 amigável
        var html404 = $@"<!DOCTYPE html>
<html lang=""pt-BR"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Link Não Encontrado</title>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background: #0f172a; color: #f8fafc; display: flex; align-items: center; justify-content: center; min-height: 100vh; margin: 0; text-align: center; }}
        .card {{ background: #1e293b; padding: 2.5rem; border-radius: 1rem; border: 1px solid #334155; max-width: 450px; box-shadow: 0 10px 25px rgba(0,0,0,0.5); }}
        h1 {{ font-size: 3rem; margin: 0 0 0.5rem; color: #f43f5e; }}
        p {{ color: #94a3b8; font-size: 1.1rem; line-height: 1.5; }}
        .code {{ background: #334155; color: #38bdf8; padding: 0.2rem 0.5rem; border-radius: 4px; font-family: monospace; }}
        a {{ display: inline-block; margin-top: 1.5rem; background: #6366f1; color: white; padding: 0.75rem 1.5rem; border-radius: 0.5rem; text-decoration: none; font-weight: 600; transition: background 0.2s; }}
        a:hover {{ background: #4f46e5; }}
    </style>
</head>
<body>
    <div class=""card"">
        <h1>404</h1>
        <h2>Link não encontrado</h2>
        <p>O link encurtado <span class=""code"">/{code}</span> não existe ou foi removido.</p>
        <a href=""/"">Ir para a Página Inicial</a>
    </div>
</body>
</html>";
        return Results.Content(html404, "text/html");
    }

    // Redirecionamento permanente ou temporário (302 é seguro para o navegador não cachear eternamente caso queira editar depois)
    return Results.Redirect(link.OriginalUrl, permanent: false);
});

var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    app.Run($"http://0.0.0.0:{port}");
}
else
{
    app.Run();
}
