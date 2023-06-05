using Benny_Scraper.DataAccess.Data;
using Microsoft.EntityFrameworkCore;

namespace Benny_Scraper.DataAccess.DbInitializer
{
    public class DbInitializer
    {
        private readonly Database _db;

        public DbInitializer(Database db)
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
