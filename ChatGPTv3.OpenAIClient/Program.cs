using System.Net.Http.Headers;

var apiKey = "yZ7B6M1JT3b5izrl25";
var baseUrl = "http://localhost:6333";

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
http.DefaultRequestHeaders.Add("api-key", apiKey);

// Test health
try
{
    var r = await http.GetAsync($"{baseUrl}/health");
    Console.WriteLine($"Health: HTTP {(int)r.StatusCode} — {await r.Content.ReadAsStringAsync()}");
}
catch (Exception ex) { Console.WriteLine($"Health FAIL: {ex.Message}"); }

// Test collections
try
{
    var r = await http.GetAsync($"{baseUrl}/collections");
    var body = await r.Content.ReadAsStringAsync();
    Console.WriteLine($"Collections: HTTP {(int)r.StatusCode}");
    Console.WriteLine(body[..Math.Min(body.Length, 500)]);
}
catch (Exception ex) { Console.WriteLine($"Collections FAIL: {ex.Message}"); }
