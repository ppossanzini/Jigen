using System.Net;
using System.Net.Http.Json;

namespace JigenTests;

/// <summary>
/// Manual dev tool, not meant to run under normal CI: seeds a live Jigen server with
/// random topic-clustered sentences (server-side embeddings via the configured Onnx
/// model) so the Insight frontend has real, semantically-searchable data — useful to
/// demo the Workbench search and the "highlight results on the HNSW graph" feature.
///
/// Requires a server already running with embeddings enabled (Jigen-AllInOne, since
/// plain Jigen.csproj dispatches CalculateEmbeddings remotely via Kaido/RabbitMQ).
/// Run explicitly: dotnet test --filter FullyQualifiedName~SeedDemoDatabase
/// </summary>
public class SeedDemoDatabaseTest
{
  private const string BaseUrl = "http://localhost:13223";
  private const string DatabaseName = "insight-demo";
  private const string Collection = "articles";
  private const int SentencesPerTopic = 40;

  [Fact]
  public async Task Seed_demo_database_with_random_topic_sentences()
  {
    var handler = new HttpClientHandler { CookieContainer = new CookieContainer(), UseCookies = true };
    using var http = new HttpClient(handler) { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromMinutes(5) };

    var login = await http.PostAsJsonAsync("/api/identity/login", new { userName = "guest", password = "P@ssw0rd!" });
    Assert.True(login.IsSuccessStatusCode, $"login failed: {login.StatusCode} — is the server running at {BaseUrl}?");

    // Idempotent: a Conflict just means a previous run already created it.
    var create = await http.PostAsync($"/api/database?name={DatabaseName}", null);
    Assert.True(create.IsSuccessStatusCode || create.StatusCode == HttpStatusCode.Conflict,
      $"create database failed: {create.StatusCode}");

    var sentences = GenerateSentences();
    var inserted = 0;

    foreach (var (title, sentence) in sentences)
    {
      var key = Guid.NewGuid();
      var response = await http.PutAsJsonAsync(
        $"/api/database/{DatabaseName}/collections/{Collection}/documents/{key}?keyType=guid",
        new { payload = new { title, body = sentence }, sentence });

      Assert.True(response.IsSuccessStatusCode, $"insert failed for '{title}': {response.StatusCode}");
      inserted++;
    }

    Assert.Equal(sentences.Count, inserted);
  }

  /// <summary>
  /// Builds sentences from a handful of distinct topic templates (subject + action +
  /// object) instead of pure word soup, so the resulting embeddings form visually
  /// distinct clusters in the HNSW graph's PCA projection and a query about one topic
  /// produces a meaningful near-to-far gradient over the results.
  /// </summary>
  private static List<(string title, string sentence)> GenerateSentences()
  {
    var rng = new Random(20260714);

    var topics = new (string Name, string[] Subjects, string[] Actions, string[] Objects)[]
    {
      new("cooking",
        ["The chef", "My grandmother", "A young cook", "The bakery"],
        ["kneads", "simmers", "roasts", "seasons", "caramelizes"],
        ["a rich tomato sauce", "fresh sourdough bread", "a batch of buttery croissants", "a pot of lentil soup", "grilled vegetables with olive oil"]),
      new("space",
        ["The astronaut", "A distant telescope", "The rover", "Mission control"],
        ["observes", "detects", "orbits", "photographs", "analyzes"],
        ["a newly formed nebula", "signals from a pulsar", "the rings of Saturn", "an exoplanet's atmosphere", "debris near the space station"]),
      new("finance",
        ["The analyst", "A hedge fund", "The central bank", "An investor"],
        ["forecasts", "hedges against", "reports", "adjusts", "monitors"],
        ["rising inflation", "a volatile currency market", "quarterly earnings growth", "interest rate changes", "a diversified bond portfolio"]),
      new("sports",
        ["The striker", "A young athlete", "The coach", "The team"],
        ["scores", "trains for", "celebrates", "prepares for", "dominates"],
        ["a last-minute goal", "the regional marathon", "a hard-fought victory", "the championship final", "a tough away match"]),
      new("gardening",
        ["The gardener", "A backyard hobbyist", "The greenhouse", "My neighbor"],
        ["prunes", "waters", "transplants", "fertilizes", "harvests"],
        ["the rose bushes", "a row of heirloom tomatoes", "young basil seedlings", "the herb garden", "a bed of spring tulips"])
    };

    var sentences = new List<(string, string)>();

    foreach (var topic in topics)
    {
      for (var i = 0; i < SentencesPerTopic; i++)
      {
        var subject = topic.Subjects[rng.Next(topic.Subjects.Length)];
        var action = topic.Actions[rng.Next(topic.Actions.Length)];
        var obj = topic.Objects[rng.Next(topic.Objects.Length)];
        var sentence = $"{subject} {action} {obj}.";
        sentences.Add(($"{topic.Name} #{i + 1}", sentence));
      }
    }

    return sentences;
  }
}
