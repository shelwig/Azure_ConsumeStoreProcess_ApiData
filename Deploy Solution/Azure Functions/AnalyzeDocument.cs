#r "Newtonsoft.Json"

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

public static void Run(string queueItem, dynamic inputDocument, TraceWriter log) {
    if (inputDocument != null) {
        string apiKey = System.Environment.GetEnvironmentVariable("text_analytics_key", EnvironmentVariableTarget.Process);
        string apiUrl = "https://westus.api.cognitive.microsoft.com/text/analytics/v2.0/keyPhrases";

        // The Cognitive Services Text API only allows "documents" to
        // have a maximum size of 5K characters.  To process the whole
        // document, we'll break it up into "chunks" and have the API
        // extract key words from each chunk.  Then we'll re-assemble
        // the results into a comprehensive list for the whole document.

        // Break up the document text into chunks that are small enough
        // to be fed to the API
        var chunks = ConvertTextToChunks(queueItem, (string) inputDocument.raw_text);

        // Call the Cognitive Services Text Analytics API
        string resultJson = CallTextAnalyticsApi(apiUrl, apiKey, chunks);

        // Parse JSON returned by the Text Analytics API
        var result = JsonConvert.DeserializeObject<dynamic>(resultJson);

        // Loop through the results for each chunk and put them into
        // a collection
        var keyPhrasesCollection = new List<string[]>();
        if (result.documents != null && result.documents.HasValues == true) {
            foreach (var document in result.documents) {
                keyPhrasesCollection.Add(document.keyPhrases.ToObject<string[]>());
            }
        }

        // Do some light text cleaning on the key phrases
        foreach (var keyPhrasesArray in keyPhrasesCollection) {
            for (int i = 0; i < keyPhrasesArray.Length; i++) {
                keyPhrasesArray[i] = Regex.Replace(keyPhrasesArray[i], "^G +", "");
                keyPhrasesArray[i] = Regex.Replace(keyPhrasesArray[i], " +", " ");
            }
        }

        // Consolidate the key phrases from all of the chunks into
        // one single list
        var keyPhrases = ConsolidateKeyPhrasesCollection(keyPhrasesCollection);

        inputDocument.key_phrases = keyPhrases;
        inputDocument.date_update = DateTime.UtcNow;
    }
}

private static List<TextAnalyticsDocumentChunk> ConvertTextToChunks (string document_number, string raw_text) {
    var output = new List<TextAnalyticsDocumentChunk>();

    int numberOfChunks = (int) Math.Ceiling((double) raw_text.Length / (double) API_MAX_TEXT_SIZE);
    int offset = 0;

    for (int i = 0; i < numberOfChunks; ++i) {
        int chunkSize = API_MAX_TEXT_SIZE;

        if (offset + chunkSize > raw_text.Length) {
            chunkSize = raw_text.Length - offset;
        }

        string chunkText = raw_text.Substring(offset, chunkSize);
        string chunkId = $"{document_number}/{i}";
        var chunk = new TextAnalyticsDocumentChunk() { Id = chunkId, Language = "en", Text = chunkText };
        output.Add(chunk);

        offset += chunkSize;
    }

    return output;
}

private static string CallTextAnalyticsApi(string apiUrl, string apiKey, List<TextAnalyticsDocumentChunk> chunks) {
    using (var httpClient = new HttpClient()) {
        // Request headers
        httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", apiKey);

        // Build the request body by converting our "chunk" objects to JSON array
        string requestBody = String.Join(", ", chunks.Select(c => c.ToJson()).ToArray());
        requestBody = "{ \"documents\": [" + requestBody + "] }";
        byte[] byteData = Encoding.UTF8.GetBytes(requestBody);

        HttpResponseMessage response;
        using (var content = new ByteArrayContent(byteData)) {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            response = httpClient.PostAsync(apiUrl, content).Result;
        }            

        return response.Content.ReadAsStringAsync().Result;
    }
}

private static List<string> ConsolidateKeyPhrasesCollection(List<string[]> keyPhrasesCollection) {
    // The order of they key phrases is important.  The most significant
    // phrases are listed first.  Therefore, we want to preserve that
    // order when consolidating the key phrase lists from each chunk.
    // We will do that by adding phrases to our consolidated list
    // in a round-robin fashion.  Also, we want the consolidated list
    // to have only unique phrases.

    var output = new List<string>();

    if (keyPhrasesCollection.Any() == true) {
        var keyPhraseHashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        int maxLength = keyPhrasesCollection.Max(kp => kp.Length);
        for (int i = 0; i < maxLength; i++) {
            foreach (var chunkKeyPhrases in keyPhrasesCollection) {
                if (chunkKeyPhrases.Length > i) {
                    string keyPhrase = chunkKeyPhrases[i];
                    if (keyPhraseHashSet.Add(keyPhrase) == true) {
                        output.Add(keyPhrase);
                    }
                }
            }
        }
    }

    return output;
}

public const int API_MAX_TEXT_SIZE = 5000;

private class TextAnalyticsDocumentChunk {
    public string Id { get; set; }
    public string Language { get; set; }
    public string Text { get; set; }

    public string ToJson() {
        return $"{{ \"language\": \"{this.Language}\", \"id\":\"{this.Id}\",  \"text\": \"{this.Text.Replace("\\", "\\\\").Replace("\"", "\\\"")}\" }}";
    }
}