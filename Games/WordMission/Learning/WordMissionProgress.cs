using System;
using System.Collections.Generic;
using System.Linq;

namespace GameBox.Games.WordMission.Learning;

public enum MasteryState { New, Learning, Practicing, Mastered }

public sealed class LearningItemProgress
{
    public int Attempts { get; set; }
    public int CorrectFirstTry { get; set; }
    public int CorrectAfterHelp { get; set; }
    public int IncorrectAttempts { get; set; }
    public int MasteryScore { get; set; }
    public DateTime LastSeenUtc { get; set; }
    public bool LastOutcomeDifficult { get; set; }
    public MasteryState Mastery { get; set; }
}

public sealed class WordMissionProgress
{
    public Dictionary<string, LearningItemProgress> Items { get; set; } = new(StringComparer.Ordinal);
    public int UnlockedStage { get; set; } = 1;
    public int CompletedSessions { get; set; }
    public int AssistedAnswers { get; set; }
    public List<string> Achievements { get; set; } = new();
    public bool SoundEnabled { get; set; } = true;
    public string DisplayCase { get; set; } = "upper";

    public LearningItemProgress Get(string id)
    {
        Items ??= new Dictionary<string, LearningItemProgress>(StringComparer.Ordinal);
        return Items.TryGetValue(id, out var item) ? item : new LearningItemProgress();
    }

    public void Record(string id, int wrongAttempts, bool assisted, DateTime nowUtc)
    {
        Items ??= new Dictionary<string, LearningItemProgress>(StringComparer.Ordinal);
        if (!Items.TryGetValue(id, out var item))
            Items[id] = item = new LearningItemProgress();
        item.Attempts++;
        item.IncorrectAttempts += Math.Max(0, wrongAttempts);
        item.LastSeenUtc = nowUtc;
        item.LastOutcomeDifficult = wrongAttempts >= 2 || assisted;
        if (assisted)
        {
            item.CorrectAfterHelp++;
            AssistedAnswers++;
            item.MasteryScore = Math.Max(0, item.MasteryScore - 1);
        }
        else if (wrongAttempts == 0)
        {
            item.CorrectFirstTry++;
            item.MasteryScore = Math.Min(10, item.MasteryScore + 2);
        }
        else
        {
            item.MasteryScore = Math.Max(0, item.MasteryScore + (wrongAttempts == 1 ? 1 : -1));
        }
        item.Mastery = item.MasteryScore >= 6 && item.CorrectFirstTry >= 3
            ? MasteryState.Mastered
            : item.Attempts == 1 ? MasteryState.Learning : MasteryState.Practicing;
        if (item.LastOutcomeDifficult && item.Mastery == MasteryState.Mastered)
            item.Mastery = MasteryState.Practicing;
    }

    public void FinishSession(LearningContent content)
    {
        CompletedSessions++;
        var masteredLetters = content.Letters.Count(letter => Get(letter.Id).Mastery == MasteryState.Mastered);
        var masteredShortWords = content.Words.Count(word => word.Stage == 2
            && Get(word.Id).Mastery == MasteryState.Mastered);
        var masteredLongerWords = content.Words.Count(word => word.Stage == 3
            && Get(word.Id).Mastery == MasteryState.Mastered);
        if (UnlockedStage == 1 && masteredLetters >= content.Stages[1].MasteredRequired)
            UnlockedStage = 2;
        if (UnlockedStage == 2 && masteredShortWords >= content.Stages[2].MasteredRequired)
            UnlockedStage = 3;
        if (UnlockedStage == 3 && content.Words.Any(word => word.Stage == 4)
            && masteredLongerWords >= content.Stages[3].MasteredRequired)
            UnlockedStage = 4;
        AddAchievement("first_mission");
        if (Items.Values.Count(item => item.Mastery == MasteryState.Mastered) >= 5)
            AddAchievement("five_mastered");
    }

    public void ResetLearning()
    {
        Items = new Dictionary<string, LearningItemProgress>(StringComparer.Ordinal);
        UnlockedStage = 1;
        CompletedSessions = 0;
        AssistedAnswers = 0;
        Achievements = new List<string>();
    }

    private void AddAchievement(string id)
    {
        Achievements ??= new List<string>();
        if (!Achievements.Contains(id)) Achievements.Add(id);
    }
}
