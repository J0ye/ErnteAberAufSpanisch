using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cosecha
{
    /// <summary>Word types. The numeric values are serialized in BalanceConfig; append new types at the end.</summary>
    public enum WordType { Noun, Verb, Adjective, Other, Phrase, Pronoun, Article, Conjunction, QuestionWord }

    public static class WordTypes
    {
        /// <summary>Words that never join sentence structures ("other" and "phrase").</summary>
        public static bool IsFlat(this WordType t) => t == WordType.Other || t == WordType.Phrase;

        /// <summary>Function words: size 1, fixed stats, may be owned several times, no complexity gate.</summary>
        public static bool IsFunctionWord(this WordType t) =>
            t == WordType.Pronoun || t == WordType.Article || t == WordType.Conjunction || t == WordType.QuestionWord;

        public static readonly WordType[] All = (WordType[])Enum.GetValues(typeof(WordType));
    }

    public class NounInfo
    {
        public string gender;
        public string number = "sg";
        public string articleDefinite;
        public string articleIndefinite;
    }

    public class PresentForms
    {
        public string yo, tu, el, nosotros, vosotros, ellos;

        public string[] ToArray() => new[] { yo, tu, el, nosotros, vosotros, ellos };
    }

    public class VerbInfo
    {
        public bool irregular;
        public bool transitive;
        public bool copula;
        public PresentForms present;
    }

    public class AdjectiveForms
    {
        public string msg, fsg, mpl, fpl;

        public string[] ToArray() => new[] { msg, fsg, mpl, fpl };
    }

    public class AdjectiveInfo
    {
        public AdjectiveForms forms;
    }

    public class PronounInfo
    {
        public int person;
        public string number;
        public string gender;
    }

    public class ArticleInfo
    {
        public bool definite;
        public string gender;
        public string number;
    }

    public class QuestionWordInfo
    {
        public string role;
    }

    /// <summary>Raw JSON entry (schema version 2).</summary>
    public class VocabEntryJson
    {
        public string id;
        public string es;
        public string[] de;
        public string type;
        public NounInfo noun;
        public VerbInfo verb;
        public AdjectiveInfo adjective;
        public PronounInfo pronoun;
        public ArticleInfo article;
        public QuestionWordInfo questionWord;
        public int complexityOverride;
        public string[] tags;
    }

    public class VocabularyFileJson
    {
        public int version;
        public List<JToken> entries;
    }

    /// <summary>Validated vocabulary entry.</summary>
    public class VocabEntry
    {
        public string Id;
        public string Es;
        public string[] De;
        public WordType Type;
        public NounInfo Noun;
        public VerbInfo Verb;
        public AdjectiveInfo Adjective;
        public PronounInfo Pronoun;
        public ArticleInfo Article;
        public QuestionWordInfo QuestionWord;
        public int ComplexityOverride;
        public string[] Tags;

        public bool IsIrregularVerb => Type == WordType.Verb && Verb != null && Verb.irregular;
        public string[] PresentForms => Verb?.present?.ToArray();
        public string[] AdjectiveForms => Adjective?.forms?.ToArray();
        public override string ToString() => $"{Es} ({Id})";
    }

    public class VocabularyDatabase
    {
        public readonly List<VocabEntry> Entries = new List<VocabEntry>();
        public readonly List<string> Warnings = new List<string>();
        /// <summary>Set when the whole file was rejected (e.g. wrong schema version).</summary>
        public string Error;
        readonly Dictionary<string, VocabEntry> byId = new Dictionary<string, VocabEntry>();

        public VocabEntry Get(string id) => id != null && byId.TryGetValue(id, out var e) ? e : null;

        public void Add(VocabEntry e)
        {
            Entries.Add(e);
            byId[e.Id] = e;
        }

        /// <summary>
        /// Dictionary lookup in both languages. Matches the Spanish word, every German translation, the id and all
        /// verb/adjective forms, ignoring case and accents. Exact matches come first, then prefix, then substring matches.
        /// </summary>
        public List<VocabEntry> Search(string query, int max = 40)
        {
            string q = Key(query);
            if (q.Length == 0) return new List<VocabEntry>();
            var hits = new List<(VocabEntry e, int rank)>();
            foreach (var e in Entries)
            {
                int best = int.MaxValue;
                foreach (var term in SearchTerms(e))
                {
                    string k = Key(term);
                    // also match without a leading German/Spanish article ("das Bier" → "bier")
                    int sp = k.IndexOf(' ');
                    string bare = sp > 0 && sp <= 3 ? k.Substring(sp + 1) : null;
                    int r = k == q || bare == q ? 0 : k.StartsWith(q) || (bare != null && bare.StartsWith(q)) ? 1 : k.Contains(q) ? 2 : int.MaxValue;
                    if (r < best) best = r;
                }
                if (best != int.MaxValue) hits.Add((e, best));
            }
            return hits.OrderBy(h => h.rank).ThenBy(h => h.e.Es.Length).ThenBy(h => h.e.Es, StringComparer.Ordinal)
                .Take(max).Select(h => h.e).ToList();
        }

        static string Key(string s) => AnswerChecker.RemoveAccents(AnswerChecker.Normalize(s));

        static IEnumerable<string> SearchTerms(VocabEntry e)
        {
            yield return e.Es;
            yield return e.Id.Replace('_', ' ');
            foreach (var d in e.De) yield return d;
            if (e.PresentForms != null) foreach (var f in e.PresentForms) yield return f;
            if (e.AdjectiveForms != null) foreach (var f in e.AdjectiveForms) yield return f;
        }
    }

    /// <summary>Parses and validates the vocabulary JSON (schema 2 only). Invalid entries are skipped with a warning.</summary>
    public static class VocabularyLoader
    {
        public const int SchemaVersion = 2;
        public static readonly string[] PersonKeys = { "yo", "tu", "el", "nosotros", "vosotros", "ellos" };
        public static readonly string[] AdjectiveKeys = { "msg", "fsg", "mpl", "fpl" };

        /// <param name="fileName">Only used in error messages.</param>
        public static VocabularyDatabase Parse(string json, string fileName = "vocabulary.json")
        {
            var db = new VocabularyDatabase();
            VocabularyFileJson file;
            try
            {
                file = JsonConvert.DeserializeObject<VocabularyFileJson>(json);
            }
            catch (Exception ex)
            {
                db.Error = $"Vokabeldatei '{fileName}' konnte nicht gelesen werden: {ex.Message}";
                db.Warnings.Add(db.Error);
                return db;
            }
            if (file == null || file.entries == null)
            {
                db.Error = $"Vokabeldatei '{fileName}' hat kein 'entries'-Array.";
                db.Warnings.Add(db.Error);
                return db;
            }
            if (file.version != SchemaVersion)
            {
                db.Error = $"Vokabeldatei '{fileName}' hat Schemaversion {file.version}, erwartet wird {SchemaVersion}. Datei wird nicht geladen.";
                db.Warnings.Add(db.Error);
                return db;
            }

            for (int i = 0; i < file.entries.Count; i++)
            {
                VocabEntryJson raw;
                try { raw = file.entries[i].ToObject<VocabEntryJson>(); }
                catch (Exception ex)
                {
                    db.Warnings.Add($"Eintrag #{i}: ungültiges Format ({ex.Message}) – übersprungen.");
                    continue;
                }
                string label = raw?.id ?? raw?.es ?? $"#{i}";
                string problem = Validate(raw, db, out var entry);
                if (problem != null)
                {
                    db.Warnings.Add($"Eintrag '{label}': {problem} – übersprungen.");
                    continue;
                }
                db.Add(entry);
            }
            return db;
        }

        static bool IsGender(string g) => g == "m" || g == "f";
        static bool IsNumber(string n) => n == "sg" || n == "pl";
        static string N(string s) => s?.Trim().Normalize(NormalizationForm.FormC);

        public static bool TryParseType(string s, out WordType type)
        {
            switch (s)
            {
                case "noun": type = WordType.Noun; return true;
                case "verb": type = WordType.Verb; return true;
                case "adjective": type = WordType.Adjective; return true;
                case "other": type = WordType.Other; return true;
                case "phrase": type = WordType.Phrase; return true;
                case "pronoun": type = WordType.Pronoun; return true;
                case "article": type = WordType.Article; return true;
                case "conjunction": type = WordType.Conjunction; return true;
                case "questionWord": type = WordType.QuestionWord; return true;
                default: type = WordType.Other; return false;
            }
        }

        static string Validate(VocabEntryJson raw, VocabularyDatabase db, out VocabEntry entry)
        {
            entry = null;
            if (raw == null) return "leer";
            if (string.IsNullOrWhiteSpace(raw.id)) return "fehlende 'id'";
            if (!raw.id.All(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-'))
                return "'id' muss klein geschriebenes ASCII sein";
            if (db.Get(raw.id) != null) return "doppelte 'id'";
            if (string.IsNullOrWhiteSpace(raw.es)) return "fehlendes 'es'";
            if (raw.de == null || raw.de.Length == 0 || raw.de.Any(string.IsNullOrWhiteSpace)) return "'de' fehlt oder enthält leere Einträge";
            if (!TryParseType(raw.type, out var type)) return $"unbekannter 'type' '{raw.type}'";
            string es = N(raw.es);

            switch (type)
            {
                case WordType.Noun:
                    if (raw.noun == null) return "Nomen ohne 'noun'-Objekt";
                    if (!IsGender(raw.noun.gender)) return "'noun.gender' muss 'm' oder 'f' sein";
                    if (!IsNumber(raw.noun.number)) return "'noun.number' muss 'sg' oder 'pl' sein";
                    if (string.IsNullOrWhiteSpace(raw.noun.articleDefinite) || string.IsNullOrWhiteSpace(raw.noun.articleIndefinite))
                        return "Nomen ohne Artikel";
                    raw.noun.articleDefinite = N(raw.noun.articleDefinite);
                    raw.noun.articleIndefinite = N(raw.noun.articleIndefinite);
                    break;
                case WordType.Verb:
                    if (raw.verb == null || raw.verb.present == null) return "Verb ohne 'verb.present'";
                    var forms = raw.verb.present.ToArray();
                    for (int i = 0; i < forms.Length; i++)
                        if (string.IsNullOrWhiteSpace(forms[i])) return $"Verbform '{PersonKeys[i]}' fehlt";
                    var p = raw.verb.present;
                    p.yo = N(p.yo); p.tu = N(p.tu); p.el = N(p.el); p.nosotros = N(p.nosotros); p.vosotros = N(p.vosotros); p.ellos = N(p.ellos);
                    break;
                case WordType.Adjective:
                    if (raw.adjective?.forms == null) return "Adjektiv ohne 'adjective.forms'";
                    var af = raw.adjective.forms;
                    var arr = af.ToArray();
                    for (int i = 0; i < arr.Length; i++)
                        if (string.IsNullOrWhiteSpace(arr[i])) return $"Adjektivform '{AdjectiveKeys[i]}' fehlt";
                    af.msg = N(af.msg); af.fsg = N(af.fsg); af.mpl = N(af.mpl); af.fpl = N(af.fpl);
                    if (af.msg != es) return "'adjective.forms.msg' muss 'es' entsprechen";
                    break;
                case WordType.Pronoun:
                    if (raw.pronoun == null) return "Pronomen ohne 'pronoun'-Objekt";
                    if (raw.pronoun.person < 1 || raw.pronoun.person > 3) return "'pronoun.person' muss 1–3 sein";
                    if (!IsNumber(raw.pronoun.number)) return "'pronoun.number' muss 'sg' oder 'pl' sein";
                    if (!IsGender(raw.pronoun.gender) && raw.pronoun.gender != "x") return "'pronoun.gender' muss 'm', 'f' oder 'x' sein";
                    break;
                case WordType.Article:
                    if (raw.article == null) return "Artikel ohne 'article'-Objekt";
                    if (!IsGender(raw.article.gender)) return "'article.gender' muss 'm' oder 'f' sein";
                    if (!IsNumber(raw.article.number)) return "'article.number' muss 'sg' oder 'pl' sein";
                    break;
                case WordType.QuestionWord:
                    if (raw.questionWord == null) return "Fragewort ohne 'questionWord'-Objekt";
                    if (raw.questionWord.role != "subject" && raw.questionWord.role != "object" && raw.questionWord.role != "adverb")
                        return "'questionWord.role' muss 'subject', 'object' oder 'adverb' sein";
                    break;
            }

            entry = new VocabEntry
            {
                Id = raw.id,
                Es = es,
                De = raw.de.Select(N).ToArray(),
                Type = type,
                Noun = type == WordType.Noun ? raw.noun : null,
                Verb = type == WordType.Verb ? raw.verb : null,
                Adjective = type == WordType.Adjective ? raw.adjective : null,
                Pronoun = type == WordType.Pronoun ? raw.pronoun : null,
                Article = type == WordType.Article ? raw.article : null,
                QuestionWord = type == WordType.QuestionWord ? raw.questionWord : null,
                ComplexityOverride = raw.complexityOverride,
                Tags = raw.tags ?? Array.Empty<string>()
            };
            return null;
        }
    }
}
