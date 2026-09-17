# Contributing to Benny-Scraper

Bug reports, fixes, and new site configurations are welcome. Please explain what you tested so I can reproduce the result without guessing.

## Before Opening an Issue

- Check for an existing issue for the same site or error.
- Try the latest release when possible.
- Include the command you ran, your operating system, and the relevant error or log lines.
- Remove passwords, cookies, tokens, and any other private information from logs.

Use the broken-site template when a supported site stops working. Use the site-request template when you want support for a site that is not currently included. Open a blank issue for anything that does not fit either template.

## Adding a Site Configuration

1. Run `benny-scraper --test-interactive "https://example.com/novel/example"` using the site's table-of-contents page.
2. Test and validate the XPath values when prompted.
3. Review the generated JSON file. The interactive test cannot know whether every result is correct.
4. Run `benny-scraper --validate-config "SiteName"` and provide a live test URL.
5. Download at least a small chapter range and check the generated file.

If the site works with `CommonStrategy`, only the new JSON file should normally be needed. Sites with different navigation, authentication, or chapter handling may still need a dedicated strategy.

## Building and Testing

The repository uses the .NET SDK version in `global.json`.

```powershell
dotnet restore Benny-Scraper.sln
dotnet build Benny-Scraper.sln --configuration Release --no-restore
dotnet test Benny-Scraper.Tests/Benny-Scraper.Tests.csproj --configuration Release --no-build --no-restore
```

Warnings are treated as errors. Please keep new code consistent with `.editorconfig` and the existing naming style.

## Pull Requests

Keep the pull request focused where possible. Include:

- What changed and why.
- The URLs or site behavior you tested.
- Whether the site needs Chrome/Selenium or FlareSolverr.
- Any part you could not test.

Do not commit downloaded novels, browser profiles, credentials, databases, logs, or IDE settings.
