using System;
using System.Collections.Generic;
using System.Linq;

namespace GameBox.Games.WordMission.Learning;

public enum ActivityKind { FindSound, BuildWord, ChooseWord, WordGate, TankWord }

public sealed record LearningChallenge(ActivityKind Activity, string ItemId, IReadOnlyList<string> Choices);

public sealed class SessionGenerator
{
    private static readonly ActivityKind[] Pattern =
    {
        ActivityKind.FindSound, ActivityKind.FindSound, ActivityKind.FindSound,
        ActivityKind.BuildWord, ActivityKind.WordGate, ActivityKind.ChooseWord,
        ActivityKind.TankWord, ActivityKind.FindSound, ActivityKind.BuildWord
    };

    public IReadOnlyList<LearningChallenge> Generate(LearningContent content,
        WordMissionProgress progress, int length, int stage, int seed)
    {
        if (length < 1) throw new ArgumentOutOfRangeException(nameof(length));
        var random = new Random(seed);
        var introduced = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<LearningChallenge>(length);
        var letters = content.Letters.Where(letter => letter.Stage <= stage).ToList();
        // One short-word preview keeps the first mission playful while the map stays locked.
        var wordStage = Math.Max(2, stage);
        var words = content.Words.Where(word => word.Stage <= wordStage
            && word.Difficulty <= Math.Max(1, stage - 1)).ToList();
        var letterNewLimit = Math.Max(1, content.MaxNewItemsPerSession - (words.Count > 0 ? 1 : 0));
        if (letters.Count < 3) throw new InvalidOperationException("Find lyden requires three letters.");

        for (var index = 0; index < length; index++)
        {
            var activity = Pattern[index % Pattern.Length];
            var activityId = activity switch
            {
                ActivityKind.BuildWord => "build",
                ActivityKind.ChooseWord => "choose",
                ActivityKind.WordGate => "gate",
                ActivityKind.TankWord => "tank",
                _ => ""
            };
            var eligibleWords = words.Where(word => word.Activities.Contains(activityId)).ToList();
            if (activity != ActivityKind.FindSound && (eligibleWords.Count == 0
                || (activity is ActivityKind.ChooseWord or ActivityKind.TankWord && eligibleWords.Count < 2)))
                activity = ActivityKind.FindSound;

            if (activity == ActivityKind.FindSound)
            {
                var target = Select(letters, letter => letter.Id, progress, introduced,
                    letterNewLimit, random, result.LastOrDefault()?.ItemId);
                var choices = letters.Where(letter => letter.Id != target.Id)
                    .OrderBy(_ => random.Next()).Take(Math.Min(3, letters.Count - 1))
                    .Select(letter => letter.Id).Append(target.Id).OrderBy(_ => random.Next()).ToList();
                result.Add(new LearningChallenge(activity, target.Id, choices));
            }
            else
            {
                var target = Select(eligibleWords, word => word.Id, progress, introduced,
                    content.MaxNewItemsPerSession, random, result.LastOrDefault()?.ItemId);
                IReadOnlyList<string> choices;
                if (activity is ActivityKind.BuildWord or ActivityKind.WordGate)
                {
                    choices = target.LetterIds.OrderBy(_ => random.Next()).ToList();
                }
                else
                {
                    // Prefer visibly different words for beginning readers.
                    var distractors = eligibleWords.Where(word => word.Id != target.Id
                        && Similarity(target.Display, word.Display) <=
                            (target.Difficulty == 1 ? 2 : 3)).ToList();
                    if (distractors.Count == 0)
                        distractors = eligibleWords.Where(word => word.Id != target.Id).ToList();
                    choices = distractors.OrderBy(word => Similarity(target.Display, word.Display))
                        .ThenBy(_ => random.Next())
                        .Take(stage < 3 ? 1 : 2)
                        .Select(word => word.Id).Append(target.Id)
                        .OrderBy(_ => random.Next()).ToList();
                }
                result.Add(new LearningChallenge(activity, target.Id, choices));
            }
        }
        return result;
    }

    private static T Select<T>(IReadOnlyList<T> candidates, Func<T, string> idOf,
        WordMissionProgress progress, HashSet<string> introduced, int newLimit,
        Random random, string? previousId)
    {
        var weighted = candidates.Select(candidate =>
        {
            var id = idOf(candidate);
            var item = progress.Get(id);
            var isNew = item.Attempts == 0 && !introduced.Contains(id);
            var weight = isNew && introduced.Count >= newLimit ? 0.0
                : item.Attempts == 0 ? 4.0
                : item.LastOutcomeDifficult ? 12.0
                : item.Mastery == MasteryState.Mastered
                    ? (DateTime.UtcNow - item.LastSeenUtc).TotalDays >= 7 ? 2.5 : 0.6
                    : 6.0;
            if (id == previousId && candidates.Count > 1) weight *= 0.25;
            return (candidate, id, weight);
        }).ToList();
        var total = weighted.Sum(entry => entry.weight);
        if (total <= 0)
        {
            // A new content type can be required before the session has seen it.
            var fallback = candidates[random.Next(candidates.Count)];
            introduced.Add(idOf(fallback));
            return fallback;
        }
        var draw = random.NextDouble() * total;
        foreach (var entry in weighted)
        {
            draw -= entry.weight;
            if (draw <= 0)
            {
                if (progress.Get(entry.id).Attempts == 0) introduced.Add(entry.id);
                return entry.candidate;
            }
        }
        return weighted[^1].candidate;
    }

    private static int Similarity(string a, string b)
    {
        var score = a.Length == b.Length ? 2 : 0;
        for (var index = 0; index < Math.Min(a.Length, b.Length); index++)
            if (a[index] == b[index]) score++;
        return score;
    }
}
