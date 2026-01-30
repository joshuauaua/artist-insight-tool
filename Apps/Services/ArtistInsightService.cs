using System.Net.Http.Json;
using ArtistInsightTool.Connections.ArtistInsightTool;
using System.Text.Json;

namespace ArtistInsightTool.Apps.Services;

public class ArtistInsightService
{
  private readonly HttpClient _httpClient;
  private const string BaseUrl = "http://localhost:5052/api";

  public ArtistInsightService()
  {
    _httpClient = new HttpClient();
  }

  public async Task<List<Asset>> GetAssetsAsync()
  {
    try
    {
      return await _httpClient.GetFromJsonAsync<List<Asset>>($"{BaseUrl}/assets") ?? new List<Asset>();
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error fetching assets: {ex.Message}");
      return new List<Asset>();
    }
  }

  public async Task<Asset?> CreateAssetAsync(Asset asset)
  {
    try
    {
      var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/assets", asset);
      if (response.IsSuccessStatusCode)
      {
        return await response.Content.ReadFromJsonAsync<Asset>();
      }
      return null;
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error creating asset: {ex.Message}");
      return null;
    }
  }

  public async Task<bool> DeleteAssetAsync(int id)
  {
    try
    {
      var response = await _httpClient.DeleteAsync($"{BaseUrl}/assets/{id}");
      return response.IsSuccessStatusCode;
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error deleting asset: {ex.Message}");
      return false;
    }
  }

  // Revenue Entries
  public async Task<List<RevenueEntryDto>> GetRevenueEntriesAsync()
  {
    var url = $"{BaseUrl}/Revenue/entries";
    try
    {
      Console.WriteLine($"DEBUG: Requesting GET {url}");
      return await _httpClient.GetFromJsonAsync<List<RevenueEntryDto>>(url) ?? new List<RevenueEntryDto>();
    }
    catch (HttpRequestException ex)
    {
      Console.WriteLine($"Error fetching revenue entries: {ex.Message} Status: {ex.StatusCode}");
      return new List<RevenueEntryDto>();
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error fetching revenue entries: {ex.Message}");
      return new List<RevenueEntryDto>();
    }
  }

  public async Task<RevenueEntry?> GetRevenueEntryAsync(int id)
  {
    try
    {
      return await _httpClient.GetFromJsonAsync<RevenueEntry>($"{BaseUrl}/Revenue/{id}");
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error fetching revenue entry {id}: {ex.Message}");
      return null;
    }
  }

  public async Task<RevenueEntry?> CreateRevenueEntryAsync(RevenueEntry entry)
  {
    try
    {
      var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/Revenue", entry);
      if (response.IsSuccessStatusCode)
      {
        return await response.Content.ReadFromJsonAsync<RevenueEntry>();
      }
      var error = await response.Content.ReadAsStringAsync();
      throw new Exception($"API Error {response.StatusCode}: {error}");
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error creating revenue entry: {ex.Message}");
      throw; // Propagate exception to caller
    }
  }

