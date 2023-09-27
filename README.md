# Benny-Scraper
Webscraper that sets out make listening to webnovels easier for myself. Turned into project that let users store all chapters of their favorite Mangas or Webnovels offline in one file. Creates Epubs, at this moment, the goal is to make adding other sites extremely easy using the `appsettings.json` in Benny-Scraper project

MangaKatana is currently the best site to get mangas as the others scramble the chapter images, I can only assume they are owned by the same people and will need to find a way to unscramble it.
## IN PROGRESS - ON DEV BRANCH
- [ ] Add Cbz filetype as an option for Mangas
- [ ] Add a Configuration table to have user have more control of settings.
## COMPLETED - or Things to Do
- [x] Figure out how to properly construct an Epub. https://validator.w3.org/#validate-by-upload for chapter validations
- [x] Code rewrite so process from Scraper to Epub works
- [x] Update code to accommodate more novel sites
- [x] Switch from SQL to MySql to embedd database
- [x] Test on computers without sql installed
- [x] Test on Linux machine - in this Case Ubuntu 20.04-x64
- [x] Add Calibre integration - completed novels will be added to the Calibredb if it is installed on host computer
- [x] Verify the update novel works - INFO can be found https://github.com/martial-god/Benny-Scraper/pull/24#issue-1885102090
- [ ] Finish up Selenium Scraper -- UPDATE: use of seleniumn was necessary when trying to retrieve images from manga sites, it is still faster to use http for NovelData (things such as tags and author)
- [x] Try Manga sites
- [ ] Add UI
- [ ] Create Documentation, especially for trying to add a new Scraper Strategy for new sites


## Getting Started
https://www.webnovelpub.com/
https://www.novelfull.com/
1. For both sites, the url for the `Table of Contents` page for the novel is needed. 
2. *Note* : all epubs will be stored in your Documents folder BennyScrapedNovels/{Novel Name} . Get an Epub Reader to read the contents, chrome extensions are available like `EPUB Reader`
3. Click a novel and copy the url at the top ![chrome_Y234bE9Ce6](https://github.com/martial-god/PageShaver/assets/8980094/31b6190b-439a-4550-aaf3-3b05b3c24a13)![chrome_044SXb9GQL](https://github.com/martial-god/PageShaver/assets/8980094/579ffd1b-f5fb-4a1a-9d30-b83a9c743ca2)

 ![chrome_fWN6VSKOKQ](https://github.com/martial-god/PageShaver/assets/8980094/7f97cd67-772c-4f60-a3d9-856337c3a987)


4. Paste copied url into application, then wait until message about epub has been generated. Speed depends on server response of the site. ![cmd_R4W67LuIR7](https://github.com/martial-god/PageShaver/assets/8980094/d682f498-54f3-40b1-ba6b-4998bd14b863)

5. SPEED HAS ALSO BEEN LIMITED TO 2 CUNCURRENT REQUEST. To change this change the variable `ConcurrentRequestLimit` Benny_Scraper.BusinessLogic.Scrapers.Strategy.ScarperStrategy, the rebuild. The limit was due to certain sites bot protection measures.


## Errors
So long as the error isn't highlighted while the application is running, they are just Warnings or Errors. Nothing Fatal

## Publishing for linux and Windows for standalone Builds
`dotnet publish -c Release --self-contained true -r ubuntu.20.04-x64 -o C:\Users\Mime\Downloads\BennyScraperLinux`         // the path can be whichever you want

`dotnet publish -c Release --self-contained true -r win-x64 -o C:\Users\Mime\Downloads\BennyScraper`

## USAGE AND OPTIONS
* Make sure executable has been added to the environment variables
```bash
dotnet Benny-Scraper.dll [COMMAND] [OPTIONS] [--] [URL...]
```
```bash
Commands:
  list                           List all novels in the database
  clear_database                 Clear all novels and chapters from the database
  delete_novel_by_id [ID]        Delete a novel by its ID
  recreate [URL]                 Recreate a novel EPUB by its URL, currently not implemented to handle Mangas

Options:
  -h, --help                     Show this help text and exit

Usage examples:
  List all novels:
    dotnet Benny-Scraper.dll list
    Benny-Scraper list

  Clear database:
    dotnet Benny-Scraper.dll clear_database
    Benny-Scraper clear_database

  Delete a novel by ID:
    dotnet Benny-Scraper.dll delete_novel_by_id [ID]
    dotnet Benny-Scraper delete_novel_by_id

  Recreate a novel EPUB by URL:
    dotnet Benny-Scraper.dll recreate [URL]
    Benny-Scraper recreate [URL]

For more information about each command and option, run:
  dotnet Benny-Scraper.dll [COMMAND] --help
```

## :sparkles: Contribute to This Project :sparkles:
Hello fellow developer! :wave:

I'm delighted you're taking an interest in this project. Your skills, insights, and perspective could be invaluable in enhancing what's been built so far. Whether it's new features, bug fixes, or general improvements, every contribution is appreciated. Here's how you can pitch in:

Fork & Clone: Begin by forking this repository and cloning it to your machine. This gives you a personal space to work and experiment.

Setup & Run: Make sure to follow the setup instructions in the README for running the project on your local machine.

Find or Report Issues: Have a look at the 'Issues' tab to see if there's something you'd like to work on. If you have new ideas or spot a bug that isn't listed, feel free to open a new issue.

Code: Create a branch on your fork for the specific issue or feature you're addressing. Commit your changes there.

Stay Synced: Regularly sync your fork with this main repository to avoid potential merge conflicts later.

Pull Request: When you're ready, submit a pull request from your branch to the main branch here. Provide a clear description of your changes and any relevant issue numbers.

I value every contribution and am always eager to see how this project can be improved and expanded. Let's collaborate, discuss, and build something great together!

Happy coding! :computer: :heart:
