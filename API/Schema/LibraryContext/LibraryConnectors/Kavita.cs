using Newtonsoft.Json.Linq;

namespace API.Schema.LibraryContext.LibraryConnectors;

public class Kavita : LibraryConnector
{
    private readonly HttpClient _httpClient;

    public Kavita(string baseUrl, string auth) : this(baseUrl, auth, new HttpClientHandler())
    {
    }

    internal Kavita(string baseUrl, string auth, HttpMessageHandler handler)
        : base(LibraryType.Kavita, baseUrl, auth)
    {
        _httpClient = new HttpClient(handler);
        _httpClient.DefaultRequestHeaders.Add("x-api-key", auth);
    }

    public override async Task UpdateLibrary(CancellationToken ct)
    {
        Log.Debug("Updating Libraries...");
        List<int> ids = await GetLibraries(ct);

        if (await _httpClient.PostAsJsonAsync(BuildUri("/api/Library/scan-multiple"), new { ids }, ct)
            is { IsSuccessStatusCode: false } response)
        {
            Log.ErrorFormat("Unable to update Kavita libraries: {0} {2} {1}", response.StatusCode,
                await response.Content.ReadAsStringAsync(ct), response.RequestMessage?.RequestUri);
        }
    }

    /// <summary>
    /// Fetches all libraries available to the user
    /// </summary>
    /// <returns>Array of KavitaLibrary</returns>
    private async Task<List<int>> GetLibraries(CancellationToken ct)
    {
        Log.Debug("Getting Libraries...");
        HttpResponseMessage response = await _httpClient.GetAsync(BuildUri("/api/Library/libraries"), ct);
        if (!response.IsSuccessStatusCode)
        {
            Log.ErrorFormat("Unable to fetch Kavita libraries: {0} {2} {1}", response.StatusCode,
                await response.Content.ReadAsStringAsync(ct), response.RequestMessage?.RequestUri);
            return [];
        }

        string responseData = await response.Content.ReadAsStringAsync(ct);
        JArray librariesJson = JArray.Parse(responseData);
        return librariesJson.Children<JObject>()
            .Select(library => library.Value<int>("id"))
            .ToList();
    }

    internal override async Task<bool> Test(CancellationToken ct)
    {
        Log.Debug("Testing...");
        HttpResponseMessage response = await _httpClient.GetAsync(BuildUri("/api/Account"), ct);
        if (!response.IsSuccessStatusCode)
        {
            Log.ErrorFormat("Unable to fetch Kavita account: {0} {2} {1}", response.StatusCode,
                await response.Content.ReadAsStringAsync(ct), response.RequestMessage?.RequestUri);
            return false;
        }

        return true;
    }
}
