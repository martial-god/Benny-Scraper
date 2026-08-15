using Autofac;
using BennyScraper.BusinessLogic.Config;
using BennyScraper.BusinessLogic.Factory;
using BennyScraper.BusinessLogic.Factory.Interfaces;
using Polly;

namespace BennyScraper.BusinessLogic.Extensions;

internal static class DependencyInjectionExtensions
{
    public static ContainerBuilder AddHttpResilience(this ContainerBuilder builder)
    {
        builder.Register(c =>
            {
                var settings = c.Resolve<NovelScraperSettings>();
                return HttpClientResilience.CreatePipeline(settings.HttpResilience);
            })
            .As<ResiliencePipeline<HttpResponseMessage>>()
            .SingleInstance();

        builder.RegisterType<HttpClientFactory>()
            .As<IHttpClientFactory>()
            .SingleInstance();

        return builder;
    }
}