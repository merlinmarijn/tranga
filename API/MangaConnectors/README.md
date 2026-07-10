# Adding a manga-site connector

For a conventional HTML site, create a JSON file at `%APPDATA%/tranga-api/Connectors/<site>.json` on Windows or `/usr/share/tranga-api/Connectors/<site>.json` in the production Linux container. It is loaded alongside the existing handwritten connector list, so do not edit `Tranga.cs`, `MangaContext.cs`, or a migration. A local definition with the same connector name overrides a bundled one.

```json
{
  "name": "ExampleManga",
  "baseUrl": "https://example-manga.test/",
  "supportedLanguages": ["en"],
  "baseUris": ["example-manga.test"],
  "iconUrl": "https://example-manga.test/favicon.ico",
  "searchUrl": "https://example-manga.test/search?q={query}",
  "searchResultXPath": "//a[contains(@href, '/series/')]",
  "mangaUrl": "https://example-manga.test/series/{id}",
  "mangaIdRegex": "/series/(?<id>[^/?#]+)",
  "title": { "xPath": "//h1" },
  "description": { "xPath": "//meta[@name='description']", "attribute": "content" },
  "cover": { "xPath": "//meta[@property='og:image']", "attribute": "content" },
  "status": { "xPath": "//span[@class='status']" },
  "authors": "//a[@rel='author']",
  "tags": "//a[contains(@href, '/genre/')]",
  "originalLanguage": "en",
  "chapterLinkXPath": "//a[contains(@href, '/chapter/')]",
  "chapterRegex": "/chapter/(?<id>[^/?#]+).*?(?:chapter|ch\\.?)\\s*(?<number>\\d+(?:\\.\\d+)?)",
  "pageImageXPath": "//img[contains(@class, 'page')]"
}
```

`MangaIdRegex` must name the stable series identifier `id`. `ChapterRegex` must name `number`; it can also name `id`, `volume`, and `title`. Regexes are evaluated against the chapter URL followed by its visible text, which keeps most definitions to a single pattern. `pageImageRegex` is optional and is useful when reader URLs are JSON embedded in the HTML rather than `<img>` elements; it must name every URL with `url`. Set `nextData` to `true` for a Next.js site that exposes its search, manga, chapter, and reader data in `__NEXT_DATA__`.

Use XPath because HtmlAgilityPack already ships with Tranga. In your browser inspector, right-click a stable element, copy XPath, then simplify it to avoid generated classes. The search selector and chapter selector should select links (`<a href="...">`). A value selector reads text by default, or an attribute when its JSON object has an `attribute` field.

Before opening a PR, check a search, series page, chapter list, and reader page against the site. If it uses a JSON API, a JavaScript challenge, pagination, language-specific feeds, or custom image decoding, inherit from `MangaConnector` directly and copy the closest existing connector instead.
