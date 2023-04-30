# Benny-Scraper
Webscraper that sets out make listening to webnovels easier for myself. Creates Epubs, at this moment, the goal is to make adding other sites extremely easy using the `appsettings.json` in Benny-Scraper project

# TO DO
- [x] Figure out how to properly construct an Epub. https://validator.w3.org/check for chapter validations
- [x] Code rewrite so process from Scraper to Epub works
- [ ] Update code to accommodate more novel sites
- [ ] Finish up Selenium Scraper
- [ ] Try Manga sites
- [ ] Add UI
- [ ] make changes to database, use ints instead of unique identifiers as Shane mentioned. Guid should be used from the outside in, where someone wants to get data through an api, for that having the main id be a int and having a column like UUID which is the guid that will be used to find the item.