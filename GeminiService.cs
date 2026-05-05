using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Configuration;

public static class GeminiService
{
    private static readonly HttpClient client = new HttpClient();
    private static readonly string API_KEY =
        new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build()["GeminiApiKey"]!;
    private const string MODEL = "gemini-2.5-flash-lite";

    public static async Task<string> AskAsync(string userMessage, string documentContext = "")
    {
        try
        {
            string url = $"https://generativelanguage.googleapis.com/v1beta/models/{MODEL}:generateContent?key={API_KEY}";

            string prompt = string.IsNullOrEmpty(documentContext)
                ? userMessage
                : $"Use this document as context:\n\n{documentContext}\n\nUser question: {userMessage}";

            var body = new JObject(
                new JProperty("contents", new JArray(
                    new JObject(
                        new JProperty("parts", new JArray(
                            new JObject(new JProperty("text", prompt))
                        ))
                    )
                ))
            );

            var content = new StringContent(body.ToString(), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(url, content);
            var json = await response.Content.ReadAsStringAsync();

            if (json.Contains("RESOURCE_EXHAUSTED"))
                return "⚠️ AI quota reached. Please wait a moment and try again.";

            var parsed = JObject.Parse(json);
            return parsed["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString()
                   ?? "No response from AI.";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}