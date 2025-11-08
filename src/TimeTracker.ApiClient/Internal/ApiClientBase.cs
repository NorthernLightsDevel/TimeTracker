using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace TimeTracker.ApiClient.Internal;

internal abstract class ApiClientBase
{
    protected ApiClientBase(TimeTrackerApiHttpClient apiHttpClient)
    {
        HttpClient = apiHttpClient?.HttpClient ?? throw new ArgumentNullException(nameof(apiHttpClient));
    }

    protected HttpClient HttpClient { get; }

    protected static JsonSerializerOptions SerializerOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    protected static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        if (response is null)
        {
            return "API request failed.";
        }

        var content = response.Content is null
            ? string.Empty
            : await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(content))
        {
            return $"API request failed with status {(int)response.StatusCode} ({response.StatusCode}).";
        }

        return content;
    }
}
