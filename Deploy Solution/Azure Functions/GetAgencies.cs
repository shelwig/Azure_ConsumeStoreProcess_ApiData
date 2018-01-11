#r "Newtonsoft.Json"

using System;
using System.Net;
using Newtonsoft.Json;

public static void Run(TimerInfo timer, ICollector<string> outputQueue, TraceWriter log) {
    string apiUrl = "https://www.federalregister.gov/api/v1/agencies";
    string jsonString = "";

    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
    using (var webClient = new WebClient()) {
        jsonString = webClient.DownloadString(apiUrl);
    }

    var agencies = JsonConvert.DeserializeObject<dynamic>(jsonString);

    if (agencies != null) {
        foreach (var agency in agencies) {
            outputQueue.Add(JsonConvert.SerializeObject(agency));
        }
    }
}