using System.Globalization;
using System.Linq;
using System.Text;

namespace Cosecha
{
    /// <summary>All German UI strings in one place (for later localization).</summary>
    public static class UiText
    {
        static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
        public static string F1(double v) => v.ToString("0.0", De);
        public static string F2(double v) => v.ToString("0.##", De);

        // Menu
        public const string Title = "Cosecha";
        public const string Subtitle = "Spanisch lernen auf dem Feld";
        public const string NewGame = "Neues Spiel";
        public const string Quit = "Beenden";
        public const string LeaderboardTitle = "Bestenliste";
        public const string LeaderboardEmpty = "Noch keine Läufe.";
        public static string LeaderboardRow(int i, LeaderboardEntry e) =>
            $"{i,2}. {F1(e.rank),7} Pkt · Runde {e.roundReached} · {e.wordsPlanted} Wörter · {e.modsLearned} Grammatik · {FormatDate(e.date)}{(e.practice ? " · Übung" : "")}";
        public const string ExamModeToggle = "Prüfungsmodus (keine Lösungshilfe)";
        public const string ShowAnswer = "Lösung zeigen";
        public const string HideAnswer = "Lösung verbergen";
        public static string JokerExample(VocabEntry e) => $"Zum Beispiel: {e.Es} ({e.De[0]})";
        public const string JokerNoExample = "Kein passendes Wort mehr frei.";
        public const string NoVocabulary = "Keine gültigen Vokabeln geladen.";
        static string FormatDate(string iso) =>
            System.DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d) ? d.ToString("dd.MM.yy", De) : iso;

        // Game over
        public const string GameOverTitle = "Missernte!";
        public const string BackToMenu = "Zum Hauptmenü";
        public static string GameOverStats(double rank, int round, int words, int grammar) =>
            $"Rang (Gesamtpunkte): {F1(rank)}\nErreichte Runde: {round}\nGepflanzte Wörter: {words}\nGrammatik gelernt: {grammar}";

        // HUD
        public static string HudRound(int r) => $"Runde {r}";
        public static string HudScore(double score, double target) => $"Ernte: {F1(score)} / {F1(target)}";
        public static string HudGold(int g) => $"Gold: {g}";
        public static string HudGrammar(int left) => $"Grammatik: {left}";
        public static string HudIncome(int g) => $"Einkommen: +{g}";
        public static string HudRank(double r) => $"Rang: {F1(r)}";
        public static string HudScarecrow(bool available) => available ? "Vogelscheuche: bereit" : "Vogelscheuche: verbraucht";
        public const string Harvest = "Ernten";
        public static string GrammarButton(int left) => $"Grammatik ({left})";
        public const string GrammarButtonTip = "Freie Grammatik-Aktion: ein Verb auf dem Feld konjugieren oder ein Adjektiv anpassen. " +
                                               "Auch per Rechtsklick auf das Wort.";
        public const string FieldGrows = "Das Feld wächst!";

        // Shop
        public const string ShopTitle = "Laden";
        public static string Reroll(int price) => $"Neu würfeln · {price} G";
        public static string Price(int price) => $"{price} G";
        public static string Buy(int price) => $"Kaufen ({price} G)";
        public const string NoMoreWords = "Keine Wörter mehr";
        public const string Sold = "Verkauft";
        public const string Missed = "Verpasst";
        public const string NotEnoughGold = "Nicht genug Gold.";
        public const string Busy = "Erst das gehaltene Wort ablegen.";
        public const string CompletesShort = "passt zum Satz";
        public const string CompletesHint = "Passt zu einem fast fertigen Satz auf dem Feld.";
        public static string WordCardTip(WordOffer o) =>
            $"{TypeName(o.Entry.Type)} · Größe {o.Stats.BaseSize}" +
            (o.Entry.Type == WordType.Verb ? " (Infinitiv)" : "") +
            $"\n{o.Stats.BasePoints} Pkt · {o.Stats.Gold} Gold" +
            (o.NoQuiz ? "\nArtikel: ohne Abfrage" : "");
        public static string DirectionHint(bool esToDe) => esToDe ? "ES → DE" : "DE → ES";
        public const string JokerTitle = "Joker";
        public static string JokerCard(WordType t, float discount) => $"Beliebiges {TypeName(t)}\nPreis: {discount * 100:0} %";
        public const string JokerEnter = "Eingeben";
        public const string JokerUsed = "Verbraucht";

