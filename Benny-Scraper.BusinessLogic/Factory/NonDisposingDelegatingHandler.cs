namespace BennyScraper.BusinessLogic.Factory;

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