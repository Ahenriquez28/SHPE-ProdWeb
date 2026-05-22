using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Net.Http;

// The ClerkPrincipal represents the verified identity extracted from a Clerk JWT
// Once verified, we attach this to the request so endpoints can read who's calling
public record ClerkPrincipal
{
    public required string ClerkUserId { get; init; }
    public string? Email { get; init; }
}

// Interface so we can swap out the implementation in tests
public interface IClerkJwtVerifier
{
    Task<ClerkPrincipal?> VerifyAsync(string token, CancellationToken ct);
}

public class ClerkJwtVerifier : IClerkJwtVerifier
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly string _issuer;
    private readonly string _jwksUrl;

    public ClerkJwtVerifier(IConfiguration cfg, IMemoryCache cache)
    {
        _http = new HttpClient();
        _cache = cache;

        // Clerk issuer URL — comes from appsettings or environment variable
        _issuer = cfg["Clerk:Issuer"]!;

        // JWKS = JSON Web Key Set — Clerk's public keys used to verify JWT signatures
        _jwksUrl = $"{_issuer}/.well-known/jwks.json";
    }

    public async Task<ClerkPrincipal?> VerifyAsync(string token, CancellationToken ct)
    {
        // Cache the JWKS for 1 hour so we don't hit Clerk's servers on every request
        var jwks = await _cache.GetOrCreateAsync("clerk-jwks", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            var json = await _http.GetStringAsync(_jwksUrl, ct);
            return new JsonWebKeySet(json);
        });

        var handler = new JsonWebTokenHandler();

        var parameters = new TokenValidationParameters
        {
            // Verify the token was issued by our Clerk app
            ValidateIssuer = true,
            ValidIssuer = _issuer,

            // We don't use audiences in this setup
            ValidateAudience = false,

            // Use Clerk's public keys to verify the signature
            IssuerSigningKeys = jwks!.Keys,

            // Reject expired tokens
            ValidateLifetime = true,
        };

        try
        {
            var result = await handler.ValidateTokenAsync(token, parameters);

            if (!result.IsValid) return null;

            // Extract the Clerk user ID (sub claim) and email from the token
            result.Claims.TryGetValue("sub", out var sub);
            result.Claims.TryGetValue("email", out var email);

            return new ClerkPrincipal
            {
                ClerkUserId = sub?.ToString() ?? "",
                Email = email?.ToString(),
            };
        }
        catch
        {
            // Any validation failure returns null — the middleware will treat as unauthenticated
            return null;
        }
    }
}