using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Http.Json;

namespace Chummer.Blazor.Services;

public class EngineClient
{
    private readonly HttpClient _http;

    public EngineClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<string> ExecuteBuildAsync(string parameters)
    {
        try 
        {
            var response = await _http.PostAsJsonAsync("/api/engine/evaluate", parameters);
            if(response.IsSuccessStatusCode) {
                return $"[API SUCCESS] Core Engine processed: {parameters}";
            }
            return $"[API ERROR] Core Engine returned status: {response.StatusCode}";
        }
        catch(System.Exception ex)
        {
            return $"[NETWORK ERROR] Could not reach Core Engine API. Is it running? Details: {ex.Message}";
        }
    }
}
