# Benny-Scraper
Webscraper that sets out make listening to webnovels easier for myself. Creates Epubs, at this moment, the goal is to make adding other sites extremely easy using the `appsettings.json` in Benny-Scraper project

# TO DO
- [x] Figure out how to properly construct an Epub. https://validator.w3.org/check for chapter validations
- [x] Code rewrite so process from Scraper to Epub works
- [x] Update code to accommodate more novel sites
- [ ] Switch from SQL to MySql to embedd database
- [ ] Finish up Selenium Scraper
- [ ] Try Manga sites
- [ ] Add UI
- [ ] make changes to database, use ints instead of unique identifiers as Shane mentioned. Guid should be used from the outside in, where someone wants to get data through an api, for that having the main id be a int and having a column like UUID which is the guid that will be used to find the item.

## Getting Started
1. Make sure you have an SQL server installed on your machine
2. At this moment, the master branch only works for novels on https://novelfull.com/. To try the other site I am currently working on, switch to the dev branch. The site will be https://www.webnovelpub.com/
3. For both sites, the url for the `Table of Contents` page is needed to work, that is the page that has all the chapters listed.
4. *Note* : all epubs will be stored in you Documents folder BennyScrapedNovels/{Novel Name} . Get an epub reader to read the contents, chrome extensions are available like `EPUB Reader`
5. For novelfull.com just click a novel and copy the link ![msedge_kXe7ITKmKp](https://github.com/feahnthor/Benny-Scraper/assets/8980094/23edc857-1e5c-4a08-9482-ee594bcb9133)
6. For the Dev branch and https://www.webnovelpub.com/ to get the table of contents url... video didn't save, just use this as a test https://www.webnovelpub.com/novel/lord-of-the-mysteries-wn-14051341
7. ![msedge_eLDuuUmL96](https://github.com/feahnthor/Benny-Scraper/assets/8980094/37383a6d-db9d-41fb-86bb-b674f0524b0d)


## Errors
So long as the error isn't highlighted while the application is running, they are just Warnings or Errors. Nothing Fatal
