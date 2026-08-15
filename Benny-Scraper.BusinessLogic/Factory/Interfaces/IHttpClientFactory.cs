using System.Net;

namespace BennyScraper.BusinessLogic.Factory.Interfaces;

internal interface IHttpClientFactory
{
    HttpClient CreateClient();

    void AddCookie(Uri uri, Cookie cookie);

    void AddCookiesFromHeader(Uri uri, string cookieHeader);
}