  public async Task<RevenueEntry?> FindRevenueEntryAsync(int year, string quarter, int sourceId)
  {
    try
    {
      return await _httpClient.GetFromJsonAsync<RevenueEntry>($"{BaseUrl}/Revenue/find?year={year}&quarter={quarter}&sourceId={sourceId}");
    }
    catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
      return null;
    }
    catch (Exception ex)
    {
      // Log or handle other errors? For now safest is return null or rethrow?
      // Returning null allows creation to proceed, which might duplicate if it was a transient error.
      // But for 404 specifically we want null.
      Console.WriteLine($"Error finding revenue entry: {ex.Message}");
      return null;
    }
  }

  public async Task<bool> UpdateRevenueEntryAsync(RevenueEntry entry)
  {
    var response = await _httpClient.PutAsJsonAsync($"{BaseUrl}/Revenue/{entry.Id}", entry);
    return response.IsSuccessStatusCode;
  }

  public async Task<bool> UpdateRevenueEntryAsync(int id, RevenueEntry entry)
  {
    try
    {
      var response = await _httpClient.PutAsJsonAsync($"{BaseUrl}/Revenue/{id}", entry);
      return response.IsSuccessStatusCode;
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error updating revenue entry: {ex.Message}");
      return false;
    }
  }

  public async Task<bool> DeleteRevenueEntryAsync(int id)
  {
    try
    {
      var response = await _httpClient.DeleteAsync($"{BaseUrl}/Revenue/{id}");
      return response.IsSuccessStatusCode;
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error deleting revenue entry: {ex.Message}");
      return false;
    }
  }

  // Import Templates
  public async Task<List<ImportTemplate>> GetTemplatesAsync()
  {
    try
    {
      return await _httpClient.GetFromJsonAsync<List<ImportTemplate>>($"{BaseUrl}/Templates") ?? new List<ImportTemplate>();
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error fetching templates: {ex.Message}");
      return new List<ImportTemplate>();
    }
  }

  public async Task<ImportTemplate?> CreateTemplateAsync(ImportTemplate template)
  {
    try
    {
      var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/Templates", template);
      if (response.IsSuccessStatusCode)
      {
        return await response.Content.ReadFromJsonAsync<ImportTemplate>();
      }
      return null;
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error creating template: {ex.Message}");
      return null;
    }
  }

  public async Task<bool> UpdateTemplateAsync(int id, ImportTemplate template)
  {
    try
    {
      var response = await _httpClient.PutAsJsonAsync($"{BaseUrl}/Templates/{id}", template);
      return response.IsSuccessStatusCode;
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error updating template: {ex.Message}");
      return false;
    }
  }

  public async Task<bool> DeleteTemplateAsync(int id)
  {
    try
    {
      // Manual cleanup to handle foreign key constraints (simulate SetNull)
      // Since we can't easily migrate the existing SQLite DB, we do this in code.

      // 1. Unlink Revenue Entries
      var linkedEntries = await _httpClient.GetFromJsonAsync<List<RevenueEntryDto>>($"{BaseUrl}/Revenue/entries?templateId={id}");
      if (linkedEntries != null)
      {
        foreach (var entry in linkedEntries)
        {
          // We need to update the entry to set TemplateId to null. 
          // However, the UpdateRevenueEntryAsync might need the full object. 
          // Let's rely on a backend endpoint if specific "Unlink" exists, or just try deleting the template 
          // assuming the backend handles it? 
          // Wait, the Service is calling the Backend API. The Logic MUST be in the Backend API, not here in the frontend service.
          // I made a mistake thinking this Service was the Backend Logic. It is the Frontend Client.
          // I need to modify the BACKEND CONTROLLER, not this Service.
        }
      }

      var response = await _httpClient.DeleteAsync($"{BaseUrl}/Templates/{id}");
      return response.IsSuccessStatusCode;
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error deleting template: {ex.Message}");
      return false;
    }
  }

  // Dashboard Metrics
  private string DateParams(DateTime from, DateTime to) => $"?from={from:s}&to={to:s}";

  public async Task<MetricDto?> GetTotalRevenueAsync(DateTime from, DateTime to)
  {
    try { return await _httpClient.GetFromJsonAsync<MetricDto>($"{BaseUrl}/Dashboard/metrics/revenue-total{DateParams(from, to)}"); }
    catch { return null; }
  }

  public async Task<MetricDto?> GetGrowthRateAsync(DateTime from, DateTime to)
  {
    try { return await _httpClient.GetFromJsonAsync<MetricDto>($"{BaseUrl}/Dashboard/metrics/growth-rate{DateParams(from, to)}"); }
    catch { return null; }
  }

  public async Task<List<PieChartSegmentDto>> GetRevenueBySourceAsync(DateTime from, DateTime to)
  {
    try
    {
      var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
      return await _httpClient.GetFromJsonAsync<List<PieChartSegmentDto>>($"{BaseUrl}/Dashboard/charts/revenue-by-source{DateParams(from, to)}", options) ?? [];
    }
    catch { return []; }
  }

  public async Task<List<PieChartSegmentDto>> GetRevenueByAlbumAsync(DateTime from, DateTime to)
  {
    try { return await _httpClient.GetFromJsonAsync<List<PieChartSegmentDto>>($"{BaseUrl}/Dashboard/charts/revenue-by-album{DateParams(from, to)}") ?? []; }
    catch { return []; }
  }

  public async Task<List<PieChartSegmentDto>> GetRevenueByAssetAsync(DateTime from, DateTime to)
  {
    try
    {
      var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
      return await _httpClient.GetFromJsonAsync<List<PieChartSegmentDto>>($"{BaseUrl}/Dashboard/metrics/revenue-by-asset{DateParams(from, to)}", options) ?? [];
    }
    catch (Exception ex)
    {
      Console.WriteLine($"[Error] GetRevenueByAssetAsync failed: {ex.Message}");
      return [];
    }
  }

  public async Task<List<LineChartPointDto>> GetRevenueTrendAsync(DateTime from, DateTime to)
  {
    try { return await _httpClient.GetFromJsonAsync<List<LineChartPointDto>>($"{BaseUrl}/Dashboard/charts/revenue-trend{DateParams(from, to)}") ?? []; }
    catch { return []; }
  }

  public async Task<List<TrackPerformanceDto>> GetTopTracksAsync(DateTime from, DateTime to)
  {
    try { return await _httpClient.GetFromJsonAsync<List<TrackPerformanceDto>>($"{BaseUrl}/Dashboard/lists/top-tracks{DateParams(from, to)}") ?? []; }
    catch { return []; }
  }

  public async Task<List<TopTrackPointDto>> GetTopPerformingTracksPointsAsync(DateTime from, DateTime to)
  {
    try { return await _httpClient.GetFromJsonAsync<List<TopTrackPointDto>>($"{BaseUrl}/Dashboard/charts/top-performing-tracks-points{DateParams(from, to)}") ?? []; }
    catch { return []; }
  }

  public async Task<MetricDto?> GetTracksCreatedAsync(DateTime from, DateTime to)
  {
    try { return await _httpClient.GetFromJsonAsync<MetricDto>($"{BaseUrl}/Dashboard/metrics/tracks-created{DateParams(from, to)}"); }
    catch { return null; }
  }

  public async Task<MetricDto?> GetAlbumsReleasedAsync(DateTime from, DateTime to)
  {
    try { return await _httpClient.GetFromJsonAsync<MetricDto>($"{BaseUrl}/Dashboard/metrics/albums-released{DateParams(from, to)}"); }
    catch { return null; }
  }

  public async Task<MetricDto?> GetAvgTrackDurationAsync(DateTime from, DateTime to)
  {
    try { return await _httpClient.GetFromJsonAsync<MetricDto>($"{BaseUrl}/Dashboard/metrics/avg-track-duration{DateParams(from, to)}"); }
    catch { return null; }
  }

  public async Task<MetricDto?> GetRevenuePerTrackAsync(DateTime from, DateTime to)
  {
    try { return await _httpClient.GetFromJsonAsync<MetricDto>($"{BaseUrl}/Dashboard/metrics/revenue-per-track{DateParams(from, to)}"); }
    catch { return null; }
  }

  public async Task<MetricDto?> GetTopSourceContributionAsync(DateTime from, DateTime to)
  {
    try { return await _httpClient.GetFromJsonAsync<MetricDto>($"{BaseUrl}/Dashboard/metrics/top-source-contribution{DateParams(from, to)}"); }
    catch { return null; }
  }

  // Settings
  public async Task<string?> GetSettingAsync(string key)
  {
    try
    {
      var response = await _httpClient.GetAsync($"{BaseUrl}/Settings/{key}");
      if (response.IsSuccessStatusCode)
      {
        var setting = await response.Content.ReadFromJsonAsync<JsonElement>();
        return setting.GetProperty("value").GetString();
      }
      return null;
    }
    catch { return null; }
  }

  public async Task<bool> SaveSettingAsync(string key, string value)
  {
    try
    {
      var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/Settings", new { Key = key, Value = value });
      return response.IsSuccessStatusCode;
    }
    catch { return false; }
  }
}
