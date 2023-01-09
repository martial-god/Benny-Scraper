﻿
using Benny_Scraper.DataAccess.Data;
using Benny_Scraper.DataAccess.Repository.IRepository;
using Benny_Scraper.Models;

namespace Benny_Scraper.DataAccess.Repository
{
    internal class NovelRepository : Repository<Novel>, INovelRepository
    {
        private ApplicationDbContext _db;

        /// <summary>
        /// Values will be passed in by the UnitOfWork class
        /// </summary>
        /// <param name="db"></param>
        public NovelRepository(ApplicationDbContext db) : base(db)
        {
            _db = db;
        }

        public void Update(Novel novel)
        {
            _db.Novels.Update(novel);
        }
    }
}