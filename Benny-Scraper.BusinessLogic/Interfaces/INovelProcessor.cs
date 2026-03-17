namespace Benny_Scraper.BusinessLogic.Interfaces
{
    public record RetryResult(int TotalFailed, int Succeeded, int StillFailed);

    public interface INovelProcessor
    {
        public Task ProcessNovelAsync(Uri novelTableOfContentsUri, int? beginChapter = null, int? endChapter = null, bool withLogin = false);
        public Task<RetryResult> RetryFailedChaptersAsync(Guid novelId, bool withLogin = false);
    }
}
