# Benny-Scraper
WebScraper that sets out make listening to webnovels easier for myself. Turned into project that let users store all chapters of their favorite Mangas or Webnovels offline in one file or multiple files. Creates Epubs of text-based novels, and PDF and most forms of comic book archives like Cbz. at this moment, the goal is to make adding other sites extremely easy using the `appsettings.json` in Benny-Scraper project.

![Platform](https://img.shields.io/badge/platform-windows%20%7C%20linux%20%7C%20macos-blue)

MangaKatana is currently the best site to get mangas as the others scramble the chapter images, I can only assume they are owned by the same people and will need to find a way to unscramble it.

## Getting Started
1. Choose a site from the supported sites list by running `benny-scraper --sites`. (Make sure if on windows this is in the environment variables path. If on linux or macos, you can add it to your path using bash or zsh)
2. The url for the **Table of Contents** page for the novel is needed. 
3. Click a novel and copy the url at the top ![chrome_044SXb9GQL](https://github.com/martial-god/PageShaver/assets/8980094/579ffd1b-f5fb-4a1a-9d30-b83a9c743ca2)

 ![chrome_fWN6VSKOKQ](https://github.com/martial-god/PageShaver/assets/8980094/7f97cd67-772c-4f60-a3d9-856337c3a987) 

4. Paste copied url into application, then wait until message about epub has been generated. Speed depends on the server response of the site. ![cmd_R4W67LuIR7](https://github.com/martial-god/PageShaver/assets/8980094/d682f498-54f3-40b1-ba6b-4998bd14b863)
### 
Test with Wuxiaworld not logging in
`benny-scraper "https://www.wuxiaworld.com/novel/nine-star-hegemon" -B 1 -E 120` ![WindowsTerminal_FQjbrmWZ4P](https://github.com/user-attachments/assets/d149373a-975d-46a6-aa29-c558eaf084b1)

## Errors
So long as the error isn't highlighted while the application is running, they are just Warnings or Errors. Nothing Fatal

## Publishing for Linux, Mac, and Windows for standalone Builds
`dotnet publish -c Release --self-contained true -r linux-x64 -o C:\Users\Mime\Downloads\BennyScraperLinux`         // the path can be whichever you want

`dotnet publish -c Release --self-contained true -r osx-x64 -o /Users/myuser/Desktop/BennyScraperMac`   // add to Environment using bash or zsh

`dotnet publish -c Release --self-contained true -r win-x64 -o C:\Users\Mime\Downloads\BennyScraper`

## USAGE AND OPTIONS
* Make sure executable has been added to the environment variables
```bash
benny-scraper [COMMAND] [OPTIONS] [--] [VALUES]
```

### Quick Start - Download a Novel (yt-dlp style)
```bash
# View all supported websites
benny-scraper --sites

# Download entire novel (interactive mode)
benny-scraper "https://www.wuxiaworld.com/novel/nine-star-hegemon"

# Download chapters 1-50 (non-interactive, no prompts)
benny-scraper "https://www.wuxiaworld.com/novel/nine-star-hegemon" -B 1 -E 50

# Download with login for premium chapters (shows browser). Your credentials are NEVER stored.
benny-scraper "https://www.wuxiaworld.com/novel/nine-star-hegemon" --with-login -B 1 -E 100

# Download from chapter 25 to the end
benny-scraper "https://www.novelfull.com/my-novel.html" -B 25
```

### Adding a New Site
Adding a new site should now be easier and can be done by someone with little to no coding experience, just some knowledge about `xpath`. The [sites](Benny-Scraper/sites) stores all the site configurations, running `benny-scraper --test-interactive` will guide you through the process.
- Notes this doesn't work for all sites, for those create an issue, and I will add it.... eventually. For others protected by Cloudflare, try installing [FlareSolverr](https://github.com/FlareSolverr/FlareSolverr) as a proxy locally.
1. Run `benny-scraper --test-interactive `
2.

### Command Reference
```bash
Download Options:
  [URL]                        Novel table of contents URL to download. When provided as the first argument,
                               downloads the novel immediately (can be combined with -B, -E, --with-login).

  -B, --begin-chapter [INT]    Starting chapter number for range selection. If not specified, starts from chapter 1.
                               Combine with -E to download a specific range non-interactively.

  -E, --end-chapter [INT]      Ending chapter number for range selection. If not specified, downloads to the last chapter.
                               Combine with -B to download a specific range non-interactively.

  --with-login                 Show browser for manual login to access premium chapters (e.g., WuxiaWorld). Your credentials
                               are NEVER stored - you login manually in the browser window, then scraping continues. Useful
                               for accessing premium/locked chapters you own. Without this flag, browser runs headless (hidden).

General:
  --sites                      Display all supported websites for scraping with clickable URLs and ASCII art header.

Database Management:
  -l, --list                   List all novels in database. Options include:
                                   -P, --page [INT]
                                   -I, --items-per-page [INT]
                                   -S, --search [STRING]

  -U, --update-all             Updates all non-completed novels in database with ones found online. Will only update ones
                               that were not modified the same day.

  -i, --novel-info-by-id       Gets the detailed saved information about a novel, including save location

  --clear-database             Clear all novels and chapters from database.

  -d, --delete-novel-by-id     Deletes a novel by its ID

  -r, --recreate-epub-by-id    Recreates Epub novel using the [ID].

Configuration:
  -c, --concurrent-request     Set the number [INT] of concurrent requests to a website. Default is 2, value will be limited
                               to number of CPU cores on your computer. *Some websites may block your ip if too many requests
                               are made in a short time*

  -s, --save-location          Set default save location [PATH]. Overridden by specific 'manga' or 'novel' locations if set.

  -m, --manga-save-location    Set manga-specific save location [PATH]. Overrides 'save-location'.

  -n, --novel-save-location    Set novel-specific save location [PATH]. Overrides 'save-location'.

  -e, --manga-extension        (Default: -1) Default extension for mangas (any image based novel) [INT] *count starts a 0*.
                               Default is PDF.

  -f, --single-file            Choose how to save Mangas: as a single file containing all chapters (Y), or as individual
                               files for each chapter (N).

  -L, --update-novel-saved-location-by-id    Updates the saved location of a novel by its [ID]. Useful when a file has been
                                             moved, or never added due to previous bug.

  --get-extension              Gets the saved default extensions for mangas.

Testing & Validation:
  -t, --test-site              Test connectivity to a site [URL]. Attempts to fetch the page and extract the title to verify
                               Cloudflare bypass is working. Useful for testing a site before implementing a scraper strategy.

  --test-all                   Test connectivity to all supported sites. Displays which sites are accessible and which are
                               blocked by Cloudflare or other protection. Runs automatically on application startup in
                               interactive mode.

  --test-interactive [URL]     Interactive mode for testing a new site. Guides you through testing each field and generates
                               a JSON configuration that will be treated as a new site.

  --test-field [FIELD:XPATH]   Test a specific field with XPath. Example: --test-field "Title://h1[@class='title']" <URL>

  --validate-config [NAME]     Validate an existing site configuration by name. Tests all selectors against a live URL.

  --validate-all-configs       Validate all active site configurations. Tests selectors for each configured site.

General:
  --help                       Display this help screen.

  --version                    Display version information.

### Usage Examples

#### 📖 Downloading Novels (Non-Interactive)
```bash
# Download entire novel - interactive chapter selection
Benny-Scraper "https://www.wuxiaworld.com/novel/nine-star-hegemon"

# Download specific chapter range (no prompts - like yt-dlp)
Benny-Scraper "https://www.wuxiaworld.com/novel/nine-star-hegemon" -B 1 -E 50

# Download from chapter 100 to the end
Benny-Scraper "https://www.novelfull.com/martial-god-asura.html" -B 100

# Download first 25 chapters
Benny-Scraper "https://mangakatana.com/manga/one-piece.123" -E 25

# Download with login for premium chapters (WuxiaWorld)
Benny-Scraper "https://www.wuxiaworld.com/novel/nine-star-hegemon" --with-login -B 1 -E 100

# Complex example: Login + specific range + works completely non-interactively
Benny-Scraper "https://www.wuxiaworld.com/novel/coiling-dragon" --with-login -B 50 -E 150
```

#### 🔐 Premium Chapter Access
```bash
# Download with login (shows browser for manual login)
Benny-Scraper "https://www.wuxiaworld.com/novel/nine-star-hegemon" --with-login

# Download premium chapters in specific range
Benny-Scraper "https://www.wuxiaworld.com/novel/nine-star-hegemon" --with-login -B 200 -E 250

# Without --with-login flag, browser runs headless (hidden) and premium chapters are skipped
Benny-Scraper "https://www.wuxiaworld.com/novel/nine-star-hegemon" -B 1 -E 50
```
**Note:** When using `--with-login`:
- Browser window opens visibly for you to login manually
- Your credentials are NEVER stored
- After login, press Enter to continue scraping
- Premium chapters you own will be included in the download

#### 📚 Database Management
```bash
# List all novels (10 per page)
Benny-Scraper --list
Benny-Scraper -l

# Search for novels with pagination
Benny-Scraper -l -I 10 -S "Martial" -P 1
# Searches for novels containing "Martial", 10 results per page, starting on page 1

# Get detailed info about a specific novel
Benny-Scraper -i [NOVEL-ID]

# Update all incomplete novels in database
Benny-Scraper -U

# Delete a novel by ID
Benny-Scraper -d [NOVEL-ID]

# Recreate EPUB from database
Benny-Scraper -r [NOVEL-ID]

# Clear entire database
Benny-Scraper --clear-database
```

#### ⚙️ Configuration
```bash
# Set default save location for all novels
Benny-Scraper -s "C:\Users\YourName\Documents\Novels"

# Set separate locations for novels and manga
Benny-Scraper -n "C:\Users\YourName\Documents\WebNovels"
Benny-Scraper -m "C:\Users\YourName\Documents\Manga"

# Set default manga extension (0=PDF, 1=CBZ, 2=CBR, etc.)
Benny-Scraper -e 1

# Set concurrent request limit (be careful - some sites rate limit)
Benny-Scraper -c 5

# Update save location for existing novel
Benny-Scraper -L [NOVEL-ID]
```

#### 🧪 Testing & Validation
```bash
# Test if you can reach a site before implementing a scraper
Benny-Scraper -t "https://wanderinginn.com"

# Test all supported sites
Benny-Scraper --test-all

# Interactive site configuration testing
Benny-Scraper --test-interactive "https://newnovelsite.com/novel/example"

# Test a specific XPath selector
Benny-Scraper --test-field "Title://h1[@class='novel-title']" "https://example.com/novel"

# Validate existing site configuration against a live URL
Benny-Scraper --validate-config "Wuxiaworld"

# Validate all site configurations
Benny-Scraper --validate-all-configs
```

#### 🎮 Interactive Mode
```bash
# Run without arguments to enter interactive mode
Benny-Scraper

# Interactive mode commands:
#   test <url>        Test a specific site URL
#   test-all          Test all supported sites
#   exit              Quit the application
```

#### 🔄 Real-World Workflows
```bash
# Daily routine: Download new chapters from your favorite novel
Benny-Scraper "https://www.novelfull.com/martial-god-asura.html" -B 2500

# Binge reading: Download entire volume with premium access
Benny-Scraper "https://www.wuxiaworld.com/novel/coiling-dragon" --with-login -B 1 -E 200

# Archive collection: Download and organize multiple novels
Benny-Scraper -m "D:\Managas" "https://mangakatana.com/manga/the-return-of-the-crazy-demon.25882" # set save location for manga
Benny-Scraper -n "D:\WebNovels" "https://www.novelfull.com/novel2.html" # change were novels without images are saved
Benny-Scraper -l -S "Novel" -I 20 # list all novels containing "Novel" with 20 results per page

# Update all your ongoing novels at once
Benny-Scraper -U
```

For more information about each command and option, run:
  dotnet Benny-Scraper.dll [COMMAND] --help
```

## ✨ Contribute to This Project ✨
Try adding a new site and see if it works, create a pull request with your new site configuration.