using Benny_Scraper.DataAccess.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Benny_Scraper.DataAccess.DbInitializer
{
    public class DbInitializer : IDbInitializer
    {
        private readonly ApplicationDbContext _db;

        public DbInitializer(ApplicationDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Initializes the database. Will allow us to not need to call update-databse in the package manager console.
        /// If there are migrations, apply them
        /// </summary>
        /// <exception cref="Exception"></exception>
        public void Initialize()
        {
            // apply Migrations if they are not applied
            try
            {
                if (_db.Database.GetPendingMigrations().Any())
                {
                    _db.Database.Migrate();
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
    }
}
