using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;
using OrbiApp.Presentation;
using OrbiApp.Presentation.Security;
using System.Text.Encodings.Web;
using Xunit;

namespace OrbiApp.Contract.Tests.Security;

public sealed class CspSecurityPolicyTests
{
    [Fact]
    public void Policy_UsesExplicitFallbacksAndRequestNonceWithoutWildcardScriptSources()
    {
        var policy = CspSecurityPolicy.Build("test-nonce");

        Assert.Contains("default-src 'self'", policy);
        Assert.Contains("object-src 'none'", policy);
        Assert.Contains("script-src 'self' 'nonce-test-nonce' 'wasm-unsafe-eval'", policy);
        Assert.Contains("style-src 'self' 'nonce-test-nonce'", policy);
        Assert.Contains("style-src-attr 'unsafe-inline'", policy);
        Assert.DoesNotContain("script-src 'unsafe-inline'", policy);
        Assert.DoesNotContain("*", policy);
    }

    [Theory]
    [InlineData("script")]
    [InlineData("style")]
    public void NonceTagHelper_AddsTheCurrentResponseNonce(string tagName)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items[CspSecurityPolicy.NonceItemKey] = "request-nonce";
        var output = new TagHelperOutput(tagName, new TagHelperAttributeList(),
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        var helper = new CspNonceTagHelper
        {
            ViewContext = new ViewContext { HttpContext = httpContext }
        };

        helper.Process(
            new TagHelperContext(new TagHelperAttributeList(), new Dictionary<object, object>(), "test"),
            output);

        Assert.Equal("request-nonce", output.Attributes["nonce"].Value);
    }
}
