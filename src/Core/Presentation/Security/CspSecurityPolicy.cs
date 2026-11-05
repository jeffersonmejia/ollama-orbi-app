namespace OrbiApp.Presentation;

internal static class CspSecurityPolicy
{
    internal const string NonceItemKey = "Orbi.ContentSecurityPolicy.Nonce";

    internal static string Build(string nonce) => string.Join("; ",
    [
        "default-src 'self'",
        "base-uri 'self'",
        "object-src 'none'",
        $"script-src 'self' 'nonce-{nonce}' 'wasm-unsafe-eval' https://cdnjs.cloudflare.com",
        $"style-src 'self' 'nonce-{nonce}'",
        "style-src-attr 'unsafe-inline'",
        "img-src 'self' data: blob: https:",
        "font-src 'self' data: https://fonts.gstatic.com",
        "connect-src 'self'",
        "form-action 'self'",
        "frame-ancestors 'self'",
        "frame-src 'self'",
        "manifest-src 'self'",
        "media-src 'self' blob:",
        "worker-src 'self' blob:"
    ]);
}
