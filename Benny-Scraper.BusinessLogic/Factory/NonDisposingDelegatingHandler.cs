namespace BennyScraper.BusinessLogic.Factory;

internal sealed class NonDisposingDelegatingHandler(HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler)
{
    protected override void Dispose(bool disposing) => base.Dispose(false);
}