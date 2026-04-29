using Google.Cloud.Language.V1;

namespace CloudNote.Services;

public class NaturalLanguageService
{
    private readonly LanguageServiceClient _client = LanguageServiceClient.Create();

   
    public async Task<NlpResult> AnalyzeAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new NlpResult(Array.Empty<string>(), 0f, "Neutral");

        var document = new Document
        {
            Content  = text.Length > 5000 ? text[..5000] : text,
            Type     = Document.Types.Type.PlainText,
            Language = "en"
        };

        var sentimentTask = _client.AnalyzeSentimentAsync(document);
        var entitiesTask  = _client.AnalyzeEntitiesAsync(document);

        await Task.WhenAll(sentimentTask, entitiesTask);

        AnalyzeSentimentResponse sentimentResp = await sentimentTask;
        AnalyzeEntitiesResponse entitiesResp   = await entitiesTask;

        var tags = entitiesResp.Entities
            .OrderByDescending(e => e.Salience)
            .Take(5)
            .Select(e => e.Name.ToLowerInvariant().Trim())
            .Where(t => t.Length > 2)
            .Distinct()
            .ToArray();

        float score         = sentimentResp.DocumentSentiment.Score;
        string sentimentLabel = score switch
        {
            >= 0.5f  => "Very Positive",
            >= 0.1f  => "Positive",
            > -0.1f  => "Neutral",
            > -0.5f  => "Negative",
            _        => "Very Negative"
        };

        return new NlpResult(tags, score, sentimentLabel);
    }
}

public record NlpResult(string[] Tags, float SentimentScore, string SentimentLabel);
