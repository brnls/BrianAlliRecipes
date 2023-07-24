using System.Net.Mime;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDirectoryBrowser();

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedHost
    | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
});

app.Use(async (ctx, next) =>
{
    const string pathBaseHeader = "X-Forwarded-PathBase";
    if (ctx.Request.Headers.TryGetValue(pathBaseHeader, out var value))
    {
        ctx.Request.PathBase = value[0];
    }
    await next();
});


var pdfRegex = new Regex("\\[\\[(.*)\\]\\]");

app.Map("/{path}", async (string path, IWebHostEnvironment env) =>
{
    var provider = env.WebRootFileProvider;
    var file = provider.GetFileInfo(path);
    if (file.Exists && !file.IsDirectory && file.Name.EndsWith(".md"))
    {
        using var ms = new MemoryStream();
        await file.CreateReadStream().CopyToAsync(ms);
        var markdown = Encoding.UTF8.GetString(ms.ToArray());
        var pdf = pdfRegex.Match(markdown);
        if (pdf.Success)
        {
            var pdfPath = Path.Combine("Content", pdf.Groups[1].Value);
            return Results.Redirect(pdfPath);
        }
        else return new HtmlResult(Markdown.ToHtml(markdown));
    }
    return Results.NotFound();
});

app.UseStaticFiles();
app.UseDirectoryBrowser();

app.Run();

class HtmlResult : IResult
{
    private readonly string _html;

    public HtmlResult(string html)
    {
        _html = html;
    }

    public Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.ContentType = MediaTypeNames.Text.Html;
        httpContext.Response.ContentLength = Encoding.UTF8.GetByteCount(_html);
        return httpContext.Response.WriteAsync(_html);
    }
}