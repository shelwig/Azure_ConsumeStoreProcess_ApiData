#r "Newtonsoft.Json"

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

public static void Run(string queueItem, dynamic inputDocument, TraceWriter log) {
    if (inputDocument != null) {
        string apiKey = System.Environment.GetEnvironmentVariable("score_category_key", EnvironmentVariableTarget.Process);
        string apiUrl = "https://ussouthcentral.services.azureml.net/workspaces/df3f6704dc804750b90088ef0629d26e/services/95a34be7261f4bedac303f2ca67f9ec2/execute?api-version=2.0";

        string[,] values = new string[,] { { inputDocument.type, inputDocument.title, inputDocument.raw_text } };

        using (var client = new HttpClient()) {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            client.BaseAddress = new Uri(apiUrl);

            var scoreRequest = new {
                Inputs = new Dictionary<string, StringTable>() {
                    {
                        "input1",
                        new StringTable()
                        {
                            ColumnNames = new string[] {"type", "title", "raw_text"},
                            Values = values
                        }
                    },
                },
                GlobalParameters = new Dictionary<string, string>()
            };

            var response = client.PostAsJsonAsync("", scoreRequest).Result;

            if (response.IsSuccessStatusCode) {
                string resultJson = response.Content.ReadAsStringAsync().Result;
                log.Info("Success!");
                log.Info(resultJson);

                var result = JsonConvert.DeserializeObject<dynamic>(resultJson);
                string predictedCategory = result.Results.output1.value.Values[0].Last.ToString();

                log.Info($"Predicted Category: {predictedCategory}");

                inputDocument.predicted_category = predictedCategory;
                inputDocument.date_update = DateTime.UtcNow;
            }
            else {
                log.Info(string.Format("The request failed with status code: {0}", response.StatusCode));

                // Print the headers - they include the requert ID and the timestamp, which are useful for debugging the failure
                log.Info(response.Headers.ToString());

                string responseContent = response.Content.ReadAsStringAsync().Result;
                log.Info(responseContent);

                throw new Exception(string.Format("The request failed with status code: {0}", response.StatusCode));
            }
        }
    }
}

public class StringTable
{
    public string[] ColumnNames { get; set; }
    public string[,] Values { get; set; }
}