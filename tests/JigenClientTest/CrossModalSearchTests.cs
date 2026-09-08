using Jigen.Client;
using Jigen.Client.BaseTypes;
using JigenClientTest.Model;

namespace JigenClientTest;

/// <summary>
/// Client-side cross-modal merge: the server returns raw scores that are not
/// comparable across collections (text↔text cosines dwarf image↔text ones), so
/// the client z-scores each collection's results and merges them on a common
/// scale. The numbers mirror the real nomic results measured by
/// Jigen.SemanticTools.Tests.CrossModalComparabilityTests.
/// </summary>
public class CrossModalSearchTests
{
  private static VectorSearchResult<Entity1> Hit(int key, float score) => new()
  {
    Key = VectorKey.From(key),
    Content = new Entity1 { Id = Guid.NewGuid(), Title = $"hit {key}", Sentence = $"hit {key}" },
    Score = score
  };

  [Fact]
  public void MergeCalibrated_ranks_own_text_and_own_image_on_top_of_the_merged_ranking()
  {
    // Raw scores from the real nomic run: the text group lives ~0.4-0.9, the
    // image group ~0.02-0.10. The own text (0.905) and own image (0.0968) are
    // each the best of their own group.
    var imageHits = new List<VectorSearchResult<Entity1>>
    {
      Hit(100, 0.0968f), // own image
      Hit(101, 0.0580f),
      Hit(102, 0.0282f),
      Hit(103, 0.0279f),
      Hit(104, 0.0240f)
    };
    var textHits = new List<VectorSearchResult<Entity1>>
    {
      Hit(200, 0.9050f), // own text
      Hit(201, 0.4742f),
      Hit(202, 0.4557f),
      Hit(203, 0.4396f),
      Hit(204, 0.4315f)
    };

    // By raw score the image is buried at #6 — the problem the calibration fixes.
    var rawMerged = imageHits.Concat(textHits).OrderByDescending(h => h.Score).ToList();
    Assert.Equal(6, rawMerged.FindIndex(h => VectorKey.ToInt(h.Key) == 100) + 1);

    var merged = CrossModalSearch.MergeCalibrated(
      ("image", imageHits),
      ("text", textHits));

    Assert.Equal(10, merged.Count);

    // Own text first, own image right behind — both "best in their group".
    Assert.Equal("text", merged[0].Modality);
    Assert.Equal(200, VectorKey.ToInt(merged[0].Result.Key));
    Assert.Equal("image", merged[1].Modality);
    Assert.Equal(100, VectorKey.ToInt(merged[1].Result.Key));

    // The rest of each group keeps its internal relative order.
    var mergedTexts = merged.Where(m => m.Modality == "text").Select(m => VectorKey.ToInt(m.Result.Key)).ToList();
    Assert.Equal([200, 201, 202, 203, 204], mergedTexts);
    var mergedImages = merged.Where(m => m.Modality == "image").Select(m => VectorKey.ToInt(m.Result.Key)).ToList();
    Assert.Equal([100, 101, 102, 103, 104], mergedImages);

    // Calibrated scores: own text and own image are both > 1 std above their
    // group mean — they clearly stand out and rank #1/#2 overall. Every other
    // candidate falls below them (the second image is still slightly above its
    // group mean because the three weakest images drag it down, which is the
    // expected z-score behaviour — it just must not overtake the own image).
    Assert.True(merged[0].CalibratedScore > 1f && merged[1].CalibratedScore > 1f);
    Assert.All(merged.Skip(2), r => Assert.True(r.CalibratedScore < merged[1].CalibratedScore));
  }

  [Fact]
  public void MergeCalibrated_handles_empty_and_single_hit_groups()
  {
    var single = new List<VectorSearchResult<Entity1>> { Hit(1, 0.5f) };

    var merged = CrossModalSearch.MergeCalibrated(
      ("empty", new List<VectorSearchResult<Entity1>>()),
      ("single", single));

    Assert.Single(merged);
    Assert.Equal("single", merged[0].Modality);
    // Single-element group: no variance, no ranking signal -> 0.
    Assert.Equal(0f, merged[0].CalibratedScore);
  }

