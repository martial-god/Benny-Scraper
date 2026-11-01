# Benny-Scraper v3.0.1

RoyalRoad downloads now find the complete chapter list and capture chapter content that the previous selectors missed. This release includes the fixes from [#59](https://github.com/martial-god/Benny-Scraper/issues/59), [#60](https://github.com/martial-god/Benny-Scraper/pull/60), and [#61](https://github.com/martial-god/Benny-Scraper/pull/61).

## RoyalRoad fixes

- Read chapter links and titles from the page's embedded `window.chapters` data. Downloads are no longer limited to the first 20 rows of the paginated chapter table.
- Updated chapter-content selectors to capture text in nested paragraphs and divs, including author notes.
- Preserve the script's chapter order so the site's `chapterSortOrder` setting remains in control.
- Report missing or invalid chapter data instead of returning an incomplete table of contents.

The manual test shared in PR #60 produced an EPUB containing all 121 chapters of *Vampire and the Dayspring Star*:

![RoyalRoad download completed with 121 chapters and a generated EPUB](https://github.com/user-attachments/assets/8315e595-54f3-4a42-926e-d35f83cd6d20)

## FlareSolverr configuration

RoyalRoad now has `requiresFlareSolverr: true`. Update the included `sites/royalroad.json` along with the application, enable FlareSolverr in `appsettings.json`, and start the service:

```sh
docker compose up -d flaresolverr
```

During normal downloads, each site's `requiresFlareSolverr` setting decides whether to use FlareSolverr. Enabled sites try it first. Disabled sites use their configured HTTP/Selenium path without automatically switching to FlareSolverr. Interactive site testing can still detect whether FlareSolverr is needed.

## Other changes

- Replaced Autofac with Microsoft dependency injection.
- Allow newer stable .NET 10.0 SDKs through `global.json` while retaining 10.0.100 as the minimum.
- Updated System.Security.Cryptography.Xml to 10.0.14.

## Validation and downloads

The Windows Release build and all 90 automated tests passed locally. Parser tests use small synthetic examples and do not depend on a live novel.

The release workflow builds self-contained packages for Windows x64, Linux x64, macOS x64, and macOS ARM64, with SHA-256 checksums. Linux and macOS execution were not tested locally.
