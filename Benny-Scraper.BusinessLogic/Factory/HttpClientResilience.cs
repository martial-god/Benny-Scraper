using System.Net;
using Polly;
using Polly.Retry;

namespace Benny_Scraper.BusinessLogic.Factory;

public static class HttpClientResilience
{
    public static ResiliencePipeline<HttpResponseMessage> CreatePipeline(
        Config.HttpResilienceSettings? settings = null)
    {
        settings ??= new Config.HttpResilienceSettings();

        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = settings.MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromSeconds(settings.BaseDelaySeconds),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>()
                    .HandleResult(static response =>
                        response.StatusCode == HttpStatusCode.RequestTimeout ||
                        response.StatusCode == HttpStatusCode.TooManyRequests ||
                        (int)response.StatusCode >= 500),
                DelayGenerator = static args =>
                {
                    if (args.Outcome.Result?.Headers.RetryAfter?.Delta is { } retryAfter)
                    {
                        return new ValueTask<TimeSpan?>(retryAfter);
                    }

                    return new ValueTask<TimeSpan?>((TimeSpan?)null);
                }
            })
            .Build();
    }
}

internal sealed class ResilientHttpMessageHandler : DelegatingHandler
{
    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline;

    public ResilientHttpMessageHandler(HttpMessageHandler innerHandler, ResiliencePipeline<HttpResponseMessage> pipeline)
        : base(new NonDisposingDelegatingHandler(innerHandler))
    {
        _pipeline = pipeline;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return _pipeline.ExecuteAsync(
            async ct => await base.SendAsync(await CloneRequestMessageAsync(request, ct), ct),
            cancellationToken).AsTask();
    }

    private static async Task<HttpRequestMessage> CloneRequestMessageAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var option in request.Options)
        {
            clone.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);
        }

        if (request.Content != null)
        {
            var memoryStream = new MemoryStream();
            await request.Content.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;

            var contentClone = new StreamContent(memoryStream);
            foreach (var header in request.Content.Headers)
            {
                contentClone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            clone.Content = contentClone;
        }

        return clone;
    }
}

internal sealed class NonDisposingDelegatingHandler : DelegatingHandler
{
    public NonDisposingDelegatingHandler(HttpMessageHandler innerHandler)
        : base(innerHandler)
    {
    }

    protected override void Dispose(bool disposing)
    {
    }
}
