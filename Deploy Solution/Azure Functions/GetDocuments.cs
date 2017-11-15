#r "Newtonsoft.Json"

using System;
using System.Net;
using Newtonsoft.Json;

public static void Run(TimerInfo timer, IEnumerable<DocumentId> existingIds, ICollector<string> outputQueue, TraceWriter log) {
    // Create a hash set with all of the document numbers already in
    // the database. This will allow us to efficiently determin if a
    // document already exists in the database or if it needs to be
    // added.
    var idHashSet = new HashSet<string>(existingIds.Select(i => i.id));

    // The URL for the first page of the documents list
    string apiUrl = "https://www.federalregister.gov/api/v1/documents.json?per_page=5&order=newest";

    // The API returns paged results.  As long as the "next_page_url"
    // property is not null, keep hitting it to get more results
    while (string.IsNullOrWhiteSpace(apiUrl) == false) {
        string jsonString = GetJsonFromApi(apiUrl);
        var documentList = JsonConvert.DeserializeObject<dynamic>(jsonString);

        if (documentList == null) {
            apiUrl = null;
        }
        else {
            // Loop through the documents we got from the API and
            // add them to the queue (to be processed by a different
            // function) UNLESS the document already exists in the
            // database.
            if (documentList.results != null) {
                foreach (var document in documentList.results) {
                    if (document.document_number != null) {
                        if (idHashSet.Contains(document.document_number.ToString()) == false) {
                            outputQueue.Add(JsonConvert.SerializeObject(document));
                        }
                    }
                }
            }

            apiUrl = documentList.next_page_url;
        }
    }
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
            if (webResponse != null && (int) webResponse.StatusCode == 429) {
                errorCount++;
                System.Threading.Thread.Sleep(2500);
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

public class DocumentId {
    public string id { get; set; }
}