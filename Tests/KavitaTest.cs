using System.Net;
using System.Text;
using API.Schema.LibraryContext.LibraryConnectors;
using Newtonsoft.Json.Linq;

namespace Tests;

public class KavitaTest
{
    [Fact]
    public async Task Test_UsesAuthKeyHeader()
    {
        RecordingHandler handler = new(_ => JsonResponse("{}"));
        Kavita kavita = new("https://kavita.example", "tranga-auth-key", handler);

        Assert.True(await kavita.Test(CancellationToken.None));

        HttpRequestMessage request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://kavita.example/api/Account", request.RequestUri?.AbsoluteUri);
        Assert.Equal("tranga-auth-key", Assert.Single(request.Headers.GetValues("x-api-key")));
        Assert.Null(request.Headers.Authorization);
    }

    [Fact]
    public async Task UpdateLibrary_SendsLibraryIdsAsJsonArrayWithAuthKeyHeader()
    {
        RecordingHandler handler = new(request => request.RequestUri?.AbsolutePath switch
        {
            "/api/Library/libraries" => JsonResponse("[{\"id\":12},{\"id\":34}]"),
            "/api/Library/scan-multiple" => JsonResponse("{}"),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });
        Kavita kavita = new("https://kavita.example", "tranga-auth-key", handler);

        await kavita.UpdateLibrary(CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, request =>
            Assert.Equal("tranga-auth-key", Assert.Single(request.Headers.GetValues("x-api-key"))));

        HttpRequestMessage scanRequest = handler.Requests[1];
        Assert.Equal(HttpMethod.Post, scanRequest.Method);
        JObject body = JObject.Parse(await scanRequest.Content!.ReadAsStringAsync());
        Assert.Equal([12, 34], body["ids"]!.Values<int>());
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            HttpRequestMessage copy = new(request.Method, request.RequestUri);
            foreach ((string key, IEnumerable<string> values) in request.Headers)
                copy.Headers.TryAddWithoutValidation(key, values);
            if (request.Content is not null)
            {
                string content = await request.Content.ReadAsStringAsync(cancellationToken);
                copy.Content = new StringContent(content, Encoding.UTF8,
                    request.Content.Headers.ContentType?.MediaType ?? "application/json");
            }

            Requests.Add(copy);
            return responseFactory(request);
        }
    }
}
