namespace Benny_Scraper.BusinessLogic.Interfaces
{
    public interface INovelProcessor
    {
        public Task ProcessNovelAsync(Uri novelTableOfContentsUri);
    }
}
