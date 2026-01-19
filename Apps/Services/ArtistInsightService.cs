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
}
