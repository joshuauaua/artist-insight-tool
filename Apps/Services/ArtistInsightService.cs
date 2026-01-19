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
  public async Task<List<RevenueEntry>> GetRevenueEntriesAsync()
  {
    try
    {
      return await _httpClient.GetFromJsonAsync<List<RevenueEntry>>($"{BaseUrl}/Revenue") ?? new List<RevenueEntry>();
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error fetching revenue entries: {ex.Message}");
      return new List<RevenueEntry>();
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
      return null;
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error creating revenue entry: {ex.Message}");
      return null;
    }
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

  public async Task<bool> DeleteTemplateAsync(int id)
  {
    try
    {
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
    try { return await _httpClient.GetFromJsonAsync<List<PieChartSegmentDto>>($"{BaseUrl}/Dashboard/charts/revenue-by-source{DateParams(from, to)}") ?? []; }
    catch { return []; }
  }

  public async Task<List<PieChartSegmentDto>> GetRevenueByAlbumAsync(DateTime from, DateTime to)
  {
    try { return await _httpClient.GetFromJsonAsync<List<PieChartSegmentDto>>($"{BaseUrl}/Dashboard/charts/revenue-by-album{DateParams(from, to)}") ?? []; }
    catch { return []; }
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
}
