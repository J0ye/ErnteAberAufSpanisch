using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cosecha
{
    public enum WordType { Noun, Verb, Adjective, Other, Phrase }

    public static class WordTypes
    {
        /// <summary>Flat-scoring types: no ring synergy, no shop multipliers, no mods ("other" and "phrase").</summary>
        public static bool IsFlat(this WordType t) => t == WordType.Other || t == WordType.Phrase;
    }

    public class NounInfo
    {
        public string gender;
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
        public PresentForms present;
    }

    /// <summary>Raw JSON entry (schema version 1).</summary>
    public class VocabEntryJson
    {
        public string id;
        public string es;
        public string[] de;
        public string type;
        public NounInfo noun;
        public VerbInfo verb;
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
        public int ComplexityOverride;
        public string[] Tags;

        public bool IsIrregularVerb => Type == WordType.Verb && Verb != null && Verb.irregular;
        public string[] PresentForms => Verb?.present?.ToArray();
        public override string ToString() => $"{Es} ({Id})";
    }

    public class VocabularyDatabase
    {
        public readonly List<VocabEntry> Entries = new List<VocabEntry>();
        public readonly List<string> Warnings = new List<string>();
        readonly Dictionary<string, VocabEntry> byId = new Dictionary<string, VocabEntry>();

        public VocabEntry Get(string id) => id != null && byId.TryGetValue(id, out var e) ? e : null;

        public void Add(VocabEntry e)
        {
            Entries.Add(e);
            byId[e.Id] = e;
        }
    }

    /// <summary>Parses and validates the vocabulary JSON. Invalid entries are skipped with a warning.</summary>
    public static class VocabularyLoader
    {
        public static readonly string[] PersonKeys = { "yo", "tu", "el", "nosotros", "vosotros", "ellos" };

        public static VocabularyDatabase Parse(string json)
        {
            var db = new VocabularyDatabase();
            VocabularyFileJson file;
            try
            {
                file = JsonConvert.DeserializeObject<VocabularyFileJson>(json);
            }
            catch (Exception ex)
            {
                db.Warnings.Add($"Vokabeldatei konnte nicht gelesen werden: {ex.Message}");
                return db;
            }
            if (file == null || file.entries == null)
            {
                db.Warnings.Add("Vokabeldatei hat kein 'entries'-Array.");
                return db;
            }
            if (file.version != 1)
                db.Warnings.Add($"Unerwartete Schemaversion {file.version} (erwartet 1). Lade trotzdem.");

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

            WordType type;
            switch (raw.type)
            {
                case "noun": type = WordType.Noun; break;
                case "verb": type = WordType.Verb; break;
                case "adjective": type = WordType.Adjective; break;
                case "other": type = WordType.Other; break;
                case "phrase": type = WordType.Phrase; break;
                default: return $"unbekannter 'type' '{raw.type}'";
            }

            if (type == WordType.Noun)
            {
                if (raw.noun == null) return "Nomen ohne 'noun'-Objekt";
                if (raw.noun.gender != "m" && raw.noun.gender != "f") return "'noun.gender' muss 'm' oder 'f' sein";
                if (string.IsNullOrWhiteSpace(raw.noun.articleDefinite) || string.IsNullOrWhiteSpace(raw.noun.articleIndefinite))
                    return "Nomen ohne Artikel";
            }
            if (type == WordType.Verb)
            {
                if (raw.verb == null || raw.verb.present == null) return "Verb ohne 'verb.present'";
                var forms = raw.verb.present.ToArray();
                for (int i = 0; i < forms.Length; i++)
                    if (string.IsNullOrWhiteSpace(forms[i])) return $"Verbform '{PersonKeys[i]}' fehlt";
            }

            entry = new VocabEntry
            {
                Id = raw.id,
                Es = raw.es.Trim().Normalize(NormalizationForm.FormC),
                De = raw.de.Select(d => d.Trim().Normalize(NormalizationForm.FormC)).ToArray(),
                Type = type,
                Noun = raw.noun,
                Verb = raw.verb,
                ComplexityOverride = raw.complexityOverride,
                Tags = raw.tags ?? Array.Empty<string>()
            };
            return null;
        }
    }
}
