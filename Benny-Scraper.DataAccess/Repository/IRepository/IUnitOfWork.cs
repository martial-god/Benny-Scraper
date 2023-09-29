

namespace Benny_Scraper.DataAccess.Repository.IRepository
{
    /// <summary>
    /// Holds all our interfaces and makes it so that we can access our methods from the repository while still using the generic repository
    /// </summary>
    public interface IUnitOfWork
    {
        IChapterRepository Chapter { get; }
        INovelRepository Novel { get; }
        INovelListRepository NovelList { get; }
        IPageRepository Page { get; }
        IConfigurationRepository Configuration { get; }
        Task<int> SaveAsync();
    }
}
