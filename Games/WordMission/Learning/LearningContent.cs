using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace GameBox.Games.WordMission.Learning;

public sealed class LearningContent
{
    public int SessionLength { get; set; } = 9;
    public int MaxNewItemsPerSession { get; set; } = 3;
    public string? InstructionAudio { get; set; }
    public string? SuccessAudio { get; set; }
    public List<SoundDefinition> Sounds { get; set; } = new();
    public List<LetterDefinition> Letters { get; set; } = new();
    public List<WordDefinition> Words { get; set; } = new();
    public List<StageDefinition> Stages { get; set; } = new();

    public static LearningContent Parse(string json) =>
        JsonSerializer.Deserialize<LearningContent>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new FormatException("Content file is empty.");

    public LetterDefinition Letter(string id) => Letters.First(letter => letter.Id == id);
    public WordDefinition Word(string id) => Words.First(word => word.Id == id);
    public SoundDefinition Sound(string id) => Sounds.First(sound => sound.Id == id);
}

public sealed class SoundDefinition
{
    public string Id { get; set; } = "";
    public string DisplayCue { get; set; } = "";
    public string? Audio { get; set; }
}

public sealed class LetterDefinition
{
    public string Id { get; set; } = "";
    public string Uppercase { get; set; } = "";
    public string Lowercase { get; set; } = "";
    public string LetterName { get; set; } = "";
    public string PrimarySoundId { get; set; } = "";
    public string? LetterNameAudio { get; set; }
    public int Stage { get; set; } = 1;
}

public sealed class WordDefinition
{
    public string Id { get; set; } = "";
    public string Display { get; set; } = "";
    public List<string> LetterIds { get; set; } = new();
    public List<string> SoundIds { get; set; } = new();
    public int Stage { get; set; } = 2;
    public int Difficulty { get; set; } = 1;
    public bool Phonetic { get; set; }
    public string ReviewStatus { get; set; } = "needs-review";
    public string VisualId { get; set; } = "";
    public string? Image { get; set; }
    public string? WordAudio { get; set; }
    public List<string> Activities { get; set; } = new();
}

public sealed class StageDefinition
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public int MasteredRequired { get; set; }
}

public static class ContentValidator
{
    private static readonly HashSet<string> KnownActivities = new(StringComparer.Ordinal)
    {
        "build", "choose", "gate", "tank"
    };

    public static IReadOnlyList<string> Validate(LearningContent content, string source)
    {
        var errors = new List<string>();
        if (content.Sounds is null || content.Letters is null || content.Words is null
            || content.Stages is null)
        {
            errors.Add($"{source}: one or more required content lists are missing.");
            return errors;
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        void CheckId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) errors.Add($"{source}: item has an empty id.");
            else if (!ids.Add(id)) errors.Add($"{source}: duplicate id '{id}'.");
        }

        foreach (var sound in content.Sounds)
        {
            if (sound is null) { errors.Add($"{source}: a sound entry is null."); continue; }
            CheckId(sound.Id);
            if (string.IsNullOrWhiteSpace(sound.DisplayCue))
                errors.Add($"{source}: sound '{sound.Id}' has no visual fallback cue.");
        }
        var sounds = content.Sounds.Where(sound => sound is not null)
            .Select(sound => sound.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var letter in content.Letters)
        {
            if (letter is null) { errors.Add($"{source}: a letter entry is null."); continue; }
            CheckId(letter.Id);
            if (string.IsNullOrWhiteSpace(letter.Uppercase) || string.IsNullOrWhiteSpace(letter.Lowercase)
                || string.IsNullOrWhiteSpace(letter.LetterName))
                errors.Add($"{source}: letter '{letter.Id}' lacks display or name text.");
            if (!sounds.Contains(letter.PrimarySoundId))
                errors.Add($"{source}: letter '{letter.Id}' references unknown sound '{letter.PrimarySoundId}'.");
            if (letter.Stage < 1 || letter.Stage > 4)
                errors.Add($"{source}: letter '{letter.Id}' has invalid stage {letter.Stage}.");
        }
        var letters = content.Letters.Where(letter => letter is not null)
            .Select(letter => letter.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var word in content.Words)
        {
            if (word is null) { errors.Add($"{source}: a word entry is null."); continue; }
            CheckId(word.Id);
            if (word.LetterIds is null || word.SoundIds is null || word.Activities is null)
            {
                errors.Add($"{source}: word '{word.Id}' lacks letters, sounds, or activities.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(word.Display) || word.LetterIds.Count < 2
                || word.LetterIds.Count != word.SoundIds.Count)
                errors.Add($"{source}: word '{word.Id}' has invalid display, letters, or sound sequence.");
            if (word.Difficulty < 1 || word.Difficulty > 4 || word.Stage < 2 || word.Stage > 4)
                errors.Add($"{source}: word '{word.Id}' has invalid difficulty or stage.");
            if (!word.Phonetic)
                errors.Add($"{source}: word '{word.Id}' is not marked phonetic for this starter pack.");
            if (word.ReviewStatus is not ("needs-review" or "adult-reviewed"))
                errors.Add($"{source}: word '{word.Id}' has invalid review status.");
            if (string.IsNullOrWhiteSpace(word.VisualId) && string.IsNullOrWhiteSpace(word.Image))
                errors.Add($"{source}: word '{word.Id}' lacks a visual target.");
            foreach (var letterId in word.LetterIds.Where(id => !letters.Contains(id)))
                errors.Add($"{source}: word '{word.Id}' references unknown letter '{letterId}'.");
            foreach (var soundId in word.SoundIds.Where(id => !sounds.Contains(id)))
                errors.Add($"{source}: word '{word.Id}' references unknown sound '{soundId}'.");
            foreach (var activity in word.Activities.Where(id => !KnownActivities.Contains(id)))
                errors.Add($"{source}: word '{word.Id}' references unknown activity '{activity}'.");
            if (word.Activities.Count == 0)
                errors.Add($"{source}: word '{word.Id}' has no eligible activities.");
            if (word.Stage == 2 && word.LetterIds.Count != 2
                || word.Stage == 3 && word.LetterIds.Count is < 3 or > 4
                || word.Stage == 4 && word.LetterIds.Count < 5)
                errors.Add($"{source}: word '{word.Id}' length does not match its stage.");
            if (word.LetterIds.All(letters.Contains))
            {
                var spelling = string.Concat(word.LetterIds.Select(id =>
                    content.Letters.First(letter => letter is not null && letter.Id == id).Uppercase));
                if (!string.Equals(word.Display, spelling, StringComparison.OrdinalIgnoreCase))
                    errors.Add($"{source}: word '{word.Id}' display does not match its letters.");
            }
        }
        var stageIds = content.Stages.Where(stage => stage is not null)
            .Select(stage => stage.Id).ToList();
        if (!stageIds.SequenceEqual(new[] { 1, 2, 3, 4 }))
            errors.Add($"{source}: stages must be listed once in order from 1 to 4.");
        if (content.SessionLength < 8 || content.SessionLength > 12)
            errors.Add($"{source}: session length must be 8–12.");
        if (content.MaxNewItemsPerSession < 1 || content.MaxNewItemsPerSession > 4)
            errors.Add($"{source}: max new items must be 1–4.");
        if (content.Letters.Count < 3 || content.Words.Count < 2)
            errors.Add($"{source}: the starter pack needs at least 3 letters and 2 words.");
        return errors;
    }
}
