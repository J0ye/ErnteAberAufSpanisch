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
            $"{i,2}. {F1(e.rank),7} Pkt · Runde {e.roundReached} · {e.wordsPlanted} Wörter · {e.modsLearned} Mods · {FormatDate(e.date)}{(e.practice ? " · Übung" : "")}";
        public const string ExamModeToggle = "Prüfungsmodus (keine Lösungshilfe)";
        public const string ShowAnswer = "Lösung zeigen";
        public const string HideAnswer = "Lösung verbergen";
        public static string JokerExample(VocabEntry e) => $"Zum Beispiel: {e.Es} ({e.De[0]})";
        public const string JokerNoExample = "Kein passendes Wort mehr frei.";
        static string FormatDate(string iso) =>
            System.DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d) ? d.ToString("dd.MM.yy", De) : iso;

        // Game over
        public const string GameOverTitle = "Missernte!";
        public const string BackToMenu = "Zum Hauptmenü";
        public static string GameOverStats(double rank, int round, int words, int mods) =>
            $"Rang (Gesamtpunkte): {F1(rank)}\nErreichte Runde: {round}\nGepflanzte Wörter: {words}\nGelernte Mods: {mods}";

        // HUD
        public static string HudRound(int r) => $"Runde {r}";
        public static string HudScore(double score, double target) => $"Ernte: {F1(score)} / {F1(target)}";
        public static string HudGold(int g) => $"Gold: {g}";
        public static string HudActions(int left, int max) => $"Aktionen: {left}/{max}";
        public static string HudIncome(int g) => $"Einkommen: +{g}";
        public static string HudRank(double r) => $"Rang: {F1(r)}";
        public static string HudScarecrow(bool available) => available ? "Vogelscheuche: bereit" : "Vogelscheuche: verbraucht";
        public const string Harvest = "Ernten";

        // Shop
        public const string ShopTitle = "Laden";
        public static string Reroll(int price) => $"Neu würfeln · {price} G";
        public static string Price(int price) => $"{price} G";
        public static string Buy(int price) => $"Kaufen ({price} G)";
        public const string NoMoreWords = "Keine Wörter mehr";
        public const string Sold = "Verkauft";
        public const string Missed = "Verpasst";
        public const string NotEnoughGold = "Nicht genug Gold.";
        public const string NoActionLeft = "Keine Aktion mehr übrig.";
        public const string NoNounTarget = "Kein Nomen ohne Artikel-Mod auf dem Feld.";
        public const string NoVerbTarget = "Kein Verb mit fehlenden Formen auf dem Feld.";
        public const string FieldMaxed = "Das Feld hat schon die Maximalgröße.";
        public const string Busy = "Erst das gehaltene Wort ablegen.";
        public static string WordCardStats(WordType t, WordStats s) =>
            $"{TypeName(t)} · Größe {s.UnmoddedSize}\n{s.BasePoints} Pkt · {s.Gold} Gold";
        public static string DirectionHint(bool esToDe) => esToDe ? "ES → DE" : "DE → ES";
        public const string JokerTitle = "Joker";
        public static string JokerCard(WordType t, float discount) => $"Beliebiges {TypeName(t)}\nPreis: {discount * 100:0} %";
        public const string JokerEnter = "Eingeben";
        public const string JokerUsed = "Verbraucht";

        public static string OfferName(UpgradeOffer o)
        {
            switch (o.Kind)
            {
                case OfferKind.ArticleMod: return "Artikel-Mod";
                case OfferKind.ConjugationMod: return "Konjugations-Mod";
                case OfferKind.PlusMult: return $"+Mult {TypeName(o.Type)}";
                case OfferKind.XMult: return $"×Mult {TypeName(o.Type)}";
                case OfferKind.FieldExpansion: return "Felderweiterung";
                case OfferKind.ExtraAction: return "Extra-Aktion";
            }
            return "";
        }

        public static string OfferEffect(UpgradeOffer o, BalanceConfig c)
        {
            switch (o.Kind)
            {
                case OfferKind.ArticleMod: return $"Nomen: Artikel lernen\nPunkte ×{F2(c.articleMultiplier)}";
                case OfferKind.ConjugationMod: return "Verb: Präsens lernen\nWort schrumpft";
                case OfferKind.PlusMult: return $"{TypeNamePlural(o.Type)}: Mult +{F2(c.plusMultStep)}";
                case OfferKind.XMult: return $"{TypeNamePlural(o.Type)}: Mult ×{F2(c.xMultFactor)}";
                case OfferKind.FieldExpansion: return "+1 Spalte oder +1 Zeile";
                case OfferKind.ExtraAction: return "+1 Aktion pro Runde";
            }
            return "";
        }

        public static string TypeName(WordType t)
        {
            switch (t)
            {
                case WordType.Noun: return "Nomen";
                case WordType.Verb: return "Verb";
                case WordType.Adjective: return "Adjektiv";
                case WordType.Phrase: return "Redewendung";
                default: return "Sonstiges";
            }
        }

        public static string TypeNamePlural(WordType t)
        {
            switch (t)
            {
                case WordType.Noun: return "Nomen";
                case WordType.Verb: return "Verben";
                case WordType.Adjective: return "Adjektive";
                case WordType.Phrase: return "Redewendungen";
                default: return "Sonstige";
            }
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
        public static string JokerQuizTitle(WordType t) => $"Joker: {TypeName(t)}";
        public static string JokerPrompt(WordType t) => $"Tippe ein spanisches {TypeName(t)}, das du noch nicht besitzt.";
        public const string JokerEmpty = "Bitte ein Wort eingeben.";
        public const string JokerUnknown = "Dieses Wort steht nicht in der Vokabelliste.";
        public static string JokerWrongType(WordType t) => $"Das ist kein {TypeName(t)}.";
        public const string JokerOwned = "Dieses Wort besitzt du schon.";
        public const string JokerExhausted = "Keine Versuche mehr für diesen Joker.";
        public static string JokerConfirm(VocabEntry e, WordStats s, int price) =>
            $"{e.Es} – {e.De[0]}\n{TypeName(e.Type)} · K {s.K} · Größe {s.UnmoddedSize} · {s.BasePoints} Pkt · {s.Gold} Gold\nJoker-Preis: {price} Gold";
        public const string ArticleQuizTitle = "Artikel-Mod";
        public static string ArticlePrompt(VocabEntry e) => $"Welche Artikel hat „{e.Es}“ ({e.De[0]})?";
        public const string ArticleDefinite = "bestimmt";
        public const string ArticleIndefinite = "unbestimmt";
        public const string ConjugationQuizTitle = "Konjugations-Mod";
        public static string ConjugationPrompt(VocabEntry e) => $"Präsens von „{e.Es}“ ({e.De[0]})";
        public static readonly string[] Persons = { "yo", "tú", "él/ella/usted", "nosotros/as", "vosotros/as", "ellos/ellas/ustedes" };
        public static string ConjugationResult(int correct, int oldSize, int newSize) =>
            $"{correct} von 6 Formen richtig. Größe {oldSize} → {newSize}.";
        public const string ModFailed = "Mod verbraucht – leider nicht geschafft.";
        public static string ArticleSuccess(float mult) => $"Artikel gelernt! Punkte ×{F2(mult)}.";

        // Field / messages
        public const string TargetingHint = "Wähle ein Ziel auf dem Feld (Esc = abbrechen, Gold zurück).";
        public const string HoldingHint = "Linksklick: pflanzen/tauschen · R/Rechtsklick: drehen · Esc: Schuppen";
        public const string ShedFull = "Der Schuppen ist voll – pflanze oder kompostiere das Wort.";
        public const string WheelbarrowFull = "Die Schubkarre ist voll – pflanze oder kompostiere das Wort.";
        public static string Composted(string es, int refund) => $"„{es}“ kompostiert. +{refund} Gold.";
        public const string FieldChoiceTitle = "Felderweiterung";
        public const string AddColumn = "+1 Spalte";
        public const string AddRow = "+1 Zeile";
        public const string Wheelbarrow = "Schubkarre";
        public const string Shed = "Schuppen";
        public const string Compost = "Kompost";

        // Harvest
        public static string HarvestPassed(double score, int income) => $"Ernte geschafft: {F1(score)} Punkte! +{income} Gold.";
        public static string ScarecrowSaved(int income) => $"Die Vogelscheuche rettet die Ernte! +{income} Gold.";
        public static string ScarecrowVerb(string es) => $"„{es}“ verliert den Konjugations-Mod.";
        public static string ScarecrowNoun(string es) => $"„{es}“ verliert den Artikel-Mod.";
        public const string ScarecrowNoPenalty = "Kein Mod ging verloren.";
        public const string RegrowInPlace = "Das Wort wächst an Ort und Stelle nach.";
        public const string RegrowToWheelbarrow = "Es passt nicht mehr und liegt jetzt in der Schubkarre.";
        public const string RegrowToShed = "Es passt nicht mehr und liegt jetzt im Schuppen.";
        public const string RegrowComposted = "Es passt nicht mehr, kein Platz im Lager: kompostiert.";

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
        public static string DebugReloaded(int n, int warnings) => $"{n} Vokabeln geladen, {warnings} Warnungen.";

        // Tooltip
        public static string WordTooltip(WordInstance w, RunState run)
        {
            var sb = new StringBuilder();
            var e = w.Entry;
            string german = string.IsNullOrWhiteSpace(w.TypedGerman) ? e.De[0] : w.TypedGerman;
            sb.Append("<b>").Append(e.Es).Append("</b> – ").Append(german).Append('\n');
            sb.Append(TypeName(e.Type)).Append(" · K ").Append(w.Stats.K).Append(" · Größe ").Append(w.Size).Append('\n');

            if (w.HasArticleMod && e.Noun != null)
                sb.Append(e.Noun.gender == "f" ? "feminin" : "maskulin").Append(": ")
                  .Append(e.Noun.articleDefinite).Append(' ').Append(e.Es).Append(" · ")
                  .Append(e.Noun.articleIndefinite).Append(' ').Append(e.Es).Append('\n');

            if (w.HasConjugationMod && e.Verb?.present != null)
            {
                var forms = e.Verb.present.ToArray();
                for (int i = 0; i < 6; i++)
                {
                    string line = $"{Persons[i]}: {forms[i]}";
                    sb.Append(w.ConjugationCorrect[i] ? line : $"<color=#B06060>{line}</color>").Append('\n');
                }
            }

            if (w.Location == WordLocation.Grid)
            {
                var b = run.ScoreOf(w);
                if (b.Flat) sb.Append($"Basis {b.BasePoints} (flach) = {F1(b.Total)}");
                else sb.Append($"{b.BasePoints} × {F2(b.Synergy)} Syn. × {F2(b.Article)} Art. × {F2(b.TypeMult)} × {F2(b.TypeXMult)} Typ = {F1(b.Total)}");
                sb.Append('\n').Append($"+{w.Stats.Gold} Gold pro Runde");
            }
            else
            {
                sb.Append(w.Location == WordLocation.Shed ? "Im Schuppen: zählt nicht." : "In der Schubkarre: zählt nicht.");
            }
            return sb.ToString();
        }
    }
}