        public static string TypeName(WordType t)
        {
            switch (t)
            {
                case WordType.Noun: return "Nomen";
                case WordType.Verb: return "Verb";
                case WordType.Adjective: return "Adjektiv";
                case WordType.Phrase: return "Redewendung";
                case WordType.Pronoun: return "Pronomen";
                case WordType.Article: return "Artikel";
                case WordType.Conjunction: return "Konjunktion";
                case WordType.QuestionWord: return "Fragewort";
                default: return "Sonstiges";
            }
        }

        // Structures
        public static string CategoryName(StructureCategory c) =>
            c == StructureCategory.Frage ? "Frage" : c == StructureCategory.Satz ? "Satz" : "Satzteil";

        /// <summary>Label on the twine: „Satzteil +0,5“, „Satz! +1,5“, „¿Frage? +2“.</summary>
        public static string StructureLabel(StructureCategory c, double bonus)
        {
            switch (c)
            {
                case StructureCategory.Frage: return $"¿Frage? +{F2(bonus)}";
                case StructureCategory.Satz: return $"Satz! +{F2(bonus)}";
                default: return $"Satzteil +{F2(bonus)}";
            }
        }

        public static string DirectionArrow(LineDirection d)
        {
            switch (d)
            {
                case LineDirection.Down: return "↓";
                case LineDirection.DownRight: return "↘";
                case LineDirection.UpRight: return "↗";
                default: return "→";
            }
        }

        public static string StructureLine(Structure s) =>
            $"{DirectionArrow(s.Direction)} {CategoryName(s.Category)} +{F2(s.Bonus)}: {string.Join(" ", s.Words.Select(w => w.Text))}";

        // Magnifier ("Lupe"): explains which structures a word is part of
        public const string MagnifierButton = "Lupe";
        public const string MagnifierPutDown = "Weglegen";
        public const string MagnifierTip = "Noch kein Satz auf dem Feld.";
        public const string MagnifierActiveTip = "Fahre mit der Lupe über ein Wort: zeigt die erkannten Sätze und ihre Übersetzung.";
        public const string MagnifierHint = "Fahre über ein Wort · Rechtsklick/Esc: Lupe weglegen";
        public const string MagnifierNoStructure = "Teil keines Satzes – kein Bonus.";

        /// <summary>German meaning of a word as shown on the field: „essen [yo]“, „rot [f. Sg.]“, „Brot“.</summary>
        public static string GlossWord(WordInstance w)
        {
            string de = string.IsNullOrWhiteSpace(w.TypedGerman) ? w.Entry.De[0] : w.TypedGerman.Trim();
            if (w.IsConjugated) return $"{de} [{Persons[w.VerbPerson]}]";
            if (w.IsInflected) return $"{de} [{AdjectiveFormNames[w.AdjectiveForm]}]";
            return de;
        }

        /// <summary>The recognized Spanish words: „Yo como el pan.“, „¿Qué comes?“, „el pan rico“.</summary>
        public static string SentenceText(Structure s)
        {
            string text = string.Join(" ", s.Words.Select(w => w.Text));
            if (s.Category == StructureCategory.Satzteil || text.Length == 0) return text;
            text = char.ToUpper(text[0], De) + text.Substring(1);
            return s.Category == StructureCategory.Frage ? $"¿{text}?" : text + ".";
        }

        /// <summary>Word-by-word German gloss of a structure; <paramref name="byId"/> maps token ids to field words.</summary>
        public static string SentenceGloss(Structure s, System.Func<int, WordInstance> byId) =>
            string.Join(" · ", s.Words.Select(t => byId(t.Id) is WordInstance w ? GlossWord(w) : t.Text));

        /// <summary>Hover box of the magnifier: every structure the word is part of, with gloss and the resulting score.</summary>
        public static string MagnifierTooltip(WordInstance hovered, System.Collections.Generic.IList<Structure> structures,
            System.Func<int, WordInstance> byId, ScoreBreakdown score)
        {
            var sb = new StringBuilder();
            sb.Append($"<b>{hovered.Shown}</b> – {GlossWord(hovered)}\n");
            if (structures == null || structures.Count == 0)
            {
                sb.Append(MagnifierNoStructure);
                return sb.ToString();
            }
            sb.Append(structures.Count == 1 ? "Teil von 1 Struktur:\n" : $"Teil von {structures.Count} Strukturen:\n");
            foreach (var s in structures)
            {
                sb.Append($"<color=#7A5A20><b>{DirectionArrow(s.Direction)} {StructureLabel(s.Category, s.Bonus)}</b></color>\n");
                sb.Append($"   <b>{SentenceText(s)}</b>\n");
                sb.Append($"   <i>{SentenceGloss(s, byId)}</i>\n");
            }
            sb.Append($"Bonus +{F2(score.Bonus)} → {score.BasePoints} × (1 + {F2(score.Bonus)}) = {F1(score.Total)} Pkt");
            return sb.ToString();
        }