  [Fact]
  public void MergeCalibrated_with_no_groups_returns_empty()
  {
    Assert.Empty(CrossModalSearch.MergeCalibrated<Entity1>());
  }

  [Fact]
  public void MergeAndCalibrate_extension_produces_the_same_ranking_as_MergeCalibrated()
  {
    var imageHits = new List<VectorSearchResult<Entity1>>
    {
      Hit(100, 0.0968f), // own image
      Hit(101, 0.0580f),
      Hit(102, 0.0282f),
      Hit(103, 0.0279f),
      Hit(104, 0.0240f)
    };
    var textHits = new List<VectorSearchResult<Entity1>>
    {
      Hit(200, 0.9050f), // own text
      Hit(201, 0.4742f),
      Hit(202, 0.4557f),
      Hit(203, 0.4396f),
      Hit(204, 0.4315f)
    };

    // The extension needs no labels: each IEnumerable is one group.
    var merged = imageHits.MergeAndCalibrate(textHits);

    Assert.Equal(10, merged.Count);

    // Own text (group "1") first, own image (group "0", the receiver) second.
    Assert.Equal("1", merged[0].Modality);
    Assert.Equal(200, VectorKey.ToInt(merged[0].Result.Key));
    Assert.Equal("0", merged[1].Modality);
    Assert.Equal(100, VectorKey.ToInt(merged[1].Result.Key));

    // Same scores as the labelled version for the same groups.
    var labelled = CrossModalSearch.MergeCalibrated(
      ("image", imageHits),
      ("text", textHits));
    Assert.Equal(
      labelled.Select(r => VectorKey.ToInt(r.Result.Key)),
      merged.Select(r => VectorKey.ToInt(r.Result.Key)));
    Assert.Equal(
      labelled.Select(r => MathF.Round(r.CalibratedScore, 4)),
      merged.Select(r => MathF.Round(r.CalibratedScore, 4)));
  }

  [Fact]
  public void MergeAndCalibrate_supports_three_or_more_groups_and_null_others()
  {
    var audioHits = new List<VectorSearchResult<Entity1>>
    {
      Hit(300, 0.2000f),
      Hit(301, 0.1200f),
      Hit(302, 0.1000f)
    };
    var imageHits = new List<VectorSearchResult<Entity1>> { Hit(100, 0.0968f), Hit(101, 0.0580f) };
    var textHits = new List<VectorSearchResult<Entity1>> { Hit(200, 0.9050f), Hit(201, 0.4742f) };

    var merged = audioHits.MergeAndCalibrate(imageHits, null, textHits);

    // Null groups are skipped; the remaining three groups merge to 7 hits.
    Assert.Equal(7, merged.Count);

    // The z-score is relative to the group: a 2-element group always gives its
    // best exactly +1.0 std, while the 3-element audio group gives its best
    // z ≈ +1.39 (tightest relative spread) — so the audio best ranks first,
    // followed by the image best and the text best (tied at +1.0, stable order
    // keeps the earlier group first).
    Assert.Equal("0", merged[0].Modality); // audio best (z ≈ 1.39)
    Assert.Equal(300, VectorKey.ToInt(merged[0].Result.Key));
    Assert.Equal("1", merged[1].Modality); // image best (z = 1.0)
    Assert.Equal(100, VectorKey.ToInt(merged[1].Result.Key));
    Assert.Equal("2", merged[2].Modality); // text best (z = 1.0)
    Assert.Equal(200, VectorKey.ToInt(merged[2].Result.Key));

    Assert.Equal([300, 100, 200],
      merged.Take(3).Select(r => VectorKey.ToInt(r.Result.Key)).ToArray());
  }

  [Fact]
  public void MergeAndCalibrate_on_an_empty_receiver_returns_empty()
  {
    var empty = new List<VectorSearchResult<Entity1>>();
    var hits = new List<VectorSearchResult<Entity1>> { Hit(1, 0.5f), Hit(2, 0.4f) };

    var merged = empty.MergeAndCalibrate(hits);

    Assert.Equal(2, merged.Count);
    Assert.Equal("1", merged[0].Modality);
  }
}
