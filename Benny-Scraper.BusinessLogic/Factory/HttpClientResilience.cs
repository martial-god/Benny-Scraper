using System.Net;
using Polly;
using Polly.Retry;

namespace BennyScraper.BusinessLogic.Factory;

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