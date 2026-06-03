using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HouseOfRuns.Frontend.Models;

namespace HouseOfRuns.Frontend.Services;

public sealed class HouseApiClient(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<T> GetAsync<T>(string path)
    {
        using var response = await CreateClient().GetAsync(path);
        return await ReadAsync<T>(response);
    }

    public async Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest request)
    {
        using var response = await CreateClient().PostAsJsonAsync(path, request, JsonOptions);
        return await ReadAsync<TResponse>(response);
    }

    public async Task<TResponse> PutAsync<TRequest, TResponse>(string path, TRequest request)
    {
        using var response = await CreateClient().PutAsJsonAsync(path, request, JsonOptions);
        return await ReadAsync<TResponse>(response);
    }

    public async Task DeleteAsync(string path)
    {
        using var response = await CreateClient().DeleteAsync(path);
        await EnsureSuccessAsync(response);
    }

    public async Task<ImportRunDraftResponse> ImportRunDraftAsync(IFormFile file, int? runIndex)
    {
        using var content = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream();
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(file.ContentType == string.Empty ? "application/json" : file.ContentType);
        content.Add(fileContent, "file", file.FileName);
        if (runIndex.HasValue)
        {
            content.Add(new StringContent(runIndex.Value.ToString()), "runIndex");
        }

        using var response = await CreateClient().PostAsync("/api/import/run-draft", content);
        return await ReadAsync<ImportRunDraftResponse>(response);
    }

    public async Task<ImportRunsDraftResponse> ImportRunDraftsAsync(IFormFile file)
    {
        using var content = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream();
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(file.ContentType == string.Empty ? "application/json" : file.ContentType);
        content.Add(fileContent, "file", file.FileName);

        using var response = await CreateClient().PostAsync("/api/import/run-drafts", content);
        return await ReadAsync<ImportRunsDraftResponse>(response);
    }

    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient("HouseApi");
        var token = httpContextAccessor.HttpContext?.Session.GetString("ApiToken");
        if (!string.IsNullOrWhiteSpace(token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        await EnsureSuccessAsync(response);
        var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        return value ?? throw new ApiException("The API returned an empty response.", response.StatusCode);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var content = await response.Content.ReadAsStringAsync();
        try
        {
            var problem = JsonSerializer.Deserialize<ProblemResponse>(content, JsonOptions);
            throw new ApiException(problem?.Detail ?? problem?.Title ?? response.ReasonPhrase ?? "API request failed.", response.StatusCode);
        }
        catch (JsonException)
        {
            throw new ApiException(content.Length > 0 ? content : response.ReasonPhrase ?? "API request failed.", response.StatusCode);
        }
    }

    private sealed record ProblemResponse(string? Title, string? Detail, int? Status);
}
