# Benny-Scraper: Web Novel and Manga Downloader

Benny-Scraper is an open-source command-line web novel and manga downloader. It saves web novels as EPUB files and manga as PDF or comic book archives such as CBZ. It runs on Windows, Linux, and macOS.

I originally made this because I wanted an easier way to listen to web novels. It grew into a way to keep novels and manga offline and update them without downloading every old chapter again. The database, downloaded chapters, and generated files stay on your computer.

Site configurations are stored as individual JSON files in the [`sites`](Benny-Scraper/sites) directory. Compatible sites can be added without creating another scraper strategy.

[![Build](https://github.com/martial-god/Benny-Scraper/actions/workflows/build.yml/badge.svg)](https://github.com/martial-god/Benny-Scraper/actions/workflows/build.yml)
[![Latest release](https://img.shields.io/github/v/release/martial-god/Benny-Scraper)](https://github.com/martial-god/Benny-Scraper/releases/latest)
![Platform](https://img.shields.io/badge/platform-windows%20%7C%20linux%20%7C%20macos-blue)
[![License](https://img.shields.io/github/license/martial-god/Benny-Scraper)](LICENSE)

## What Makes It Different

- Saved novels can be updated without downloading every old chapter again.
- Successfully downloaded chapters remain saved when another chapter fails and can be retried later.
- Text novels can be saved as EPUB, while manga can be saved as PDF, CBZ, CBR, CB7, CBT, or CBA.
- The interactive testing commands help create and validate configurations for new sites.
- Selenium and FlareSolverr are available for sites that cannot be loaded with a normal HTTP request.

Benny-Scraper does not use a hosted scraping service. It runs on your computer and connects directly to the sites you choose.

## Supported Sites

| Site | Content | Output | Additional requirement |
| --- | --- | --- | --- |
| [Inovelhub](https://inovelhub.com) | Web novel | EPUB | None |
| [mangakakalot](https://mangakakalot.to) | Manga/comic | PDF or comic book archive | Chrome/Selenium |
| [mangakatana](https://mangakatana.com) | Manga/comic | PDF or comic book archive | Chrome/Selenium |
| [mangareader](https://mangareader.to) | Manga/comic | PDF or comic book archive | Chrome/Selenium |
| [NovelBin](https://novelbin.me) | Web novel | EPUB | Chrome/Selenium |
| [noveldrama](https://noveldrama.com) | Web novel | EPUB | None |
| [novelfire](https://novelfire.net) | Web novel | EPUB | None |
| [novelfull](https://novelfull.com) | Web novel | EPUB | FlareSolverr |
| [NovLove](https://novlove.com) | Web novel | EPUB | Chrome/Selenium |
| [Royalroad](https://royalroad.com) | Web novel | EPUB | None |
| [Toonily](https://toonily.com) | Manga/comic | PDF or comic book archive | FlareSolverr |
| [wanderinginn](https://wanderinginn.com) | Web novel | EPUB | None |
| [Wuxiaworld](https://wuxiaworld.com) | Web novel | EPUB | Chrome/Selenium |

Run `benny-scraper --sites` to see the active sites included with your installed version. Sites can change after a release, so this list does not guarantee that every external site is currently working.

### Known Site Limitations

MangaKatana currently gives me the most reliable manga results. Some other manga sites can return scrambled chapter images, so a completed download does not always mean the images are in the right order. External sites also change without notice. If a previously working site stops loading, please open a [broken site report](https://github.com/martial-god/Benny-Scraper/issues/new?template=broken-site.yml).

## Requirements

- A Benny-Scraper release for your operating system, or the .NET 10 SDK when building from source.
- Google Chrome for sites and tests that require Selenium.
- [FlareSolverr](https://github.com/FlareSolverr/FlareSolverr) is optional, but is required for some Cloudflare-protected sites. It runs as a separate application and is commonly installed using Docker.

FlareSolverr can be enabled or disabled in `appsettings.json`:

```json
"FlareSolverrSettings": {
  "Enabled": true,
  "Url": "http://localhost:8191",
  "MaxTimeout": 60000
}
```

### Running FlareSolverr with Docker

Benny-Scraper continues to run normally from your terminal and uses your locally installed Chrome. Docker is only used to run FlareSolverr.

1. Install and start [Docker Desktop](https://www.docker.com/products/docker-desktop/) on Windows or macOS. On Linux, install Docker Engine and the Docker Compose plugin.
2. Open a terminal in the extracted Benny-Scraper directory containing `compose.yaml`.
3. Start FlareSolverr:

```bash
docker compose up -d flaresolverr
```

4. Verify that the container is running:

```bash
docker compose ps
```

5. Use Benny-Scraper normally:

```bash
benny-scraper "https://example.com/novel"
```

FlareSolverr will restart automatically with Docker unless you stop it. These commands can be used to view its logs or remove the container:

```bash
docker compose logs -f flaresolverr
docker compose down
```

The Compose file exposes FlareSolverr only on `127.0.0.1:8191`. Do not expose its service to the internet.

## Installation

1. Download the appropriate archive from the [Releases](https://github.com/martial-god/Benny-Scraper/releases/latest) page.
2. Extract the archive to a permanent location.
3. Run `Benny-Scraper.exe` on Windows or `Benny-Scraper` on Linux and macOS.
4. Optionally add that directory to your PATH so you can run `benny-scraper` from any terminal.

To build and run directly from source:

```bash
dotnet restore Benny-Scraper.sln
dotnet run --project Benny-Scraper/Benny-Scraper.csproj -- --help
```

## Getting Started
1. Add the executable to your PATH, then run `benny-scraper --sites` to view the supported sites.
2. Open a novel's **Table of Contents** page in your browser.
3. Copy the URL from the browser address bar. ![chrome_044SXb9GQL](https://github.com/martial-god/PageShaver/assets/8980094/579ffd1b-f5fb-4a1a-9d30-b83a9c743ca2)

 ![chrome_fWN6VSKOKQ](https://github.com/martial-god/PageShaver/assets/8980094/7f97cd67-772c-4f60-a3d9-856337c3a987) 

4. Pass the copied URL to Benny-Scraper and wait for the output file to be generated. Speed depends on the response time of the site. ![cmd_R4W67LuIR7](https://github.com/martial-god/PageShaver/assets/8980094/d682f498-54f3-40b1-ba6b-4998bd14b863)

Test Wuxiaworld without logging in:

`benny-scraper "https://www.wuxiaworld.com/novel/nine-star-hegemon" -B 1 -E 120` ![WindowsTerminal_FQjbrmWZ4P](https://github.com/user-attachments/assets/d149373a-975d-46a6-aa29-c558eaf084b1)

## Errors

Warnings usually indicate a recoverable problem, such as a failed chapter or an unavailable selector. Benny-Scraper continues when possible and records failed chapters so they can be retried. Fatal errors stop the current operation. Check the log file for complete details.

## Application Data

- Downloads are saved to `Documents/BennyScrapedNovels` unless a different location is configured.
- The database is stored under the operating system's application data directory in `BennyScraper/Database`.
- Logs are stored under the operating system's application data directory in `BennyScraper/logs` and are retained for up to 14 days.

Back up the database before installing a major update or clearing the database.

## Creating a Release

Pushing a version tag runs the GitHub release workflow. It builds and tests the application, publishes self-contained archives for Windows x64, Linux x64, macOS Intel, and macOS Apple Silicon, and then creates the GitHub release with generated release notes and SHA-256 checksums.

Update the version in `Benny-Scraper.csproj`, commit the release changes, and then create and push the matching tag:

```bash
git tag v2.0.1
git push origin v2.0.1
```

A tag containing a suffix, such as `v2.1.0-prerelease`, creates a prerelease. The workflow passes the tag version into the published application, so the tag and `benny-scraper --version` stay consistent. Do not reuse an existing tag; increase the version before creating another release.

### Publishing Standalone Builds Manually

Run these commands from the repository root. The output paths can be changed.

```bash
dotnet publish Benny-Scraper/Benny-Scraper.csproj -c Release --self-contained true -r linux-x64 -o publish/linux-x64
dotnet publish Benny-Scraper/Benny-Scraper.csproj -c Release --self-contained true -r osx-x64 -o publish/osx-x64
dotnet publish Benny-Scraper/Benny-Scraper.csproj -c Release --self-contained true -r osx-arm64 -o publish/osx-arm64
dotnet publish Benny-Scraper/Benny-Scraper.csproj -c Release --self-contained true -r win-x64 -o publish/win-x64
```

On Linux or macOS, make the executable runnable with `chmod +x Benny-Scraper` and add its directory to your PATH if you want to run it from anywhere.

## USAGE AND OPTIONS
* Make sure executable has been added to the environment variables
```bash
benny-scraper [OPTIONS] [URL]
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
Adding a new site requires little to no coding experience, but some knowledge of XPath is helpful. The [`sites`](Benny-Scraper/sites) directory stores each site configuration, and `--test-interactive` guides you through creating one.

1. Run `benny-scraper --test-interactive "https://example.com/novel/example"` using a table-of-contents URL.
2. Enter and validate each XPath when prompted.
3. Review the generated JSON file in the `sites` directory.
4. Run `benny-scraper --validate-config "SiteName"` and provide a live test URL.
5. Test the configuration by downloading a novel.
6. If the site works with `CommonStrategy`, only the new JSON file needs to be included in a pull request.

This process will not work for every site. Sites with custom navigation or authentication may require a dedicated strategy. For Cloudflare-protected sites, try running FlareSolverr locally before testing the site. When FlareSolverr is the only successful way to load the table of contents or chapter content, the generated configuration sets `requiresFlareSolverr` to `true`.

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

  -i, --novel-info-by-id [GUID]    Gets detailed saved information about a novel, including its save location.

  --clear-database             Clear all novels and chapters from database.

  -d, --delete-novel-by-id [GUID]  Deletes a novel by its ID.

  -r, --recreate-epub-by-id [GUID] Recreates an EPUB novel using its ID.

  --retry-failed [GUID]        Retries missing or failed chapters for one saved novel. Combine with --with-login when
                               retrying premium chapters that require an authenticated browser session.

  --retry-all-failed           Retries missing or failed chapters for every saved novel. Can be combined with --with-login.

Configuration:
  -c, --concurrent-request     Set the number [INT] of concurrent requests to a website. Default is 2, value will be limited
                               to number of CPU cores on your computer. *Some websites may block your ip if too many requests
                               are made in a short time*

  --get-concurrent             Display the saved concurrent request limit.

  -s, --save-location          Set default save location [PATH]. Overridden by specific 'manga' or 'novel' locations if set.

  -m, --manga-save-location    Set manga-specific save location [PATH]. Overrides 'save-location'.

  -n, --novel-save-location    Set novel-specific save location [PATH]. Overrides 'save-location'.

  -e, --manga-extension        (Default: -1) Default extension for mangas (any image based novel) [INT] *count starts a 0*.
                               0=PDF, 1=CBZ, 2=CBR, 3=CB7, 4=CBT, 5=CBA. Default is PDF.

  -f, --single-file            Choose how to save Mangas: as a single file containing all chapters (Y), or as individual
                               files for each chapter (N).

  -L, --update-novel-saved-location-by-id    Updates the saved location of a novel by its [ID]. Useful when a file has been
                                             moved, or never added due to previous bug.

  -x, --novel-extension-by-id [GUID]         Change the file type of a saved novel.
                                             0=EPUB, 1=PDF, 2=CBZ, 3=CBR, 4=CB7, 5=CBT, 6=CBA.

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

  --fields                     List all field names supported by --test-field, grouped by table-of-contents and chapter fields.

  --use-selenium               Use Selenium with --test-field for content that is rendered by JavaScript.

  --show-browser               Show the browser while using Selenium with --test-field. Without this option, Selenium runs
                               in headless mode.

  --validate-config [NAME]     Validate an existing site configuration by name. Tests all selectors against a live URL.

  --validate-all-configs       Validate all active site configurations. Tests selectors for each configured site.

General:
  --help                       Display this help screen.

  --version                    Display version information.
```

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

# Retry failed chapters for one novel
Benny-Scraper --retry-failed [NOVEL-ID]

# Retry premium chapters with a visible login session
Benny-Scraper --retry-failed [NOVEL-ID] --with-login

# Retry failed chapters for every saved novel
Benny-Scraper --retry-all-failed

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

# Display the current manga extension
Benny-Scraper --get-extension

# Set concurrent request limit (be careful - some sites rate limit)
Benny-Scraper -c 5

# Display the current concurrent request limit
Benny-Scraper --get-concurrent

# Save manga as one file containing all selected chapters
Benny-Scraper -f y

# Update save location for existing novel
Benny-Scraper -L [NOVEL-ID]

# Change the file type of a saved novel
Benny-Scraper -x [NOVEL-ID]
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

# List the supported field names
Benny-Scraper --fields

# Test JavaScript-rendered chapter content in a visible browser
Benny-Scraper --test-field "ChapterContent://div[@id='chapter-content']" "https://example.com/chapter-1" --use-selenium --show-browser

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

```bash
benny-scraper --help
```

## Contributing

Try adding a new site and see if it works. If it uses `CommonStrategy`, the pull request may only need the new JSON file. See [CONTRIBUTING.md](CONTRIBUTING.md) for the checks to run and the information to include.

Only download content you are allowed to access. You are responsible for following the rules of the sites you use.