        // Dictionary search (practice mode only)
        public const string SearchButton = "Wörterbuch";
        public const string SearchTitle = "Wörterbuch";
        public const string SearchPlaceholder = "Deutsch oder Spanisch …";
        public const string SearchEmptyQuery = "Gib ein deutsches oder spanisches Wort ein.";
        public static string SearchNoResults(string q) => $"Kein Eintrag für „{q}“ gefunden.";
        public static string SearchResultCount(int n, int max) => n >= max ? $"Die ersten {n} Treffer:" : n == 1 ? "1 Treffer:" : $"{n} Treffer:";

        static string GenderName(string g) => g == "m" ? "maskulin" : g == "f" ? "feminin" : g == "x" ? "beide" : g;
        static string NumberName(string n) => n == "pl" ? "Plural" : n == "sg" ? "Singular" : n;
        static string RoleName(string r) => r == "subject" ? "Subjekt" : r == "object" ? "Objekt" : r == "adverb" ? "Adverbial" : r;

        /// <summary>Every field of a vocabulary entry, formatted for the dictionary panel.</summary>
        public static string VocabDetails(VocabEntry e)
        {
            var sb = new StringBuilder();
            string head = e.Noun != null ? $"{e.Noun.articleDefinite} {e.Es}" : e.Es;
            sb.Append($"<size=125%><b>{head}</b></size>  <color=#B07F10>{TypeName(e.Type)}</color>\n");
            sb.Append($"<b>Deutsch:</b> {string.Join(", ", e.De)}\n");
            if (e.Noun != null)
                sb.Append($"<b>Genus:</b> {GenderName(e.Noun.gender)} · <b>Numerus:</b> {NumberName(e.Noun.number)}\n" +
                          $"<b>Artikel:</b> {e.Noun.articleDefinite} / {e.Noun.articleIndefinite}\n");
            if (e.Verb != null)
            {
                sb.Append($"{(e.Verb.irregular ? "unregelmäßig" : "regelmäßig")} · {(e.Verb.transitive ? "transitiv" : "intransitiv")}" +
                          $"{(e.Verb.copula ? " · Kopula" : "")}\n<b>Präsens:</b>\n");
                var f = e.PresentForms;
                for (int i = 0; i < f.Length; i++) sb.Append($"   {Persons[i]}: <b>{f[i]}</b>\n");
            }
            if (e.Adjective != null)
            {
                var f = e.AdjectiveForms;
                sb.Append("<b>Formen:</b> ");
                sb.Append(string.Join(" · ", f.Select((x, i) => $"{AdjectiveFormNames[i]} <b>{x}</b>")));
                sb.Append('\n');
            }
            if (e.Pronoun != null)
                sb.Append($"<b>Person:</b> {e.Pronoun.person}. · <b>Numerus:</b> {NumberName(e.Pronoun.number)} · <b>Genus:</b> {GenderName(e.Pronoun.gender)}\n");
            if (e.Article != null)
                sb.Append($"{(e.Article.definite ? "bestimmt" : "unbestimmt")} · <b>Genus:</b> {GenderName(e.Article.gender)} · <b>Numerus:</b> {NumberName(e.Article.number)}\n");
            if (e.QuestionWord != null)
                sb.Append($"<b>Rolle:</b> {RoleName(e.QuestionWord.role)}\n");
            if (e.ComplexityOverride > 0) sb.Append($"<b>Komplexität (fest):</b> {e.ComplexityOverride}\n");
            if (e.Tags != null && e.Tags.Length > 0) sb.Append($"<b>Tags:</b> {string.Join(", ", e.Tags)}\n");
            sb.Append($"<color=#82705F><size=85%>ID: {e.Id}</size></color>");
            return sb.ToString();
        }

