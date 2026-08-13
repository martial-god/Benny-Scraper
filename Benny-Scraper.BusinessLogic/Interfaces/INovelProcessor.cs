namespace BennyScraper.BusinessLogic.Interfaces;

internal interface INovelProcessor
{
    public Task ProcessNovelAsync(Uri novelTableOfContentsUri, int? beginChapter = null, int? endChapter = null, bool withLogin = false);

    public Task<RetryResult> RetryFailedChaptersAsync(Guid novelId, bool withLogin = false);
}

internal sealed record RetryResult(int TotalFailed, int Succeeded, int StillFailed);