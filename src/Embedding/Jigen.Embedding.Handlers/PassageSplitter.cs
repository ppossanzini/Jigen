using System.Text.RegularExpressions;
using Jigen.SemanticTools;

namespace Jigen.Embedding.Handlers;

internal static partial class PassageSplitter
{
  internal sealed record Passage(string Text, int StartToken, int TokenCount);

  public static Passage[] Split(
    string text,
    int passageTokenSize,
    int passageOverlapSize,
    IEmbeddingGenerator generator)
  {
    var words = WordPattern().Matches(text).Cast<Match>().ToArray();
    if (words.Length == 0)
      return [];

    var size = Math.Max(passageTokenSize, 1);
    var overlap = Math.Clamp(passageOverlapSize, 0, size - 1);
    var passages = new List<Passage>();
    var startWord = 0;

    while (startWord < words.Length)
    {
      var endWord = FindPassageEnd(text, words, startWord, size, generator);
      var startCharacter = words[startWord].Index;
      var endCharacter = words[endWord].Index + words[endWord].Length;
      var passageText = text[startCharacter..endCharacter];
      var tokenCount = generator.CountTokens(passageText);
      var startToken = CountContentTokens(text[..startCharacter], generator);
      passages.Add(new Passage(passageText, startToken, tokenCount));

      if (endWord == words.Length - 1)
        break;

      startWord = FindNextStart(text, words, startWord, endWord, overlap, generator);
    }

    return passages.ToArray();
  }

    private static int FindPassageEnd(
      string text,
      Match[] words,
      int startWord,
      int passageTokenSize,
      IEmbeddingGenerator generator)
    {
    var low = startWord;
    var high = words.Length - 1;
    var best = startWord;

    while (low <= high)
    {
      var middle = low + (high - low) / 2;
      var candidate = Slice(text, words, startWord, middle);
      if (generator.CountTokens(candidate) <= passageTokenSize)
      {
        best = middle;
        low = middle + 1;
      }
      else
      {
        high = middle - 1;
      }
    }

    return best;
  }

    private static int FindNextStart(
      string text,
      Match[] words,
      int currentStart,
      int currentEnd,
      int overlapTokens,
      IEmbeddingGenerator generator)
    {
    if (overlapTokens == 0)
      return currentEnd + 1;

    var nextStart = currentEnd;
    while (nextStart > currentStart && generator.CountTokens(Slice(text, words, nextStart, currentEnd)) < overlapTokens)
      nextStart--;

    return Math.Max(nextStart, currentStart + 1);
  }

    private static int CountContentTokens(string text, IEmbeddingGenerator generator) =>
    string.IsNullOrWhiteSpace(text) ? 0 : Math.Max(generator.CountTokens(text) - 2, 0);

    private static string Slice(string text, Match[] words, int startWord, int endWord)
    {
    var start = words[startWord].Index;
    var end = words[endWord].Index + words[endWord].Length;
    return text[start..end];
  }

    [GeneratedRegex(@"\S+")]
  private static partial Regex WordPattern();
}
