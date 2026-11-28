using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Cosecha
{
    public class LeaderboardEntry
    {
        public string date;
        public double rank;
        public int roundReached;
        public int wordsPlanted;
        /// <summary>Successful grammar actions (JSON name kept from the mod system for old leaderboards).</summary>
        public int modsLearned;
        public bool practice;
    }

    /// <summary>Local leaderboard in a JSON file. Keeps every run.</summary>
    public class Leaderboard
    {
        readonly string path;
        public List<LeaderboardEntry> Entries { get; private set; } = new List<LeaderboardEntry>();

        public Leaderboard(string path)
        {
            this.path = path;
            Load();
        }

        public void Load()
        {
            try
            {
                Entries = File.Exists(path)
                    ? JsonConvert.DeserializeObject<List<LeaderboardEntry>>(File.ReadAllText(path)) ?? new List<LeaderboardEntry>()
                    : new List<LeaderboardEntry>();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"Leaderboard konnte nicht gelesen werden: {ex.Message}");
                Entries = new List<LeaderboardEntry>();
            }
        }

        public void Add(LeaderboardEntry e)
        {
            Entries.Add(e);
            try { File.WriteAllText(path, JsonConvert.SerializeObject(Entries, Formatting.Indented)); }
            catch (Exception ex) { UnityEngine.Debug.LogWarning($"Leaderboard konnte nicht gespeichert werden: {ex.Message}"); }
        }

        public List<LeaderboardEntry> Top(int n) => Entries.OrderByDescending(e => e.rank).Take(n).ToList();
    }

    /// <summary>Appends one CSV line per round for balancing analysis.</summary>
    public class RunLogger
    {
        const string Header = "timestamp,runId,round,target,score,goldBefore,goldAfter,wordsBought,rerolls,grammarActions,quizAttempts,quizFailures,result,satzteile,saetze,fragen";
        readonly string path;

        public RunLogger(string path) { this.path = path; }

        public void LogRound(RunState run, HarvestResult res, RoundStats stats)
        {
            try
            {
                // a log with an older column layout is moved aside instead of mixing layouts
                if (File.Exists(path))
                {
                    string first;
                    using (var sr = new StreamReader(path)) first = sr.ReadLine();
                    if (first != Header)
                    {
                        string old = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + "_old_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".csv");
                        File.Move(path, old);
                    }
                }
                bool newFile = !File.Exists(path);
                var c = CultureInfo.InvariantCulture;
                string result = res.Passed ? "pass" : res.ScarecrowSaved ? "scarecrow" : "fail";
                string line = string.Join(",",
                    DateTime.Now.ToString("s", c), run.RunId, res.Round.ToString(c),
                    res.Target.ToString("0.###", c), res.Score.ToString("0.###", c),
                    stats.GoldAtStart.ToString(c), run.Gold.ToString(c),
                    stats.WordsBought.ToString(c), stats.Rerolls.ToString(c), stats.GrammarActions.ToString(c),
                    stats.QuizAttempts.ToString(c), stats.QuizFailures.ToString(c), result,
                    res.Count(StructureCategory.Satzteil).ToString(c), res.Count(StructureCategory.Satz).ToString(c),
                    res.Count(StructureCategory.Frage).ToString(c));
                using (var sw = new StreamWriter(path, append: true))
                {
                    if (newFile) sw.WriteLine(Header);
                    sw.WriteLine(line);
                }
            }
            catch (Exception ex) { UnityEngine.Debug.LogWarning($"Runlog konnte nicht geschrieben werden: {ex.Message}"); }
        }
    }
}