        // Quizzes
        public const string Submit = "Prüfen";
        public const string Continue = "Weiter";
        public const string Cancel = "Abbrechen";
        public const string Close = "Schließen";
        public static string PurchaseQuizTitle(bool esToDe) => esToDe ? "Übersetze ins Deutsche" : "Übersetze ins Spanische";
        public static string AttemptsLeft(int n) => n == 1 ? "Noch 1 Versuch." : $"Noch {n} Versuche.";
        public static string Wrong(int left) => $"Leider falsch. {AttemptsLeft(left)}";
        public static string Solution(string s) => $"Lösung: {s}";
        public static string AccentHint(string s) => $"Richtig! Korrekte Schreibweise: {s}";
        public const string Correct = "Richtig!";
        public const string PurchaseFailed = "Drei Fehlversuche – das Angebot verschwindet für diese Runde.";
        public static string BoughtWithoutQuiz(string es) => $"„{es}“ gekauft.";
        public static string JokerQuizTitle(WordType t) => $"Joker: {TypeName(t)}";
        public static string JokerPrompt(WordType t) => $"Tippe ein spanisches {TypeName(t)}, das du noch nicht besitzt.";
        public const string JokerEmpty = "Bitte ein Wort eingeben.";
        public const string JokerUnknown = "Dieses Wort steht nicht in der Vokabelliste.";
        public static string JokerWrongType(WordType t) => $"Das ist kein {TypeName(t)}.";
        public const string JokerOwned = "Dieses Wort besitzt du schon.";
        public const string JokerExhausted = "Keine Versuche mehr für diesen Joker.";
        public static string JokerConfirm(VocabEntry e, WordStats s, int price) =>
            $"{e.Es} – {e.De[0]}\n{TypeName(e.Type)} · K {s.K} · Größe {s.BaseSize} · {s.BasePoints} Pkt · {s.Gold} Gold\nJoker-Preis: {price} Gold";

        // Grammar action
        public static readonly string[] Persons = { "yo", "tú", "él/ella/usted", "nosotros/as", "vosotros/as", "ellos/ellas/ustedes" };
        public static readonly string[] AdjectiveFormNames = { "m. Sg.", "f. Sg.", "m. Pl.", "f. Pl." };
        public const string ConjugateTitle = "Verb konjugieren";
        public const string InflectTitle = "Adjektiv anpassen";
        public static string ConjugatePrompt(VocabEntry e) => $"„{e.Es}“ ({e.De[0]}) – wähle eine Person und tippe die Präsensform.";
        public static string InflectPrompt(VocabEntry e) => $"„{e.Es}“ ({e.De[0]}) – wähle eine Form und tippe sie.";
        public const string PickPersonFirst = "Erst eine Person wählen.";
        public const string PickFormFirst = "Erst eine Form wählen.";
        public static string GrammarSuccess(string form) => $"Richtig! Das Wort zeigt jetzt „{form}“.";
        public const string GrammarFailed = "Drei Fehlversuche – die Aktion ist verbraucht, das Wort bleibt unverändert.";
        public const string NoGrammarAction = "Keine freie Grammatik-Aktion mehr in dieser Runde.";
        public const string NoGrammarTarget = "Kein Verb oder Adjektiv auf dem Feld.";
        public const string GrammarTargetHint = "Klicke ein Verb oder Adjektiv auf dem Feld (Esc = abbrechen).";

        // Field / messages
        public const string HoldingHint = "Linksklick: pflanzen/tauschen (Artikel auf Nomen: verschmelzen) · Esc: Schuppen";
        public const string PlanningHint = "Rechtsklick auf Verb/Adjektiv: Grammatik";
        public const string ShedFull = "Der Schuppen ist voll – pflanze oder kompostiere das Wort.";
        public static string ArticleMerged(string article, string noun, int points, int gold) =>
            $"„{article}“ verschmilzt mit „{noun}“: {points} Pkt, +{gold} Gold pro Runde.";
        public static string Composted(string es, int refund) => $"„{es}“ kompostiert. +{refund} Gold.";
        public const string Wheelbarrow = "Schubkarre";
        public const string Shed = "Schuppen";
        public const string Compost = "Kompost";

        // Harvest
        public static string HarvestPassed(double score, int income) => $"Ernte geschafft: {F1(score)} Punkte! +{income} Gold.";
        public static string ScarecrowSaved(int income) => $"Die Vogelscheuche rettet die Ernte! +{income} Gold.";
        public static string ScarecrowVerb(string shown, string lemma) => $"„{shown}“ fällt zurück in den Infinitiv „{lemma}“.";
        public static string ScarecrowAdjective(string shown, string lemma) => $"„{shown}“ fällt zurück auf „{lemma}“.";
        public const string ScarecrowNoPenalty = "Nichts ging verloren.";
        public const string RegrowInPlace = "Das Wort wächst um ein Feld nach rechts.";
        public const string RegrowToShed = "Es passt nicht mehr und liegt jetzt im Schuppen.";
        public const string RegrowComposted = "Es passt nicht mehr, kein Platz im Schuppen: kompostiert.";

