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

    // Gemini uses systemInstruction (not a system role message)
    private const string SYSTEM_INSTRUCTION =
        "You are RavnLearn, an intelligent study assistant. " +
        "You help students understand documents, summarize content, and create quizzes. " +
        "When formatting ANY type of quiz (multiple choice, true/false, fill-in-the-blank, or mixed), " +
        "always follow these rules strictly:\n" +
        "1. Use plain underscores ____ for fill-in-the-blank blanks. Never escape underscores as \\_.\n" +
        "2. Always end EVERY quiz with an Answer Key section labeled exactly 'Answer Key:' — " +
        "this is mandatory for ALL question types, no exceptions.\n" +
        "3. Answer Key format per type:\n" +
        "   - Multiple choice:  '1. B) Photosynthesis'\n" +
        "   - True/False:       '2. True' or '2. False'\n" +
        "   - Fill-in-the-blank:'3. mitochondria'\n" +
        "4. Each answer must be on its own line, numbered to match its question.\n" +
        "5. Do not use markdown bold (**) or italic (*) inside questions, options, or the answer key.\n" +
        "6. Keep question numbering as plain numbers: '1.' '2.' '3.' etc.\n" +
        "7. Never omit the Answer Key even if the quiz is short or the answers seem obvious.";

    public static async Task<string> AskAsync(string userMessage, string documentContext = "")
    {
        try
        {
            string url = $"https://generativelanguage.googleapis.com/v1beta/models/{MODEL}:generateContent?key={API_KEY}";

            // Inject extra instructions when the user is asking for a quiz
            bool isQuizRequest = IsQuizRequest(userMessage);
            string quizExtra = isQuizRequest
                ? "\n\nREMINDER: Use plain underscores ____ for blanks (never \\_\\_\\_\\_). " +
                  "End with a numbered 'Answer Key:' section for ALL question types — " +
                  "multiple choice (e.g. '1. B) answer'), true/false (e.g. '2. True'), " +
                  "and fill-in-the-blank (e.g. '3. answer'). Never skip the Answer Key."
                : "";

            string prompt = string.IsNullOrEmpty(documentContext)
                ? userMessage + quizExtra
                : $"Use this document as context:\n\n{documentContext}\n\nUser question: {userMessage}{quizExtra}";

            var body = new JObject(
                // systemInstruction is Gemini's equivalent of a system prompt
                new JProperty("systemInstruction", new JObject(
                    new JProperty("parts", new JArray(
                        new JObject(new JProperty("text", SYSTEM_INSTRUCTION))
                    ))
                )),
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

    private static bool IsQuizRequest(string message)
{
    var lower = message.ToLowerInvariant();
    return lower.Contains("quiz") ||
           lower.Contains("fill in the blank") ||
           lower.Contains("fill-in-the-blank") ||
           lower.Contains("multiple choice") ||
           lower.Contains("test me") ||
           lower.Contains("create questions") ||
           lower.Contains("make questions") ||
           lower.Contains("another one") ||       
           lower.Contains("another quiz") ||      
           lower.Contains("one more") ||        
           lower.Contains("next quiz");        
}
}