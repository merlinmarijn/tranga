using System.Net;
using API.MangaDownloadClients;

namespace Tests;

public class RateLimitHandlerTest
{
    [Fact]
    public async Task RequestsToDifferentServers_DoNotShareAQueue()
    {
        using RateLimitHandler handler = new(new SuccessfulHandler(), 1, 1, TimeSpan.FromHours(1));
        using HttpClient client = new(handler);

        using HttpResponseMessage first = await client.GetAsync("https://first.example/one");
        using CancellationTokenSource cancellation = new();
        Task<HttpResponseMessage> queued = client.GetAsync("https://first.example/two", cancellation.Token);

        using HttpResponseMessage independent = await client.GetAsync("https://second.example/one")
            .WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(HttpStatusCode.OK, independent.StatusCode);
        Assert.False(queued.IsCompleted);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
    }

    private sealed class SuccessfulHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}