        // Debug
        public const string DebugTitle = "Debug (F1)";
        public const string DebugGold = "+50 Gold";
        public const string DebugRoundDown = "Runde −1";
        public const string DebugRoundUp = "Runde +1";
        public const string DebugPass = "Ernte: bestehen";
        public const string DebugFail = "Ernte: scheitern";
        public const string DebugRefill = "Laden auffüllen";
        public const string DebugReload = "Vokabeln neu laden";
        public const string DebugLabels = "Punkte-Labels an/aus";
        public const string DebugGrammar = "+1 Grammatik";
        public const string DebugStructuresTitle = "Strukturen";
        public const string DebugNoStructures = "(keine)";
        public static string DebugReloaded(int n, int warnings) => $"{n} Vokabeln geladen, {warnings} Warnungen.";

        // Tooltip
        static string Gender(string g) => g == "f" ? "f." : g == "m" ? "m." : "m./f.";
        static string Number(string n) => n == "pl" ? "Pl." : "Sg.";

        /// <summary>Type with features, e.g. „Nomen, f., Sg.“.</summary>
        public static string TypeWithFeatures(WordInstance w)
        {
            var e = w.Entry;
            string t = TypeName(e.Type);
            switch (e.Type)
            {
                case WordType.Noun: return $"{t}, {Gender(e.Noun.gender)}, {Number(e.Noun.number)}";
                case WordType.Pronoun: return $"{t}, {e.Pronoun.person}. Person, {Number(e.Pronoun.number)}" + (e.Pronoun.gender == "x" ? "" : $", {Gender(e.Pronoun.gender)}");
                case WordType.Article: return $"{t}, {(e.Article.definite ? "bestimmt" : "unbestimmt")}, {Gender(e.Article.gender)}, {Number(e.Article.number)}";
                case WordType.Adjective: return $"{t}, {AdjectiveFormNames[w.AdjectiveForm]}";
                case WordType.QuestionWord:
                    return $"{t}, " + (e.QuestionWord.role == "subject" ? "fragt nach dem Subjekt" : e.QuestionWord.role == "object" ? "fragt nach dem Objekt" : "Adverb");
                case WordType.Verb:
                    var parts = new System.Collections.Generic.List<string> { t };
                    if (e.Verb.copula) parts.Add("Kopula");
                    else parts.Add(e.Verb.transitive ? "transitiv" : "intransitiv");
                    if (e.Verb.irregular) parts.Add("unregelmäßig");
                    parts.Add(w.IsConjugated ? Persons[w.VerbPerson] : "Infinitiv");
                    return string.Join(", ", parts);
                default: return t;
            }
        }

        /// <summary>First tooltip line: „como – comer, yo“.</summary>
        public static string FormLine(WordInstance w)
        {
            if (w.IsConjugated) return $"<b>{w.Shown}</b> – {w.Entry.Es}, {Persons[w.VerbPerson]}";
            if (w.IsInflected) return $"<b>{w.Shown}</b> – {w.Entry.Es}, {AdjectiveFormNames[w.AdjectiveForm]}";
            return $"<b>{w.Shown}</b>";
        }

        public static string WordTooltip(WordInstance w, RunState run)
        {
            var sb = new StringBuilder();
            var e = w.Entry;
            string german = string.IsNullOrWhiteSpace(w.TypedGerman) ? string.Join(", ", e.De) : w.TypedGerman;
            sb.Append(FormLine(w)).Append('\n');
            sb.Append(german).Append('\n');
            sb.Append(TypeWithFeatures(w)).Append(" · Größe ").Append(w.Size).Append('\n');
            if (w.HasMergedArticle)
                sb.Append($"Mit „{w.MergedArticle.Entry.Es}“ verschmolzen: {w.Stats.BasePoints} + {w.MergedArticle.Stats.BasePoints} Pkt, ")
                  .Append($"{w.Stats.Gold} + {w.MergedArticle.Stats.Gold} Gold\n");

            if (w.Location == WordLocation.Grid)
            {
                var b = run.ScoreOf(w);
                foreach (var s in b.Structures)
                    sb.Append("<color=#7A5A20>").Append(StructureLine(s)).Append("</color>\n");
                if (e.Type.IsFlat()) sb.Append("Bildet keine Sätze.\n");
                sb.Append(b.Bonus > 0
                    ? $"{b.BasePoints} × (1 + {F2(b.Bonus)}) = {F1(b.Total)}"
                    : $"{b.BasePoints} × 1 = {F1(b.Total)}");
                sb.Append('\n').Append($"+{w.GoldYield} Gold pro Runde");
            }
            else
            {
                sb.Append(w.Location == WordLocation.Shed ? "Im Schuppen: zählt nicht." : "In der Schubkarre: zählt nicht.");
            }
            return sb.ToString();
        }
    }
}
