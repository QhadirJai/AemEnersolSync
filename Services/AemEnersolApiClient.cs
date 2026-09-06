using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AemEnersolSync.Models.Dtos;
using Microsoft.Extensions.Options;

namespace AemEnersolSync.Services;

/// <summary>
/// Typed HttpClient over the AEM Enersol API: logs in for a JWT, then calls the platform
/// endpoints with it. The token is cached for the lifetime of this instance (one request),
/// so a sync run logs in once rather than once per call.
/// </summary>
public class AemEnersolApiClient : IAemEnersolApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        // Tolerates a numeric field arriving quoted, one more payload change that
        // should not take the sync down.
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private readonly HttpClient _httpClient;
    private readonly AemEnersolApiOptions _options;
    private readonly ILogger<AemEnersolApiClient> _logger;

    private string? _token;

    public AemEnersolApiClient(
        HttpClient httpClient,
        IOptions<AemEnersolApiOptions> options,
        ILogger<AemEnersolApiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PlatformDto>> GetPlatformsAsync(
        PlatformWellDataset dataset,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var path = dataset == PlatformWellDataset.Actual ? _options.ActualPath : _options.DummyPath;
        var token = await GetTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        // The API does not accept a bare token: the "Bearer " scheme is required.
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        _logger.LogInformation("Fetching {Dataset} from {Path}", dataset, path);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, path, cancellationToken);

        var platforms = await response.Content.ReadFromJsonAsync<List<PlatformDto>>(JsonOptions, cancellationToken)
                        ?? new List<PlatformDto>();

        _logger.LogInformation(
            "Fetched {PlatformCount} platform(s) and {WellCount} well(s) from {Dataset}",
            platforms.Count, platforms.Sum(p => p.Wells?.Count ?? 0), dataset);

        return platforms;
    }

    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (_token is not null)
        {
            return _token;
        }

        _logger.LogInformation("Logging in as {Username}", _options.Username);

        using var response = await _httpClient.PostAsJsonAsync(
            _options.LoginPath,
            new { username = _options.Username, password = _options.Password },
            cancellationToken);

        await EnsureSuccessAsync(response, _options.LoginPath, cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        _token = ExtractToken(body)
                 ?? throw new InvalidOperationException("The login response contained no token.");

        return _token;
    }

    /// <summary>
    /// The login endpoint returns the JWT as a bare JSON string. An object wrapper is
    /// accepted too, so a future change to the response shape does not break login.
    /// </summary>
    private static string? ExtractToken(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.String)
            {
                return root.GetString();
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var name in new[] { "token", "accessToken", "access_token", "jwt" })
                {
                    if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                    {
                        return value.GetString();
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Not JSON at all — the endpoint may have returned the raw token unquoted.
            return body.Trim();
        }

        return null;
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            throw new InvalidOperationException(
                $"{AemEnersolApiOptions.SectionName}:BaseUrl is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.Username) || string.IsNullOrWhiteSpace(_options.Password))
        {
            throw new InvalidOperationException(
                $"{AemEnersolApiOptions.SectionName}:Username and :Password must be configured.");
        }
    }

    /// <summary>
    /// Fails with the response body included — a bare status code makes an auth or routing
    /// problem far harder to diagnose from the logs.
    /// </summary>
    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string path,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            $"'{path}' returned {(int)response.StatusCode} {response.ReasonPhrase}. {body}".Trim(),
            inner: null,
            statusCode: response.StatusCode);
    }
}
