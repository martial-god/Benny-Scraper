using BennyScraper.BusinessLogic.Config;
using BennyScraper.BusinessLogic.Factory;
using BennyScraper.BusinessLogic.Factory.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Polly;

namespace BennyScraper.BusinessLogic.Extensions;

internal static class DependencyInjectionExtensions
{
    public static IServiceCollection AddHttpResilience(this IServiceCollection services)
    {
        services.AddSingleton<ResiliencePipeline<HttpResponseMessage>>(provider =>
            {
                var settings = provider.GetRequiredService<NovelScraperSettings>();
                return HttpClientResilience.CreatePipeline(settings.HttpResilience);
            });

        services.AddSingleton<IHttpClientFactory, HttpClientFactory>();

        return services;
    }
}