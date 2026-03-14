using DP.Api.Services;
using System.Text.Json;

namespace DP.Api.Middleware;

public class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    public IdempotencyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IServiceProvider serviceProvider)
    {
        if (context.Request.Method != HttpMethods.Post)
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(IdempotencyKeyHeader, out var keyValues) || string.IsNullOrEmpty(keyValues.ToString()))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { Error = "Idempotency-Key header is required" });
            return;
        }

        var key = keyValues.ToString();
        var idempotencyService = serviceProvider.GetRequiredService<IIdempotencyService>();

        var cachedResponse = await idempotencyService.GetCachedResponseAsync(key);

        if (cachedResponse != null)
        {
            context.Response.StatusCode = cachedResponse.StatusCode;
            context.Response.ContentType = "application/json";
            if (cachedResponse.ResponseBody != null)
            {
                await context.Response.WriteAsync(cachedResponse.ResponseBody);
            }
            return;
        }

        // Intercept response body
        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        context.Request.EnableBuffering();
        var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
        context.Request.Body.Position = 0;

        await _next(context);

        if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 300)
        {
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var responseText = await new StreamReader(context.Response.Body).ReadToEndAsync();
            context.Response.Body.Seek(0, SeekOrigin.Begin);

            await idempotencyService.CacheResponseAsync(key, requestBody, responseText, context.Response.StatusCode);
        }

        await responseBody.CopyToAsync(originalBodyStream);
    }
}
