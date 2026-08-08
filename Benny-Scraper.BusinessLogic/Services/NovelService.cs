using BennyScraper.BusinessLogic.Helper;
using BennyScraper.BusinessLogic.Services.Interfaces;
using BennyScraper.DataAccess.Repository.IRepository;
using BennyScraper.Models;

namespace BennyScraper.BusinessLogic.Services;

public class NovelService(IUnitOfWork unitOfWork) : INovelService
{
    // CreateScraper new novel with a passed in novel
    public async Task<Guid> CreateAsync(Novel novel)
    {
        ArgumentNullException.ThrowIfNull(novel);

        novel.DateLastModified = DateTime.Now;
        novel.TotalChapters = novel.Chapters.Count;
        await unitOfWork.Novel.AddAsync(novel).ConfigureAwait(false);

        // await _unitOfWork.Chapter.AddAsync(novel.Chapters.FirstOrDefault());
        await unitOfWork.SaveAsync().ConfigureAwait(false);
        return novel.Id;
    }

    /// <summary>
    /// Updates an existing novel and persists a collection of newly scraped chapters.
    /// </summary>
    /// <param name="novel">The novel entity to update.</param>
    /// <param name="chapters">The newly scraped chapters associated with the novel.</param>
    /// <returns>A task that represents the asynchronous update operation.</returns>
    public async Task UpdateAndAddChaptersAsync(Novel novel, IEnumerable<Chapter> chapters)
    {
        unitOfWork.Novel.Update(novel); // update existing

        await unitOfWork.SaveAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Updates an existing novel's stored data.
    /// </summary>
    /// <param name="novel">The novel entity to update.</param>
    /// <returns>A task that represents the asynchronous update operation.</returns>
    public async Task UpdateAsync(Novel novel)
    {
        ArgumentNullException.ThrowIfNull(novel);

        novel.DateLastModified = DateTime.Now;
        unitOfWork.Novel.Update(novel);
        await unitOfWork.SaveAsync().ConfigureAwait(false);
    }

    public async Task<IEnumerable<Novel>> GetAllAsync()
    {
        return await unitOfWork.Novel.GetAllAsync().ConfigureAwait(false);
    }

    public async Task<Novel?> GetByUrlAsync(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        var context = await unitOfWork.Novel.GetFirstOrDefaultAsync(filter: c => c.Url == uri.OriginalString, includeProperties: "ChapterRanges").ConfigureAwait(false);
        if (context == null)
        {
            return context;
        }

        var chapterContext = await unitOfWork.Chapter.GetAllAsync(filter: c => c.NovelId == context.Id).ConfigureAwait(false);
        context.Chapters.ReplaceWith(chapterContext);
        return context;
    }

    public async Task<Novel?> GetByIdAsync(Guid id)
    {
        var context = await unitOfWork.Novel.GetFirstOrDefaultAsync(filter: c => c.Id == id).ConfigureAwait(false);
        if (context == null)
        {
            return context;
        }

        var chapterContext = await unitOfWork.Chapter.GetAllAsync(filter: c => c.NovelId == context.Id, includeProperties: "Pages").ConfigureAwait(false);
        context.Chapters.ReplaceWith(chapterContext);
        return context;
    }

    /// <summary>
    /// Checks whether a novel with the given table of contents url already exists in the database.
    /// </summary>
    /// <param name="tableOfContentsUrl">url of the table of contents page of the novel.</param>
    /// <returns>True if a matching novel exists in the database; otherwise, false.</returns>
    public async Task<bool> IsNovelInDatabaseAsync(string tableOfContentsUrl)
    {
        var context = await unitOfWork.Novel.GetFirstOrDefaultAsync(filter: c => c.Url == tableOfContentsUrl).ConfigureAwait(false);
        return context != null;
    }

    public async Task<bool> IsNovelInDatabaseAsync(Guid id)
    {
        var context = await unitOfWork.Novel.GetFirstOrDefaultAsync(filter: c => c.Id == id).ConfigureAwait(false);
        return context != null;
    }

    public async Task RemoveAllAsync()
    {
        var allNovels = await unitOfWork.Novel.GetAllAsync().ConfigureAwait(false);
        var allChapters = await unitOfWork.Chapter.GetAllAsync().ConfigureAwait(false);

        unitOfWork.Novel.RemoveRange(allNovels);
        unitOfWork.Chapter.RemoveRange(allChapters);

        await unitOfWork.SaveAsync().ConfigureAwait(false);
    }

    public async Task RemoveByIdAsync(Guid id)
    {
        var novel = await unitOfWork.Novel.GetByIdAsync(id).ConfigureAwait(false);
        if (novel == null)
        {
            throw new InvalidOperationException("Novel not found.");
        }

        var chapters = await unitOfWork.Chapter.GetAllAsync(filter: c => c.NovelId == id).ConfigureAwait(false);
        unitOfWork.Chapter.RemoveRange(chapters);
        unitOfWork.Novel.Remove(novel);
        await unitOfWork.SaveAsync().ConfigureAwait(false);
    }
}