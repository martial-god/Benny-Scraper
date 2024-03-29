# Benny-Scraper
Webscraper that sets out make listening to webnovels easier for myself. Turned into project that let users store all chapters of their favorite Mangas or Webnovels offline in one file. Creates Epubs of text based novels, and PDF and most forms of comic book archives like Cbz, at this moment, the goal is to make adding other sites extremely easy using the `appsettings.json` in Benny-Scraper project.

![Platform](https://img.shields.io/badge/platform-windows%20%7C%20linux%20%7C%20macos-blue)

MangaKatana is currently the best site to get mangas as the others scramble the chapter images, I can only assume they are owned by the same people and will need to find a way to unscramble it.
## IN PROGRESS - ON DEV BRANCH
- [ ] Addition of webnovle.com from https://github.com/martial-god/Benny-Scraper/issues/41 - Expected Release by 04/14/2024
- [ ] Create Documentation, especially for trying to add a new Scraper Strategy for new sites - *COMING SOON* https://feahnthor.github.io/
## COMPLETED - or Things to Do
- [x] Add Cbz filetype as an option for Mangas
- [x] Figure out how to properly construct an Epub. https://validator.w3.org/#validate-by-upload for chapter validations
- [x] Code rewrite so process from Scraper to Epub works
- [x] Update code to accommodate more novel sites
- [x] Switch from SQL to MySql to embedd database
- [x] Test on computers without sql installed
- [x] Test on Linux machine and Mac - in this Case Ubuntu 20.04-x64, Mac Sonoma 14.1
- [x] Add Calibre integration - completed novels will be added to the Calibredb if it is installed on host computer
- [x] Verify the update novel works - INFO can be found https://github.com/martial-god/Benny-Scraper/pull/24#issue-1885102090
- [x] Try Manga sites
- [x] Add a Configuration table to have user have more control of settings. *STILL NEED TO ADD COMMANDLINE OPTIONS TO RETRIEVE VALUES*
- [ ] Finish up Selenium Scraper -- UPDATE: use of seleniumn was necessary when trying to retrieve images from manga sites, it is still faster to use http for NovelData (things such as tags and author)
- [ ] Add UI

## Getting Started
https://lightnovelworld.com
https://www.novelfull.com/
https://mangakatana.com/
1. For both sites, the url for the `Table of Contents` page for the novel is needed. 
2. *Note* : all Epubs will be stored in your Documents folder BennyScrapedNovels/{Novel Name}, *unless changed through command line options*. Get an Epub Reader to read the contents, chrome extensions are available like `EPUB Reader`
3. Click a novel and copy the url at the top ![chrome_Y234bE9Ce6](https://github.com/martial-god/PageShaver/assets/8980094/31b6190b-439a-4550-aaf3-3b05b3c24a13)![chrome_044SXb9GQL](https://github.com/martial-god/PageShaver/assets/8980094/579ffd1b-f5fb-4a1a-9d30-b83a9c743ca2)

 ![chrome_fWN6VSKOKQ](https://github.com/martial-god/PageShaver/assets/8980094/7f97cd67-772c-4f60-a3d9-856337c3a987)


4. Paste copied url into application, then wait until message about epub has been generated. Speed depends on server response of the site. ![cmd_R4W67LuIR7](https://github.com/martial-god/PageShaver/assets/8980094/d682f498-54f3-40b1-ba6b-4998bd14b863)

## Errors
So long as the error isn't highlighted while the application is running, they are just Warnings or Errors. Nothing Fatal

## Publishing for Linux, Mac, and Windows for standalone Builds
`dotnet publish -c Release --self-contained true -r ubuntu.20.04-x64 -o C:\Users\Mime\Downloads\BennyScraperLinux`         // the path can be whichever you want

`dotnet publish -c Release --self-contained true -r osx-x64 -o /Users/myuser/Desktop/BennyScraperMac`   // add to Environment using bash or zsh

`dotnet publish -c Release --self-contained true -r win-x64 -o C:\Users\Mime\Downloads\BennyScraper`

## USAGE AND OPTIONS
* Make sure executable has been added to the environment variables
```bash
dotnet Benny-Scraper.dll [COMMAND] [OPTIONS] [--] [VALUES]
```
```bash
Commands:
    -l, --list                 List all novels in database. Options include
                                   -P, --page [INT]
                                   -I, --items-per-page [INT]
                                   -S, --search [STRING]

  -U, --update-all             Updates all non-completed novels in database with ones found online. Will only update ones that were not modified the same day.

  -i, --novel-info-by-id       Gets the detailed saved information about a novel, including save location

  --clear-database             Clear all novels and chapters from database.

  -d, --delete-novel-by-id     Deletes a novel by its ID

  -r, --recreate-epub-by-id    Recreates Epub novel using the [ID].

  -c, --concurrent-request     Set the number [INT] of concurrent requests to a website. Default is 2, value will be limited to number
                               of CPU cores on your computer. *Some websites may block your ip if too many requests are made in a short
                               time*

  -s, --save-location          Set default save location [PATH]. Overridden by specific 'manga' or 'novel' locations if set.

  -m, --manga-save-location    Set manga-specific save location [PATH]. Overrides 'save-location'.

  -n, --novel-save-location    Set novel-specific save location [PATH]. Overrides 'save-location'.

  -e, --manga-extension        (Default: -1) Default extension for mangas (any image based novel) [INT] *count starts a 0*. Default is
                               PDF.

  -f, --single-file            Choose how to save Mangas: as a single file containing all chapters (Y), or as individual
                               files for each chapter (N).

  -L, --update-novel-saved-location-by-id    Updates the saved location of a novel by its [ID]. Useful when a file has been moved, or never added due to previous bug.

  --get-extension              Gets the saved default extensions for mangas.

  --help                       Display this help screen.

  --version                    Display version information.

Usage examples:
  List all novels, default 10 to a page:
    dotnet Benny-Scraper.dll --list
    Benny-Scraper -l

  List all novels searching by name, changing total results per page: [OPTIONS] -P, --page [INT] | -I, --items-per-page [INT] | -S, --search [STRING]
    dotnet Benny-Scraper.dll --list -I [INT] -S [STRING]    ex: 15                ex: One Piece
    Benny-Scraper -l -I 10 -S Martial -P 1   -- this will search for all novels where the title the contains the word 'Martial', showing only 10 results per page, and start the search on page 1.

  Get more info about a novel, including how things were saved. IT IS RECOMMENDED YOU RUN THIS AFTER USING benny-Scraper VERSION 1.0.0, as bugs caused files to not be stored correctly.
    dotnet Benny-Scraper.dll --novel-info-by-id [ID]      ex: 00000000-0000-0000-0000-000000000000
    Benny-Scraper -i [ID]

  Clear database:
    dotnet Benny-Scraper.dll --clear-database
    Benny-Scraper --clear-database

  Delete a novel by ID:
    dotnet Benny-Scraper.dll --delete-novel-by-id [ID]    ex: 00000000-0000-0000-0000-000000000000
    dotnet Benny-Scraper -d [ID]

  Recreate a novel EPUB by ID:
    dotnet Benny-Scraper.dll --recreate-epub-by-id [ID]
    Benny-Scraper -r [ID]

  Set the Default location where both webnovels and Mangas will be saved.
    dotnet Benny-Scraper.dll --save-location [PATH]    ex: C:\Users\test\Downloads   must be a Directory/Folder not a File
    Benny-Scraper -s [PATH]

  Sets the default file extension for Comicbook Archive, i.e. .cbz, .cbr, .cbt
    dotnet Benny-Scraper.dll --manga-extension [INT]    ex: 1
    Benny-Scraper -e [INT]

  Update location of a novel by its id, you can get ID from the --list or -l command:
    dotnet Benny-Scraper.dll --update-novel-saved-location-by-id [ID]    ex: 00000000-0000-0000-0000-000000000000         You will be prompted to enter the full path for the FOLDER your file(s) are stored
    Benny-Scraper -L [ID]

For more information about each command and option, run:
  dotnet Benny-Scraper.dll [COMMAND] --help
```

## ✨ Contribute to This Project ✨
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
