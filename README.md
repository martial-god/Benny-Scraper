# Benny-Scraper
Webscraper that sets out make listening to webnovels easier for myself. Creates Epubs, at this moment, the goal is to make adding other sites extremely easy using the `appsettings.json` in Benny-Scraper project

# TO DO
- [x] Figure out how to properly construct an Epub. https://validator.w3.org/check for chapter validations
- [x] Code rewrite so process from Scraper to Epub works
- [x] Update code to accommodate more novel sites
- [x] Switch from SQL to MySql to embedd database
- [x] Test on computers without sql installed
- [x] Test on Linux machine - in this Case Ubuntu 20.04-x64
- [ ] Verify the update novel works
- [ ] Finish up Selenium Scraper -- UPDATE: use of seleniumn was necessary when trying to retrieve images from manga sites, it is still faster to use http for NovelData (things such as tags and author)
- [x] Try Manga sites
- [ ] Add UI
- [ ] make changes to database, use ints instead of unique identifiers as Shane mentioned. Guid should be used from the outside in, where someone wants to get data through an api, for that having the main id be a int and having a column like UUID which is the guid that will be used to find the item.

## Getting Started
https://www.webnovelpub.com/
https://www.novelfull.com/
1. For both sites, the url for the `Table of Contents` page for the novel is needed. 
2. *Note* : all epubs will be stored in you Documents folder BennyScrapedNovels/{Novel Name} . Get an epub reader to read the contents, chrome extensions are available like `EPUB Reader`
3. Click a novel and copy the url at the top ![msedge_kXe7ITKmKp](https://github.com/feahnthor/Benny-Scraper/assets/8980094/23edc857-1e5c-4a08-9482-ee594bcb9133)
![chrome_s3toEMlclt](https://github.com/feahnthor/Benny-Scraper/assets/8980094/76e1c90f-7638-4585-bbcf-3b6e51334434)
4. Paste copied url into application, then wait until message about epub has been generated. Speed depends on server response of the site.![Benny-Scraper_s1UZSARERa](https://github.com/feahnthor/Benny-Scraper/assets/8980094/6be17188-e9ce-4fd2-89fd-2c575a4b97c6)


## Errors
So long as the error isn't highlighted while the application is running, they are just Warnings or Errors. Nothing Fatal

## Publishing for linux and windows
`dotnet publish -c Release --self-contained true -r ubuntu.20.04-x64 -o C:\Users\Mime\Downloads\BennyScraperLinux`
`dotnet publish -c Release --self-contained true -r win-x64 -o C:\Users\Mime\Downloads\BennyScraper`
