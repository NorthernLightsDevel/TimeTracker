using System;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace TimeTracker.ApiClient;

internal sealed class TimeTrackerApiHttpClient
{
    public TimeTrackerApiHttpClient(HttpClient httpClient, IOptions<TimeTrackerApiClientOptions> optionsAccessor)
    {
        if (httpClient is null)
        {
            throw new ArgumentNullException(nameof(httpClient));
        }

        if (optionsAccessor is null)
        {
            throw new ArgumentNullException(nameof(optionsAccessor));
        }

        var options = optionsAccessor.Value ?? throw new InvalidOperationException("API client options are not configured.");
        var baseAddress = NormalizeBaseAddress(options.BaseAddress);

        httpClient.BaseAddress = new Uri(baseAddress, UriKind.Absolute);
        httpClient.Timeout = options.Timeout <= TimeSpan.Zero
            ? TimeSpan.FromSeconds(10)
            : options.Timeout;

        if (!httpClient.DefaultRequestHeaders.Accept.Contains(MediaTypeWithQualityHeaderValue.Parse("application/json")))
        {
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        HttpClient = httpClient;
    }

    public HttpClient HttpClient { get; }

    private static string NormalizeBaseAddress(string baseAddress)
    {
        if (string.IsNullOrWhiteSpace(baseAddress))
        {
            baseAddress = TimeTrackerApiClientOptions.DefaultBaseAddress;
        }

        if (!baseAddress.EndsWith("/", StringComparison.Ordinal))
        {
            baseAddress += "/";
        }

        return baseAddress;
    }
}
