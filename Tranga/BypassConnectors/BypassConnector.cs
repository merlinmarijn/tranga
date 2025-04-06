using System.Text.Json.Nodes;
using Newtonsoft.Json;

namespace Tranga.BypassConnectors;

public class BypassConnector : GlobalBase
{
    public string bypassSolution { get; }
    public string url { get; private set; }
    public int timeout { get; private set; }

    [JsonConstructor]
    public BypassConnector(GlobalBase clone, string bypassSolution, string url, int timeout = 60) : base(clone)
    {
        this.bypassSolution = bypassSolution;
        this.url = url;
        this.timeout = timeout;
    }

    public void UpdateUrl(string newUrl)
    {
        this.url = newUrl;
        SaveBypassConnector();
    }

    public void UpdateTimeout(int newTimeout)
    {
        this.timeout = newTimeout;
        SaveBypassConnector();
    }

    private void SaveBypassConnector()
    {
        while(IsFileInUse(TrangaSettings.bypassConnectorsFilePath))
            Thread.Sleep(100);
        Log("Exporting bypassConnectors");
        File.WriteAllText(TrangaSettings.bypassConnectorsFilePath, JsonConvert.SerializeObject(this, Formatting.Indented));
    }

    public override string ToString()
    {
        return $"{bypassSolution} {url}";
    }
} 