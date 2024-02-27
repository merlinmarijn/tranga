﻿using GlaxLogger;
using Tranga.MangaConnectors;
using JobQueue;
using Microsoft.Extensions.Logging;

namespace Tranga;

public partial class Tranga : GlobalBase
{
    public bool keepRunning;
    public JobQueue<MangaConnector> JobQueue;
    private Server _server;
    private HashSet<MangaConnector> _connectors;

    public Tranga(Logger? logger, TrangaSettings settings) : base(logger, settings)
    {
        Log("\n\n _______                                   \n|_     _|.----..---.-..-----..-----..---.-.\n  |   |  |   _||  _  ||     ||  _  ||  _  |\n  |___|  |__|  |___._||__|__||___  ||___._|\n                             |_____|       \n\n");
        Log(settings.ToString());
        keepRunning = true;
        _connectors = new HashSet<MangaConnector>()
        {  
            new Manganato(this),
            new Mangasee(this),
            new MangaDex(this),
            new MangaKatana(this),
            new Mangaworld(this),
            new Bato(this),
            new MangaLife(this)
        };
        this.JobQueue = new JobQueue<MangaConnector>(100, logger);
        this._server = new Server(this);
        string[] emojis = { "(•‿•)", "(づ \u25d5‿\u25d5 )づ", "( \u02d8\u25bd\u02d8)っ\u2668", "=\uff3e\u25cf \u22cf \u25cf\uff3e=", "（ΦωΦ）", "(\u272a\u3268\u272a)", "( ﾉ･o･ )ﾉ", "（〜^\u2207^ )〜", "~(\u2267ω\u2266)~","૮ \u00b4• ﻌ \u00b4• ა", "(\u02c3ᆺ\u02c2)", "(=\ud83d\udf66 \u0f1d \ud83d\udf66=)"};
        SendNotifications("Tranga Started", emojis[Random.Shared.Next(0,emojis.Length-1)]);
    }

    public MangaConnector? GetConnector(string name)
    {
        foreach(MangaConnector mc in _connectors)
            if (mc.name.Equals(name, StringComparison.InvariantCultureIgnoreCase))
                return mc;
        return null;
    }

    public bool TryGetConnector(string name, out MangaConnector? connector)
    {
        connector = GetConnector(name);
        return connector is not null;
    }

    public IEnumerable<MangaConnector> GetConnectors()
    {
        return _connectors;
    }

    public Manga? GetPublicationById(string internalId)
    {
        if (cachedPublications.Exists(publication => publication.internalId == internalId))
            return cachedPublications.First(publication => publication.internalId == internalId);
        return null;
    }

    public bool TryGetPublicationById(string internalId, out Manga? manga)
    {
        manga = GetPublicationById(internalId);
        return manga is not null;
    }
}