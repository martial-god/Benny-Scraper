using Polly;

namespace BennyScraper.BusinessLogic.Factory;

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