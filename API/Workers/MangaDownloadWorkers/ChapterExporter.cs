using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using API.MangaConnectors;
using API.Schema.MangaContext;
using HtmlAgilityPack;
using log4net;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Binarization;

namespace API.Workers.MangaDownloadWorkers;

internal static class ChapterExporter
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(ChapterExporter));

    internal static Task<bool> Export(ChapterDownloadPayload payload, MangaConnector connector, Chapter chapter,
        string outputPath, string? referrer, Func<Task> beforeArchive, CancellationToken cancellationToken) => payload switch
    {
        ImagePagesPayload pages => ExportManga(pages, connector, chapter, outputPath, referrer, beforeArchive, cancellationToken),
        ChapterHtmlPayload html => ExportNovel(html, chapter, outputPath, beforeArchive, cancellationToken),
        _ => Task.FromResult(false)
    };

    private static async Task<bool> ExportManga(ImagePagesPayload pages, MangaConnector connector, Chapter chapter,
        string outputPath, string? referrer, Func<Task> beforeArchive, CancellationToken cancellationToken)
    {
        if (pages.Urls.Length < 1)
        {
            Log.Info($"No imageUrls for chapter {chapter}");
            return false;
        }

        Log.Info($"Downloading images: {chapter}");
        List<Stream> images = [];
        foreach (string imageUrl in pages.Urls)
        {
            try
            {
                if (await connector.DownloadImage(imageUrl, cancellationToken, referrer) is not { } stream)
                {
                    Log.Error($"Failed to download image: {imageUrl}");
                    return false;
                }
                images.Add(await ProcessImage(stream, cancellationToken));
            }
            catch (Exception exception)
            {
                Log.Error(exception);
                images.ForEach(image => image.Dispose());
                return false;
            }
        }

        await beforeArchive();
        if (File.Exists(outputPath))
        {
            Log.Info($"Archive {outputPath} already existed, overwriting.");
            File.Delete(outputPath);
        }

        try
        {
            Log.Debug($"Creating archive: {outputPath}");
            using ZipArchive archive = ZipFile.Open(outputPath, ZipArchiveMode.Create);
            if (Constants.CreateComicInfoXml)
            {
                Log.Debug("Writing ComicInfo.xml");
                await using Stream comicStream = archive.CreateEntry("ComicInfo.xml").Open();
                await comicStream.WriteAsync(Encoding.UTF8.GetBytes(chapter.GetComicInfoXmlString()), cancellationToken);
            }
            else
                Log.Debug("Skipping ComicInfo.xml. CREATE_COMICINFO_XML is set to false");

            for (int index = 0; index < images.Count; index++)
            {
                Log.Debug($"Packaging images to archive {chapter} , image {index}");
                await using Stream zipStream = archive.CreateEntry($"{index}.jpg").Open();
                images[index].Position = 0;
                await images[index].CopyToAsync(zipStream, cancellationToken);
            }
        }
        catch (Exception exception)
        {
            Log.Error(exception);
        }
        finally
        {
            images.ForEach(image => image.Dispose());
        }

        return true;
    }

    private static async Task<bool> ExportNovel(ChapterHtmlPayload payload, Chapter chapter, string outputPath,
        Func<Task> beforeArchive, CancellationToken cancellationToken)
    {
        await beforeArchive();
        if (File.Exists(outputPath))
            File.Delete(outputPath);

        string chapterTitle = chapter.Title is { Length: > 0 } title ? title : $"Chapter {chapter.ChapterNumber}";
        string author = string.Join(", ", chapter.ParentManga.Authors.Select(item => item.AuthorName));
        string language = chapter.ParentManga.OriginalLanguage ?? "en";
        HtmlDocument document = new();
        document.LoadHtml(payload.Html);
        XNamespace dc = "http://purl.org/dc/elements/1.1/";
        XNamespace opf = "http://www.idpf.org/2007/opf";
        XNamespace epub = "http://www.idpf.org/2007/ops";
        XNamespace containerNamespace = "urn:oasis:names:tc:opendocument:xmlns:container";
        XDocument package = new(new XElement(opf + "package",
            new XAttribute("version", "3.0"), new XAttribute("unique-identifier", "book-id"), new XAttribute(XNamespace.Xml + "lang", language),
            new XElement(opf + "metadata",
                new XAttribute(XNamespace.Xmlns + "dc", dc),
                new XElement(dc + "identifier", new XAttribute("id", "book-id"), chapter.Key),
                new XElement(dc + "title", chapter.ParentManga.Name),
                new XElement(dc + "creator", author),
                new XElement(dc + "language", language),
                new XElement(dc + "description", chapter.ParentManga.Description)),
            new XElement(opf + "manifest",
                new XElement(opf + "item", new XAttribute("id", "chapter"), new XAttribute("href", "chapter.xhtml"), new XAttribute("media-type", "application/xhtml+xml")),
                new XElement(opf + "item", new XAttribute("id", "nav"), new XAttribute("href", "nav.xhtml"), new XAttribute("media-type", "application/xhtml+xml"), new XAttribute("properties", "nav"))),
            new XElement(opf + "spine", new XElement(opf + "itemref", new XAttribute("idref", "chapter")))));
        XNamespace xhtml = "http://www.w3.org/1999/xhtml";
        XDocument chapterDocument = new(new XElement(xhtml + "html",
            new XElement(xhtml + "head", new XElement(xhtml + "title", chapterTitle),
                new XElement(xhtml + "style", "p { margin: 0 0 1em; } blockquote { margin: 1em; }")),
            new XElement(xhtml + "body", new XElement(xhtml + "h1", chapterTitle),
                new XElement(xhtml + "div", new XAttribute("class", "chapter-content"), ToXhtml(document.DocumentNode, xhtml)))));
        XDocument navigation = new(new XElement(xhtml + "html", new XElement(xhtml + "head", new XElement(xhtml + "title", chapter.ParentManga.Name)),
            new XElement(xhtml + "body", new XElement(xhtml + "nav", new XAttribute(XNamespace.Xml + "id", "toc"), new XAttribute(epub + "type", "toc"),
                new XElement(xhtml + "ol", new XElement(xhtml + "li", new XElement(xhtml + "a", new XAttribute("href", "chapter.xhtml"), chapterTitle)))))));
        XDocument container = new(new XElement(containerNamespace + "container", new XAttribute("version", "1.0"),
            new XElement(containerNamespace + "rootfiles", new XElement(containerNamespace + "rootfile", new XAttribute("full-path", "OEBPS/content.opf"), new XAttribute("media-type", "application/oebps-package+xml")))));

        using ZipArchive archive = ZipFile.Open(outputPath, ZipArchiveMode.Create);
        await Write(archive, "mimetype", "application/epub+zip", CompressionLevel.NoCompression, cancellationToken);
        await Write(archive, "META-INF/container.xml", container.ToString(), CompressionLevel.Optimal, cancellationToken);
        await Write(archive, "OEBPS/content.opf", package.ToString(), CompressionLevel.Optimal, cancellationToken);
        await Write(archive, "OEBPS/chapter.xhtml", chapterDocument.ToString(), CompressionLevel.Optimal, cancellationToken);
        await Write(archive, "OEBPS/nav.xhtml", navigation.ToString(), CompressionLevel.Optimal, cancellationToken);
        return true;
    }

    private static IEnumerable<XNode> ToXhtml(HtmlNode node, XNamespace xhtml)
    {
        if (node.Name is "script" or "style" or "noscript" or "iframe" or "object" or "embed")
            return [];
        if (node.NodeType == HtmlNodeType.Text)
            return string.IsNullOrWhiteSpace(node.InnerText) ? [] : [new XText(HtmlEntity.DeEntitize(node.InnerText))];
        if (node.NodeType != HtmlNodeType.Element)
            return node.ChildNodes.SelectMany(child => ToXhtml(child, xhtml));

        string tag = node.Name.ToLowerInvariant() switch
        {
            "b" => "strong",
            "i" => "em",
            "p" or "br" or "strong" or "em" or "a" or "blockquote" or "ul" or "ol" or "li" or "h2" or "h3" or "h4" => node.Name.ToLowerInvariant(),
            _ => "span"
        };
        XElement element = new(xhtml + tag, node.ChildNodes.SelectMany(child => ToXhtml(child, xhtml)));
        if (tag == "a" && node.GetAttributeValue("href", "") is { Length: > 0 } href)
            element.SetAttributeValue("href", href);
        return [element];
    }

    private static async Task Write(ZipArchive archive, string name, string contents, CompressionLevel compression,
        CancellationToken cancellationToken)
    {
        await using Stream stream = archive.CreateEntry(name, compression).Open();
        await stream.WriteAsync(Encoding.UTF8.GetBytes(contents), cancellationToken);
    }

    private static async Task<Stream> ProcessImage(Stream imageStream, CancellationToken cancellationToken)
    {
        Log.Debug("Processing image");
        imageStream.Position = 0;
        int imageCompression = Math.Clamp(Tranga.Settings.ImageCompression, 1, 100);
        if (!Tranga.Settings.BlackWhiteImages && imageCompression == 100)
        {
            Log.Debug("No processing requested for image");
            return imageStream;
        }

        MemoryStream processedImage = new();
        try
        {
            using Image image = await Image.LoadAsync(imageStream, cancellationToken);
            Log.Debug("Image loaded");
            if (Tranga.Settings.BlackWhiteImages)
                image.Mutate(operation => operation.ApplyProcessor(new AdaptiveThresholdProcessor()));
            await image.SaveAsJpegAsync(processedImage, new JpegEncoder { Quality = imageCompression });
            Log.Debug("Image processed");
            processedImage.Position = 0;
            return processedImage;
        }
        catch (Exception exception)
        {
            if (exception is UnknownImageFormatException or NotSupportedException)
                Log.Debug("Unable to process image: Not supported image format");
            else if (exception is InvalidImageContentException)
                Log.Debug("Unable to process image: Invalid Content");
            else
                Log.Error(exception);
            await imageStream.CopyToAsync(processedImage, cancellationToken);
            processedImage.Position = 0;
            return processedImage;
        }
    }
}
