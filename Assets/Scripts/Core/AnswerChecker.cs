using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Cosecha
{
    public enum AnswerResult { Wrong, Correct, CorrectIgnoringAccents }

    public struct AnswerCheck
    {
        public AnswerResult Result;
        /// <summary>The solution that matched (correct spelling), or the main solution when wrong.</summary>
        public string Solution;
        public bool IsCorrect => Result != AnswerResult.Wrong;
    }

    public class AnswerChecker
    {
        static readonly string[] GermanArticles = { "der", "die", "das", "ein", "eine", "einen" };
        static readonly string[] SpanishArticles = { "el", "la", "los", "las", "un", "una" };
        static readonly char[] StripChars = { '¿', '?', '¡', '!', '.', ',', '\'', '\u2019' };   // ' and typographic ’
        static readonly Regex Whitespace = new Regex(@"\s+");

        readonly AccentMode mode;

        public AnswerChecker(AccentMode mode) { this.mode = mode; }
        public AnswerChecker(BalanceConfig cfg) : this(cfg.accentMode) { }

        /// <summary>Trim, strip punctuation, collapse whitespace, lower-case.</summary>
        public static string Normalize(string s)
        {
            if (s == null) return string.Empty;
            s = s.Normalize(NormalizationForm.FormC);
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
                if (System.Array.IndexOf(StripChars, c) < 0) sb.Append(c);
            s = Whitespace.Replace(sb.ToString(), " ").Trim();
            return s.ToLower(CultureInfo.InvariantCulture);
        }

        public static string StripLeadingArticle(string normalized, IEnumerable<string> articles)
        {
            foreach (var a in articles)
                if (normalized.Length > a.Length + 1 && normalized.StartsWith(a + " "))
                    return normalized.Substring(a.Length + 1);
            return normalized;
        }

        /// <summary>Removes diacritics (á→a, ñ→n, ü→u ...).</summary>
        public static string RemoveAccents(string s)
        {
            var d = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(d.Length);
            foreach (char c in d)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        AnswerResult Compare(string input, string solution)
        {
            if (input.Length == 0) return AnswerResult.Wrong;
            if (input == solution) return AnswerResult.Correct;
            if (mode == AccentMode.Lenient && RemoveAccents(input) == RemoveAccents(solution))
                return AnswerResult.CorrectIgnoringAccents;
            return AnswerResult.Wrong;
        }

        AnswerCheck CheckAgainst(string input, IList<string> solutions, string[] articles)
        {
            string a = Normalize(input);
            if (articles != null) a = StripLeadingArticle(a, articles);

            var best = new AnswerCheck { Result = AnswerResult.Wrong, Solution = solutions.Count > 0 ? solutions[0] : "" };
            foreach (var sol in solutions)
            {
                string b = Normalize(sol);
                if (articles != null) b = StripLeadingArticle(b, articles);
                var r = Compare(a, b);
                if (r == AnswerResult.Correct) return new AnswerCheck { Result = r, Solution = sol };
                if (r == AnswerResult.CorrectIgnoringAccents && best.Result == AnswerResult.Wrong)
                    best = new AnswerCheck { Result = r, Solution = sol };
            }
            return best;
        }

        /// <summary>German answer: any entry of the "de" array counts, leading article ignored.</summary>
        public AnswerCheck CheckGerman(string input, IList<string> solutions) =>
            CheckAgainst(input, solutions, GermanArticles);

        /// <summary>Spanish word answer: leading article ignored ("la casa" == "casa").</summary>
        public AnswerCheck CheckSpanish(string input, string solution) =>
            CheckAgainst(input, new[] { solution }, SpanishArticles);

        /// <summary>Exact token answer (articles, verb forms): no article stripping.</summary>
        public AnswerCheck CheckExact(string input, string solution) =>
            CheckAgainst(input, new[] { solution }, null);

        /// <summary>For the joker: does the input match this Spanish lemma?</summary>
        public bool MatchesSpanish(string input, string solution) => CheckSpanish(input, solution).IsCorrect;
    }
}
