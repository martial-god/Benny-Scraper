namespace BennyScraper.BusinessLogic.Interfaces;

public interface INovelProcessor
{
    public Task ProcessNovelAsync(Uri novelTableOfContentsUri, int? beginChapter = null, int? endChapter = null, bool withLogin = false);

    public Task<RetryResult> RetryFailedChaptersAsync(Guid novelId, bool withLogin = false);
}

public record RetryResult(int TotalFailed, int Succeeded, int StillFailed);