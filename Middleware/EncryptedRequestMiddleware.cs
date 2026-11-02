using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Primitives;
using SankalpAPI.Security;

namespace SankalpAPI.Middleware;

/// <summary>Unwraps web requests using the existing AES/RSA wire format before model binding.</summary>
public sealed class EncryptedRequestMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        if (!request.Path.StartsWithSegments("/api") || request.Headers["X-Encrypted-Request"] != "1")
        {
            await next(context);
            return;
        }

        var originalBody = request.Body;
        MemoryStream? decodedBody = null;
        try
        {
            var key = RSA_Enc_Dec.Decryption(request.Headers["X-Request-Key"].ToString());
            if (request.Query.TryGetValue("__encryptedQuery", out var query))
                request.QueryString = new QueryString("?" + Aes256CbcEncrypterApp.Decrypt(query.ToString(), key));

            if (request.HasFormContentType)
            {
                var form = await request.ReadFormAsync(context.RequestAborted);
                var fields = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
                foreach (var field in form)
                    fields[field.Key] = new StringValues(field.Value.Select(value =>
                        JsonSerializer.Deserialize<string>(Aes256CbcEncrypterApp.Decrypt(value ?? "", key)) ?? "").ToArray());
                request.Form = new FormCollection(fields, form.Files);
            }
            else if (request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) == true)
            {
                using var envelope = await JsonDocument.ParseAsync(request.Body, cancellationToken: context.RequestAborted);
                var plaintext = Aes256CbcEncrypterApp.Decrypt(envelope.RootElement.GetProperty("payload").GetString()!, key);
                // Validate before passing to controllers. The original helper accepts plaintext on failure.
                using var validation = JsonDocument.Parse(plaintext);
                decodedBody = new MemoryStream(Encoding.UTF8.GetBytes(plaintext));
                request.Body = decodedBody;
                request.ContentLength = decodedBody.Length;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            decodedBody?.Dispose();
            request.Body = originalBody;
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { message = "Invalid encrypted request." });
            return;
        }
        try { await next(context); }
        finally
        {
            request.Body = originalBody;
            decodedBody?.Dispose();
        }
    }
}
