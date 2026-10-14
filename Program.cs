using EncurtadorLink.Data;
using EncurtadorLink.DTOs;
using EncurtadorLink.Models;
using EncurtadorLink.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configuração do suporte a proxy reverso do Render (HTTPS, Host e IP corretos)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Suporte a CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

// Configuração de Banco de Dados: Suporta PostgreSQL do Render (DATABASE_URL) ou SQLite como fallback
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL") 
                  ?? builder.Configuration.GetConnectionString("DATABASE_URL");

if (!string.IsNullOrWhiteSpace(databaseUrl))
{
    try
    {
        var pgConnString = ConvertDatabaseUrlToPostgresConnString(databaseUrl);
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(pgConnString));
        Console.WriteLine("[DB] Usando PostgreSQL do Render (DATABASE_URL detectada).");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DB AVISO] Erro ao configurar PostgreSQL ({ex.Message}). Usando SQLite embutido como fallback.");
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite("Data Source=links.db"));
    }
}
else
{
    var sqliteConn = builder.Configuration.GetConnectionString("DefaultConnection") 
                     ?? "Data Source=links.db";
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite(sqliteConn));
    Console.WriteLine("[DB] Usando SQLite embutido.");
}

// Serviço de geração de QR Code
builder.Services.AddSingleton<IQrCodeService, QrCodeService>();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseCors();

// Garante que o banco de dados e tabelas existam
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        Console.WriteLine("[DB] Banco de dados e tabelas verificados com sucesso.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DB ERRO] Falha ao inicializar o banco de dados: {ex.Message}");
    }
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

    var scheme = context.Request.Headers["X-Forwarded-Proto"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(scheme))
    {
        scheme = context.Request.Scheme;
    }

    var host = context.Request.Headers["X-Forwarded-Host"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(host))
    {
        host = context.Request.Host.Value;
    }

    return $"{scheme}://{host}";
}

// Endpoint de Diagnóstico / Saúde
app.MapGet("/api/health", async (AppDbContext db) =>
{
    try
    {
        var canConnect = await db.Database.CanConnectAsync();
        var totalLinks = await db.ShortenedUrls.CountAsync();
        var provider = db.Database.ProviderName;
        return Results.Ok(new 
        { 
            status = "Online", 
            banco = provider, 
            conectado = canConnect, 
            totalLinks 
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: 500, title: "Erro na conexão com o banco");
    }
});

// 1. Criar novo link encurtado (com ou sem nome personalizado)
app.MapPost("/api/shorten", async (
    [FromBody] ShortenRequest request, 
    AppDbContext db, 
    HttpContext context) =>
{
    try
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
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERRO /api/shorten] {ex}");
        return Results.Problem(detail: ex.Message, statusCode: 500, title: "Erro ao encurtar o link");
    }
});

// 2. Listar todos os links cadastrados (do mais recente para o mais antigo)
app.MapGet("/api/links", async (AppDbContext db, HttpContext context) =>
{
    try
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
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERRO /api/links] {ex}");
        return Results.Problem(detail: ex.Message, statusCode: 500, title: "Erro ao listar links");
    }
});

// 3. Excluir um link por código
app.MapDelete("/api/links/{code}", async (string code, AppDbContext db) =>
{
    try
    {
        var link = await db.ShortenedUrls.FirstOrDefaultAsync(u => u.Code.ToLower() == code.ToLower());
        if (link is null)
        {
            return Results.NotFound(new { error = "Link não encontrado." });
        }

        db.ShortenedUrls.Remove(link);
        await db.SaveChangesAsync();

        return Results.Ok(new { message = $"O link '{code}' foi removido com sucesso." });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERRO DELETE /api/links] {ex}");
        return Results.Problem(detail: ex.Message, statusCode: 500, title: "Erro ao excluir link");
    }
});

// 4. Gerar e retornar imagem do QR Code em PNG
app.MapGet("/api/qr/{code}", async (
    string code, 
    AppDbContext db, 
    IQrCodeService qrService, 
    HttpContext context,
    [FromQuery] int? size) =>
{
    try
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
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERRO /api/qr] {ex}");
        return Results.Problem(detail: ex.Message, statusCode: 500, title: "Erro ao gerar QR Code");
    }
});

// 5. Redirecionamento de link encurtado permanente /{code}
app.MapGet("/{code}", async (string code, AppDbContext db) =>
{
    if (code.Contains('.'))
    {
        return Results.NotFound();
    }

    try
    {
        var link = await db.ShortenedUrls.FirstOrDefaultAsync(u => u.Code.ToLower() == code.ToLower());
        if (link is null)
        {
            var html404 = $@"<!DOCTYPE html>
<html lang=""pt-BR"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Link Não Encontrado</title>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background: #080c14; color: #f8fafc; display: flex; align-items: center; justify-content: center; min-height: 100vh; margin: 0; text-align: center; }}
        .card {{ background: #0f172a; padding: 2.5rem; border-radius: 1rem; border: 1px solid rgba(255,255,255,0.1); max-width: 450px; box-shadow: 0 10px 25px rgba(0,0,0,0.5); }}
        h1 {{ font-size: 3rem; margin: 0 0 0.5rem; color: #f43f5e; }}
        p {{ color: #94a3b8; font-size: 1.1rem; line-height: 1.5; }}
        .code {{ background: #1e293b; color: #38bdf8; padding: 0.2rem 0.5rem; border-radius: 4px; font-family: monospace; }}
        a {{ display: inline-block; margin-top: 1.5rem; background: #6366f1; color: white; padding: 0.75rem 1.5rem; border-radius: 0.5rem; text-decoration: none; font-weight: 600; }}
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

        return Results.Redirect(link.OriginalUrl, permanent: false);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERRO /{code}] {ex}");
        return Results.Problem(detail: ex.Message, statusCode: 500, title: "Erro no redirecionamento");
    }
});

var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    Console.WriteLine($"[SERVER] Iniciando na porta {port} (Render)");
    app.Run($"http://0.0.0.0:{port}");
}
else
{
    app.Run();
}

static string ConvertDatabaseUrlToPostgresConnString(string databaseUrl)
{
    if (databaseUrl.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
        databaseUrl.Contains("Server=", StringComparison.OrdinalIgnoreCase))
    {
        return databaseUrl;
    }

    if (databaseUrl.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
    {
        databaseUrl = "postgresql://" + databaseUrl["postgres://".Length..];
    }

    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':');
    var username = Uri.UnescapeDataString(userInfo[0]);
    var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
    var port = uri.Port > 0 ? uri.Port : 5432;
    var database = uri.AbsolutePath.TrimStart('/');

    return $"Host={uri.Host};Port={port};Database={database};Username={username};Password={password};SSL Mode=Prefer;Trust Server Certificate=true;Timeout=15;Command Timeout=30;";
}
