using System.Net;
using System.Text.Json;
using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

public class RateLimitDetectorTests
{
    private static HttpRequestException WithStatus(HttpStatusCode status) =>
        new("provider failed", null, status);

    [Fact]
    public void NullIsNotARateLimit() => Assert.False(RateLimitDetector.IsRateLimit(null));

    [Fact]
    public void TooManyRequestsIsARateLimit() =>
        Assert.True(RateLimitDetector.IsRateLimit(WithStatus(HttpStatusCode.TooManyRequests)));

    [Fact]
    public void Overloaded529IsARateLimit() =>
        Assert.True(RateLimitDetector.IsRateLimit(WithStatus((HttpStatusCode)529)));

    [Fact]
    public void ForbiddenIsNotARateLimit() =>
        Assert.False(RateLimitDetector.IsRateLimit(WithStatus(HttpStatusCode.Forbidden)));

    [Fact]
    public void ServerErrorIsNotARateLimit() =>
        Assert.False(RateLimitDetector.IsRateLimit(WithStatus(HttpStatusCode.InternalServerError)));

    /// <summary>
    /// The shape GTranslate actually throws: the 429 aggregated with a JsonReaderException from parsing the
    /// error body.
    /// </summary>
    [Fact]
    public void AggregateWrappingA429IsARateLimit()
    {
        var aggregate = new AggregateException(
            WithStatus(HttpStatusCode.TooManyRequests),
            new JsonException("The input does not contain any JSON tokens."));

        Assert.True(RateLimitDetector.IsRateLimit(aggregate));
    }

    [Fact]
    public void NestedAggregateIsUnwrapped()
    {
        var inner = new AggregateException(WithStatus(HttpStatusCode.TooManyRequests));
        var outer = new AggregateException(inner);

        Assert.True(RateLimitDetector.IsRateLimit(outer));
    }

    [Fact]
    public void AggregateOfOrdinaryFailuresIsNotARateLimit()
    {
        var aggregate = new AggregateException(
            WithStatus(HttpStatusCode.Forbidden),
            new JsonException("broken"));

        Assert.False(RateLimitDetector.IsRateLimit(aggregate));
    }
}
