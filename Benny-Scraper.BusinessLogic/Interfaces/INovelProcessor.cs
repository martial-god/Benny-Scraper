namespace Benny_Scraper.BusinessLogic.Interfaces
{
    public interface INovelProcessor
    {
        public Task ProcessNovelAsync(Uri novelTableOfContentsUri, int? beginChapter = null, int? endChapter = null);
    }
}
