﻿using System.Globalization;
using System.Text.RegularExpressions;
using Logging;
using Newtonsoft.Json;
using Tranga.BypassConnectors;
using Tranga.LibraryConnectors;
using Tranga.NotificationConnectors;

namespace Tranga;

public abstract class GlobalBase
{
    [JsonIgnore]
    public Logger? logger { get; init; }
    protected HashSet<NotificationConnector> notificationConnectors { get; init; }
    protected HashSet<LibraryConnector> libraryConnectors { get; init; }
    protected HashSet<BypassConnector> bypassConnectors { get; init; }
    private Dictionary<string, Manga> cachedPublications { get; init; }
    public static readonly NumberFormatInfo numberFormatDecimalPoint = new (){ NumberDecimalSeparator = "." };
    protected static readonly Regex baseUrlRex = new(@"https?:\/\/[0-9A-z\.-]+(:[0-9]+)?");

    protected GlobalBase(GlobalBase? clone)
    {
        if (clone is null)
        {
            this.logger = null;
            this.notificationConnectors = new HashSet<NotificationConnector>();
            this.libraryConnectors = new HashSet<LibraryConnector>();
            this.bypassConnectors = new HashSet<BypassConnector>();
            this.cachedPublications = new Dictionary<string, Manga>();
        }
        else
        {
            this.logger = clone.logger;
            this.notificationConnectors = clone.notificationConnectors;
            this.libraryConnectors = clone.libraryConnectors;
            this.bypassConnectors = clone.bypassConnectors;
            this.cachedPublications = clone.cachedPublications;
        }
    }

    protected GlobalBase(Logger? logger)
    {
        this.logger = logger;
        this.notificationConnectors = new HashSet<NotificationConnector>();
        this.libraryConnectors = new HashSet<LibraryConnector>();
        this.bypassConnectors = new HashSet<BypassConnector>();
        this.cachedPublications = new Dictionary<string, Manga>();
    }

    protected void AddMangaToCache(Manga manga)
    {
        if (!this.cachedPublications.TryAdd(manga.internalId, manga))
        {
            Log($"Overwriting Manga {manga.internalId}");
            this.cachedPublications[manga.internalId] = manga;
        }
    }

    protected Manga? GetCachedManga(string internalId)
    {
        return cachedPublications.TryGetValue(internalId, out Manga manga) switch
        {
            true => manga,
            _ => null
        };
    }

    protected IEnumerable<Manga> GetAllCachedManga()
    {
        return cachedPublications.Values;
    }

    protected void Log(string message)
    {
        logger?.WriteLine(this.GetType().Name, message);
    }

    protected void Log(string fStr, params object?[] replace)
    {
        Log(string.Format(fStr, replace));
    }

    protected void SendNotifications(string title, string text, bool buffer = false)
    {
        foreach (NotificationConnector nc in notificationConnectors)
            nc.SendNotification(title, text, buffer);
    }

    protected void AddNotificationConnector(NotificationConnector notificationConnector)
    {
        Log($"Adding {notificationConnector}");
        notificationConnectors.RemoveWhere(nc => nc.notificationConnectorType == notificationConnector.notificationConnectorType);
        notificationConnectors.Add(notificationConnector);
        
        while(IsFileInUse(TrangaSettings.notificationConnectorsFilePath))
            Thread.Sleep(100);
        Log("Exporting notificationConnectors");
        File.WriteAllText(TrangaSettings.notificationConnectorsFilePath, JsonConvert.SerializeObject(notificationConnectors));
    }

    protected void DeleteNotificationConnector(NotificationConnector.NotificationConnectorType notificationConnectorType)
    {
        Log($"Removing {notificationConnectorType}");
        notificationConnectors.RemoveWhere(nc => nc.notificationConnectorType == notificationConnectorType);
        while(IsFileInUse(TrangaSettings.notificationConnectorsFilePath))
            Thread.Sleep(100);
        Log("Exporting notificationConnectors");
        File.WriteAllText(TrangaSettings.notificationConnectorsFilePath, JsonConvert.SerializeObject(notificationConnectors));
    }

    protected void UpdateLibraries()
    {
        foreach(LibraryConnector lc in libraryConnectors)
            lc.UpdateLibrary();
    }

    protected void AddLibraryConnector(LibraryConnector libraryConnector)
    {
        Log($"Adding {libraryConnector}");
        libraryConnectors.RemoveWhere(lc => lc.libraryType == libraryConnector.libraryType);
        libraryConnectors.Add(libraryConnector);
        
        while(IsFileInUse(TrangaSettings.libraryConnectorsFilePath))
            Thread.Sleep(100);
        Log("Exporting libraryConnectors");
        File.WriteAllText(TrangaSettings.libraryConnectorsFilePath, JsonConvert.SerializeObject(libraryConnectors, Formatting.Indented));
    }

    protected void DeleteLibraryConnector(LibraryConnector.LibraryType libraryType)
    {
        Log($"Removing {libraryType}");
        libraryConnectors.RemoveWhere(lc => lc.libraryType == libraryType);
        while(IsFileInUse(TrangaSettings.libraryConnectorsFilePath))
            Thread.Sleep(100);
        Log("Exporting libraryConnectors");
        File.WriteAllText(TrangaSettings.libraryConnectorsFilePath, JsonConvert.SerializeObject(libraryConnectors, Formatting.Indented));
    }

    protected void AddBypassConnector(BypassConnector bypassConnector)
    {
        Log($"Adding {bypassConnector}");
        bypassConnectors.RemoveWhere(bc => bc.bypassSolution == bypassConnector.bypassSolution);
        bypassConnectors.Add(bypassConnector);
        
        while(IsFileInUse(TrangaSettings.bypassConnectorsFilePath))
            Thread.Sleep(100);
        Log("Exporting bypassConnectors");
        File.WriteAllText(TrangaSettings.bypassConnectorsFilePath, JsonConvert.SerializeObject(bypassConnectors, Formatting.Indented));
    }

    protected void DeleteBypassConnector(string bypassSolution)
    {
        Log($"Removing {bypassSolution}");
        bypassConnectors.RemoveWhere(bc => bc.bypassSolution == bypassSolution);
        while(IsFileInUse(TrangaSettings.bypassConnectorsFilePath))
            Thread.Sleep(100);
        Log("Exporting bypassConnectors");
        File.WriteAllText(TrangaSettings.bypassConnectorsFilePath, JsonConvert.SerializeObject(bypassConnectors, Formatting.Indented));
    }

    protected bool IsFileInUse(string filePath) => IsFileInUse(filePath, this.logger);

    public static bool IsFileInUse(string filePath, Logger? logger)
    {
        if (!File.Exists(filePath))
            return false;
        try
        {
            using FileStream stream = new (filePath, FileMode.Open, FileAccess.Read, FileShare.None);
            stream.Close();
            return false;
        }
        catch (IOException)
        {
            logger?.WriteLine($"File is in use {filePath}");
            return true;
        }
    }
}