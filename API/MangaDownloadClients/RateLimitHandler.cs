using System.Net;
using System.Threading.RateLimiting;
using log4net;

namespace API.MangaDownloadClients;

public class RateLimitHandler : DelegatingHandler
{
    private ILog Log { get; init; } = LogManager.GetLogger(typeof(RateLimitHandler));

    private readonly PartitionedRateLimiter<HttpRequestMessage> _limiter;

    public RateLimitHandler() : this(new HttpClientHandler(),
        Tranga.Settings.UserAgent.Equals(TrangaSettings.DefaultUserAgent) ? int.Min(Constants.RequestsPerMinute, 90) : Constants.RequestsPerMinute,
        Math.Max(1, Constants.RequestsPerMinute / 60), TimeSpan.FromSeconds(1))
    {
    }

    internal RateLimitHandler(HttpMessageHandler innerHandler, int tokenLimit, int tokensPerPeriod, TimeSpan replenishmentPeriod)
        : base(innerHandler)
    {
        _limiter = CreateLimiter(tokenLimit, tokensPerPeriod, replenishmentPeriod);
    }

    private static PartitionedRateLimiter<HttpRequestMessage> CreateLimiter(int tokenLimit, int tokensPerPeriod,
        TimeSpan replenishmentPeriod) => PartitionedRateLimiter.Create<HttpRequestMessage, string>(request =>
        RateLimitPartition.GetTokenBucketLimiter(request.RequestUri?.Authority ?? string.Empty, _ => new()
        {
            AutoReplenishment = true,
            QueueLimit = 100,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            ReplenishmentPeriod = replenishmentPeriod,
            TokenLimit = tokenLimit,
            TokensPerPeriod = tokensPerPeriod
        }));
    
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Log.DebugFormat("Requesting lease {0}", request.RequestUri);
        using RateLimitLease lease = await _limiter.AcquireAsync(request, permitCount: 1, cancellationToken);
        Log.DebugFormat("Acquired lease {0}", request.RequestUri);

        return lease.IsAcquired
            ? await base.SendAsync(request, cancellationToken)
            : new (HttpStatusCode.TooManyRequests);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _limiter.Dispose();
        base.Dispose(disposing);
    }
}
