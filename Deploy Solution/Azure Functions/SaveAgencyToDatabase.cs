#r "Newtonsoft.Json"

using System;
using Newtonsoft.Json;

public static void Run(string queueItem, out object outputDocument, TraceWriter log) {
    var agency = JsonConvert.DeserializeObject<Agency>(queueItem);
    outputDocument = agency;    
}

public class Agency {
    public string id { get; set; }
    public string name { get; set; }
    public string short_name { get; set; }
    public string parent_id { get; set; }
    public string description { get; set; }
    public string url { get; set; }
    public string agency_url { get; set; }
    public string json_url { get; set; }
    public string slug { get; set; }

    public string[] child_ids { get; set; }
    public dynamic logo { get; set; } 

    public DateTime date_update {
        get {
            return DateTime.UtcNow;
        }
    }
}
