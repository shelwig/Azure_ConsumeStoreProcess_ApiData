#r "Newtonsoft.Json"

using System;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Azure.Documents.Client;
using Newtonsoft.Json;

public static string Run(string queueItem, out Document outputDocument, TraceWriter log) {
    var queueDocument = JsonConvert.DeserializeObject<dynamic>(queueItem);

    // Populate POCO fields with data from the JSON in the queue message
    outputDocument = new Document();
    outputDocument.id = queueDocument.document_number;
    outputDocument.title = queueDocument.title;
    outputDocument.type = queueDocument.type;
    outputDocument.abstract_text = queueDocument.Property("abstract").Value;
    outputDocument.excerpts = queueDocument.excerpts;
    outputDocument.html_url = queueDocument.html_url;
    outputDocument.pdf_url = queueDocument.pdf_url;
    outputDocument.public_inspection_pdf_url = queueDocument.public_inspection_pdf_url;
    outputDocument.publication_date = queueDocument.publication_date;

    // Get the agency IDs from the "agencies" JSON property
    var agencyIds = (IEnumerable<dynamic>) queueDocument.SelectTokens("$.agencies[*].id");
    outputDocument.agency_ids = agencyIds.Select(i => (string)i).ToArray();

    // Use the Federal Register to obtain additional details about the document
    var documentDetail = GetDocumentDetails(queueDocument.document_number.ToString());
    outputDocument.body_html_url = documentDetail.body_html_url;
    outputDocument.full_text_xml_url = documentDetail.full_text_xml_url;
    outputDocument.raw_text_url = documentDetail.raw_text_url;

    // Get the full text of the document
    string fullText = GetFullText(outputDocument.raw_text_url);
    using (MemoryStream memoryStream = new MemoryStream(pic.Data))
    {
        Attachment attachment = await Client.CreateAttachmentAsync(outputDocument.AttachmentsLink, memoryStream, new MediaOptions { ContentType = "text/plain", Slug = "raw_text" });
    }

    // Return the document ID so it will get thrown into a queue
    // for further processing 
    return outputDocument.id;
}

private static dynamic GetDocumentDetails(string document_number) {
    string apiUrl = $"https://www.federalregister.gov/api/v1/documents/{document_number}.json";
    string jsonString = GetJsonFromApi(apiUrl);
    return JsonConvert.DeserializeObject<dynamic>(jsonString);
}

private static string GetFullText(string raw_text_url) {
    string output = GetJsonFromApi(raw_text_url);

    // Do some light processing of the raw text
    output = Regex.Match(output, @"<PRE>(.|\n)*?<\/PRE>", RegexOptions.IgnoreCase).Value;     // The "raw text" is actually wrapped in HTML.  So only keep text inside the "<pre>" tag
    output = Regex.Replace(output, "<.*?>", String.Empty);                                    // Remove HTML tags from the raw text
    output = Regex.Replace(output, "/[0-9]+/", " ");
    output = Regex.Replace(output, @"\\[0-9]+\\", " ");
    output = Regex.Replace(output, "[`’´]", "'");
    output = Regex.Replace(output, "[“”]", "\"");
    output = output.Replace("''", "\"");
    output = output.Trim(new char[] { ' ', '\t', '\r', '\n' });

    return output;
}

private static string GetJsonFromApi(string apiUrl) {
    string output = "";

    // This code runs too fast!
    // The federal API has a rate limit.  If we get a 429 error, then take a break and try again
    int errorCount = 0;
    while (errorCount < 50) {
        try {
            using (var webClient = new WebClient()) {
                output = webClient.DownloadString(apiUrl);
                break;
            }
        }
        catch (WebException webException) {
            var webResponse = webException.Response as System.Net.HttpWebResponse;
            if (webResponse != null && (int)webResponse.StatusCode == 429) {
                errorCount++;

                // Wait a random amount of time between 2.5 and 20 seconds before trying again
                Random random = new Random();
                int millisecondsToSleep = random.Next(2500, 20000);
                System.Threading.Thread.Sleep(millisecondsToSleep);
            }
            else {
                throw;
            }
        }
        catch {
            throw;
        }
    }

    return output;
}

public class Document
{
    public Document()
    {
        this.predicted_category = "";
        this.predicted_interest_score = 0;
        this.actual_category = "Other";
        this.actual_rating = -1;

        this.date_categorized = null;
        this.date_expanded = null;
        this.date_rated = null;
        this.date_pdf_read = null;
        this.date_reviewed = null;

        this.date_add = DateTime.UtcNow;
        this.date_update = DateTime.UtcNow;
    }

    public string id { get; set; }
    public string title { get; set; }
    public string type { get; set; }
    public string abstract_text { get; set; }
    public string excerpts { get; set; }
    public string html_url { get; set; }
    public string pdf_url { get; set; }
    public string public_inspection_pdf_url { get; set; }
    public string publication_date { get; set; }

    public string[] agency_ids { get; set; }

    public string body_html_url { get; set; }
    public string full_text_xml_url { get; set; }
    public string raw_text_url { get; set; }
    public string[] key_phrases { get; set; }

    public string predicted_category { get; set; }
    public decimal predicted_interest_score { get; set; }

    public string actual_category { get; set; }
    public int actual_rating { get; set; }
    public string date_categorized { get; set; }
    public string date_rated { get; set; }
    public string date_expanded { get; set; }
    public string date_pdf_read { get; set; }
    public string date_reviewed { get; set; }

    public DateTime date_add { get; set; }
    public DateTime date_update { get; set; }
}
