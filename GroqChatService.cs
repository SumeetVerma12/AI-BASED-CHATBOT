using System.Text;
using System.Text.Json;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

public interface IGroqChatService
{
    Task<string> AskAsync(string userMessage, CancellationToken ct = default);
}

public sealed class GroqChatService : IGroqChatService
{
    private readonly HttpClient _http;
    private readonly string _model;

    public GroqChatService(IHttpClientFactory factory, IConfiguration config)
    {
        _http = factory.CreateClient("groq");
        _model = config["Groq:Model"] ?? "llama-3.3-70b-versatile";
    }

    public async Task<string> AskAsync(string userMessage, CancellationToken ct = default)
    {
        var payload = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = "You are a helpful, concise assistant." },
                new { role = "user", content = userMessage }
            },
            temperature = 0.2,
            max_tokens = 512
        };

        // ✅ Use correct Groq path (no leading slash)
        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var resp = await _http.PostAsync("chat/completions", content, ct);

        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            return $"❌ Error {(int)resp.StatusCode}: {body}";

        try
        {
            using var doc = JsonDocument.Parse(body);
            var answer = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return answer?.Trim() ?? "(no response)";
        }
        catch (Exception ex)
        {
            return $"⚠️ Parse error: {ex.Message}\nResponse: {body}";
        }
    }
}
