using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Cosecha.Tests
{
    public class CoreTests
    {
        BalanceConfig cfg;
        ComplexityCalculator calc;

        [SetUp]
        public void SetUp()
        {
            cfg = ScriptableObject.CreateInstance<BalanceConfig>();
            calc = new ComplexityCalculator(cfg);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(cfg);

        static VocabularyDatabase LoadSample()
        {
            var db = VocabularyLoader.Parse(TestVocabulary.Json);
            Assert.IsEmpty(db.Warnings, string.Join("\n", db.Warnings));
            return db;
        }

        // ------------------------------------------------------------ complexity (section 5)

        [TestCase("dos", WordType.Other, false, 3, 4, 1, 4)]
        [TestCase("casa", WordType.Noun, false, 4, 4, 2, 4)]
        [TestCase("qué", WordType.Other, false, 5, 3, 3, 2)]
        [TestCase("comer", WordType.Verb, false, 5, 5, 3, 2)]
        [TestCase("grande", WordType.Adjective, false, 6, 3, 4, 2)]
        [TestCase("ser", WordType.Verb, true, 6, 5, 4, 2)]
        [TestCase("siempre", WordType.Other, false, 7, 3, 5, 2)]
        [TestCase("mañana", WordType.Other, false, 9, 2, 8, 1)]
        [TestCase("pequeño", WordType.Adjective, false, 10, 2, 10, 1)]
        [TestCase("información", WordType.Noun, false, 13, 1, 17, 0)]
        [TestCase("buenos días", WordType.Phrase, false, 12, 2, 14, 1)]
        [TestCase("¿qué tal?", WordType.Phrase, false, 8, 3, 6, 2)]
        public void ComplexityTable(string es, WordType type, bool irregular, int k, int size, int points, int gold)
        {
            var s = calc.Compute(es, type, irregular);
            Assert.AreEqual(k, s.K, "K");
            Assert.AreEqual(size, s.UnmoddedSize, "size");
            Assert.AreEqual(points, s.BasePoints, "base points");
            Assert.AreEqual(gold, s.Gold, "gold");
        }

        [Test]
        public void ComplexityIgnoresPunctuationAndSpaces()
        {
            Assert.AreEqual(3, ComplexityCalculator.CountLetters("¿dos?"));
            Assert.AreEqual(calc.ComputeK("qué", false), calc.ComputeK("¿Qué?", false));
            Assert.AreEqual(8, calc.ComputeK("por favor", false));
        }

        [Test]
        public void ComplexityOverrideWins() => Assert.AreEqual(11, calc.ComputeK("dos", false, 11));

        // ------------------------------------------------------------ prices (section 10)

        [TestCase("dos", WordType.Other, 15)]
        [TestCase("siempre", WordType.Other, 13)]
        [TestCase("mañana", WordType.Other, 13)]
        [TestCase("información", WordType.Noun, 19)]
        public void WordPrices(string es, WordType type, int price) =>
            Assert.AreEqual(price, calc.Compute(es, type, false).Price);

        [Test]
        public void JokerPriceIsCeilOfDiscount()
        {
            Assert.AreEqual(9, calc.JokerPrice(15));  // 9.0
            Assert.AreEqual(8, calc.JokerPrice(13));  // 7.8 -> 8
        }

        // ------------------------------------------------------------ shapes (section 6)

        [Test]
        public void ShapeNesting()
        {
            foreach (ShapeFamily f in System.Enum.GetValues(typeof(ShapeFamily)))
                for (int rot = 0; rot < 4; rot++)
                    for (int k = 1; k < Shapes.MaxSize; k++)
                    {
                        var small = Shapes.LocalCells(f, k, rot);
                        var big = Shapes.LocalCells(f, k + 1, rot);
                        Assert.IsTrue(small.All(big.Contains), $"{f} rot {rot}: size {k} not subset of {k + 1}");
                        Assert.AreEqual(k, small.Distinct().Count(), "cells must be distinct");
                    }
        }

        [Test]
        public void ShapesFitIn5x5AtSize6()
        {
            foreach (ShapeFamily f in System.Enum.GetValues(typeof(ShapeFamily)))
            {
                var cells = Shapes.LocalCells(f, 6, 0);
                Assert.IsTrue(cells.All(c => c.x >= 0 && c.x < 5 && c.y >= 0 && c.y < 5), f.ToString());
            }
        }

        [Test]
        public void ShapesContainAnchorAndAreConnected()
        {
            foreach (ShapeFamily f in System.Enum.GetValues(typeof(ShapeFamily)))
                for (int rot = 0; rot < 4; rot++)
                    for (int k = 1; k <= Shapes.MaxSize; k++)
                    {
                        var cells = Shapes.LocalCells(f, k, rot);
                        Assert.AreEqual(Vector2Int.zero, cells[0]);
                        var seen = new HashSet<Vector2Int> { cells[0] };
                        var queue = new Queue<Vector2Int>(seen);
                        while (queue.Count > 0)
                        {
                            var c = queue.Dequeue();
                            foreach (var n in new[] { c + Vector2Int.up, c + Vector2Int.down, c + Vector2Int.left, c + Vector2Int.right })
                                if (cells.Contains(n) && seen.Add(n)) queue.Enqueue(n);
                        }
                        Assert.AreEqual(k, seen.Count, $"{f} rot {rot} size {k} not connected");
                    }
        }

        // ------------------------------------------------------------ answer checking (section 7)

        [Test]
        public void GermanAnswers()
        {
            var ac = new AnswerChecker(AccentMode.Lenient);
            var sol = new[] { "Information", "Auskunft" };
            Assert.IsTrue(ac.CheckGerman("  die   Information ", sol).IsCorrect);
            Assert.IsTrue(ac.CheckGerman("eine Auskunft", sol).IsCorrect);
            Assert.AreEqual("Auskunft", ac.CheckGerman("auskunft!", sol).Solution);
            Assert.IsTrue(ac.CheckGerman("das Haus", new[] { "Haus" }).IsCorrect);
            Assert.IsTrue(ac.CheckGerman("einen Hund", new[] { "Hund" }).IsCorrect);
            Assert.IsFalse(ac.CheckGerman("Katze", new[] { "Hund" }).IsCorrect);
            Assert.IsFalse(ac.CheckGerman("", new[] { "Hund" }).IsCorrect);
        }

        [Test]
        public void SpanishAnswersStripArticlesAndPunctuation()
        {
            var ac = new AnswerChecker(AccentMode.Strict);
            Assert.AreEqual(AnswerResult.Correct, ac.CheckSpanish("la casa", "casa").Result);
            Assert.AreEqual(AnswerResult.Correct, ac.CheckSpanish("Casa", "casa").Result);
            Assert.AreEqual(AnswerResult.Correct, ac.CheckSpanish("el agua", "agua").Result);
            Assert.AreEqual(AnswerResult.Correct, ac.CheckSpanish("¿Qué?", "qué").Result);
            Assert.AreEqual(AnswerResult.Correct, ac.CheckSpanish("un perro.", "perro").Result);
            Assert.AreEqual(AnswerResult.Wrong, ac.CheckSpanish("gato", "perro").Result);
        }

        [Test]
        public void LenientAccents()
        {
            var ac = new AnswerChecker(AccentMode.Lenient);
            var r = ac.CheckSpanish("manana", "mañana");
            Assert.AreEqual(AnswerResult.CorrectIgnoringAccents, r.Result);
            Assert.AreEqual("mañana", r.Solution);
            Assert.AreEqual(AnswerResult.CorrectIgnoringAccents, ac.CheckSpanish("informacion", "información").Result);
            Assert.AreEqual(AnswerResult.CorrectIgnoringAccents, ac.CheckExact("comeis", "coméis").Result);
            Assert.AreEqual(AnswerResult.Correct, ac.CheckSpanish("mañana", "mañana").Result);
        }

        [Test]
        public void StrictAccents()
        {
            var ac = new AnswerChecker(AccentMode.Strict);
            Assert.AreEqual(AnswerResult.Wrong, ac.CheckSpanish("manana", "mañana").Result);
            Assert.AreEqual(AnswerResult.Wrong, ac.CheckSpanish("que", "qué").Result);
            Assert.AreEqual(AnswerResult.Correct, ac.CheckSpanish("Qué", "qué").Result);
        }

        [Test]
        public void ExactCheckDoesNotStripArticles()
        {
            var ac = new AnswerChecker(AccentMode.Strict);
            Assert.IsTrue(ac.CheckExact("La", "la").IsCorrect);
            Assert.IsFalse(ac.CheckExact("el", "la").IsCorrect);
        }

        [Test]
        public void ApostrophesAreIgnored()
        {
            var ac = new AnswerChecker(AccentMode.Strict);
            var sol = new[] { "wie geht's", "wie geht es dir" };
            Assert.IsTrue(ac.CheckGerman("Wie gehts?", sol).IsCorrect);
            Assert.IsTrue(ac.CheckGerman("wie geht\u2019s", sol).IsCorrect);
            Assert.IsTrue(ac.CheckGerman("wie geht's", sol).IsCorrect);
            Assert.IsTrue(new AnswerChecker(AccentMode.Lenient).CheckSpanish("que tal", "¿qué tal?").IsCorrect);
        }

        // ------------------------------------------------------------ phrases

        [Test]
        public void PhrasesScoreFlatLikeOther()
        {
            var sc = new ScoreCalculator(cfg);
            var db = VocabularyLoader.Parse(TestVocabulary.Json);
            var run = new RunState(cfg, db, new SystemRng(1));
            var up = new UpgradeState();
            up.PlusMult[0] = 3; up.XMult[1] = 2;
            var words = new List<WordInstance>
            {
                run.CreateWord(db.Get("casa"), ShapeFamily.Line, 0),
                run.CreateWord(db.Get("comer"), ShapeFamily.Line, 0),
                run.CreateWord(db.Get("grande"), ShapeFamily.Line, 0),
            };
            var b = sc.Score(WordType.Phrase, 14, true, words, up);
            Assert.IsTrue(b.Flat);
            Assert.AreEqual(14, b.Total, 1e-9);
            Assert.AreEqual(1.0, sc.Synergy(WordType.Phrase, words), 1e-9);
        }

        [Test]
        public void PhrasesGetNoModsAndCanBeJokered()
        {
            var db = VocabularyLoader.Parse(TestVocabulary.Json);
            var run = new RunState(cfg, db, new SystemRng(1));
            run.StartNewRun();
            var phrase = run.CreateWord(db.Get("buenos_dias"), ShapeFamily.Line, 0);
            Assert.IsTrue(run.PlaceIn(run.Grid, phrase, new Vector2Int(0, 6), 0));
            Assert.IsFalse(run.IsValidModTarget(OfferKind.ArticleMod, phrase));
            Assert.IsFalse(run.IsValidModTarget(OfferKind.ConjugationMod, phrase));

            var free = VocabularyLoader.Parse(TestVocabulary.Json);
            var run2 = new RunState(cfg, free, new SystemRng(1));
            run2.StartNewRun();
            var e = run2.ValidateJoker("que tal", WordType.Phrase, null, out var reason);
            Assert.IsNotNull(e, reason);
            Assert.AreEqual("que_tal", e.Id);
            Assert.IsNull(run2.ValidateJoker("buenos dias", WordType.Other, null, out reason));
        }

        [Test]
        public void JokerCanRequirePhrase()
        {
            var gen = new ShopGenerator(cfg, calc);
            var rng = new SystemRng(7);
            bool seen = false;
            for (int i = 0; i < 200 && !seen; i++) seen = gen.MakeJoker(rng).Type == WordType.Phrase;
            Assert.IsTrue(seen);
        }

        // ------------------------------------------------------------ scoring (section 9)

        [Test]
        public void TargetCurve()
        {
            var sc = new ScoreCalculator(cfg);
            Assert.AreEqual(8.0, sc.Target(1), 1e-9);
            Assert.AreEqual(59.6, sc.Target(10), 0.01);
        }

        [Test]
        public void StartingInventory()
        {
            var run = new RunState(cfg, LoadSample(), new SystemRng(1));
            run.StartNewRun();
            Assert.AreEqual(4, run.Grid.Words.Count, "all four start words on the grid");
            CollectionAssert.AreEquivalent(new[] { "comer", "casa", "grande", "dos" }, run.Grid.Words.Select(w => w.Entry.Id));
            Assert.AreEqual(10.9, run.RoundScorePreview, 1e-6);
            Assert.AreEqual(15, run.IncomePreview);
            Assert.AreEqual(5, run.Gold);
            Assert.AreEqual(2, run.ActionsLeft);
            Assert.IsTrue(run.ScarecrowAvailable);
        }

        [Test]
        public void StartingWordFallsBackToLowestK()
        {
            var db = LoadSample();
            var trimmed = new VocabularyDatabase();
            foreach (var e in db.Entries.Where(e => e.Id != "comer")) trimmed.Add(e);
            var run = new RunState(cfg, trimmed, new SystemRng(1));
            run.StartNewRun();
            // Lowest-K verbs: ser (6) and tener... hablar (6) – ties broken by id: hablar
            var verb = run.OwnedWords().Single(w => w.Type == WordType.Verb);
            Assert.AreEqual("hablar", verb.Entry.Id);
        }

        [Test]
        public void HarvestPassAddsRankAndIncome()
        {
            var run = new RunState(cfg, LoadSample(), new SystemRng(1));
            run.StartNewRun();
            var res = run.Harvest();
            Assert.IsTrue(res.Passed);
            Assert.AreEqual(20, run.Gold);
            Assert.AreEqual(10.9, run.Rank, 1e-6);
            run.AdvanceRound();
            Assert.AreEqual(2, run.Round);
            Assert.AreEqual(10.0, run.Target, 1e-9);
        }

        [Test]
        public void ScarecrowSavesOnceThenGameOver()
        {
            var run = new RunState(cfg, LoadSample(), new SystemRng(1));
            run.StartNewRun();
            var res = run.Harvest(ForcedOutcome.Fail);
            Assert.IsTrue(res.ScarecrowSaved);
            Assert.IsFalse(res.GameOver);
            Assert.IsFalse(run.ScarecrowAvailable);
            Assert.AreEqual(20, run.Gold);
            run.AdvanceRound();
            res = run.Harvest(ForcedOutcome.Fail);
            Assert.IsTrue(res.GameOver);
        }

        [Test]
        public void ConjugationShrinksAndScarecrowRegrows()
        {
            var run = new RunState(cfg, LoadSample(), new SystemRng(1));
            run.StartNewRun();
            var comer = run.Grid.Words.Single(w => w.Entry.Id == "comer");
            Assert.AreEqual(5, comer.Size);
            run.ApplyConjugationModResult(comer, new[] { true, true, true, true, true, true });
            Assert.AreEqual(2, comer.Size);
            Assert.AreEqual(6, run.ModsLearned);
            run.Harvest(ForcedOutcome.Fail);
            Assert.IsFalse(comer.HasConjugationMod);
            Assert.AreEqual(5, comer.Size);
            Assert.AreEqual(0, comer.CorrectForms);
        }

        [Test]
        public void SynergyRing()
        {
            var sc = new ScoreCalculator(cfg);
            var db = LoadSample();
            var run = new RunState(cfg, db, new SystemRng(1));
            var words = new List<WordInstance>
            {
                run.CreateWord(db.Get("grande"), ShapeFamily.Line, 0),
                run.CreateWord(db.Get("rojo"), ShapeFamily.Line, 0),
                run.CreateWord(db.Get("comer"), ShapeFamily.Line, 0),
            };
            Assert.AreEqual(1.2, sc.Synergy(WordType.Verb, words), 1e-9);
            Assert.AreEqual(1.1, sc.Synergy(WordType.Noun, words), 1e-9);
            Assert.AreEqual(1.0, sc.Synergy(WordType.Adjective, words), 1e-9);
            Assert.AreEqual(1.0, sc.Synergy(WordType.Other, words), 1e-9);
        }

        [Test]
        public void UpgradeMultipliersAndArticle()
        {
            var sc = new ScoreCalculator(cfg);
            var up = new UpgradeState();
            up.PlusMult[(int)WordType.Noun] = 2;
            up.XMult[(int)WordType.Noun] = 1;
            var b = sc.Score(WordType.Noun, 10, true, new List<WordInstance>(), up);
            Assert.AreEqual(10 * 1.5 * 2.0 * 1.5, b.Total, 1e-9);
            var other = sc.Score(WordType.Other, 10, false, new List<WordInstance>(), up);
            Assert.AreEqual(10, other.Total, 1e-9);
        }

        // ------------------------------------------------------------ shop

        [Test]
        public void ShopComplexityGateAndFill()
        {
            var db = LoadSample();
            var gen = new ShopGenerator(cfg, calc);
            Assert.AreEqual(6, gen.ComplexityGate(1));
            Assert.AreEqual(7, gen.ComplexityGate(2));
            for (int seed = 0; seed < 20; seed++)
            {
                var words = gen.DrawWords(db.Entries, new HashSet<string>(), 1, 3, new SystemRng(seed));
                Assert.AreEqual(3, words.Count);
                Assert.IsTrue(words.All(w => calc.ComputeK(w) <= 6));
            }
            // Exclude everything except two high-K words -> fill-up with remaining.
            var excluded = new HashSet<string>(db.Entries.Select(e => e.Id).Where(id => id != "informacion" && id != "pequeno"));
            var few = gen.DrawWords(db.Entries, excluded, 1, 3, new SystemRng(1));
            CollectionAssert.AreEqual(new[] { "pequeno", "informacion" }, few.Select(e => e.Id));
        }

        [Test]
        public void UpgradePrices()
        {
            var gen = new ShopGenerator(cfg, calc);
            var up = new UpgradeState();
            var plus = new UpgradeOffer { Kind = OfferKind.PlusMult, Type = WordType.Verb };
            Assert.AreEqual(10, gen.OfferPrice(plus, up));
            up.PlusMult[(int)WordType.Verb] = 2;
            Assert.AreEqual(26, gen.OfferPrice(plus, up));   // 25.6
            Assert.AreEqual(10, gen.OfferPrice(new UpgradeOffer { Kind = OfferKind.PlusMult, Type = WordType.Noun }, up));
            up.FieldExpansions = 1;
            Assert.AreEqual(30, gen.OfferPrice(new UpgradeOffer { Kind = OfferKind.FieldExpansion }, up));
        }

        [Test]
        public void CompostRefundsHalf()
        {
            var db = LoadSample();
            var run = new RunState(cfg, db, new SystemRng(1));
            run.StartNewRun();
            var w = run.CreateWord(db.Get("siempre"), ShapeFamily.Line, 13);
            Assert.AreEqual(6, run.Compost(w));
        }

        [Test]
        public void TooltipShowsArticlesFormsAndBreakdown()
        {
            var run = new RunState(cfg, LoadSample(), new SystemRng(1));
            run.StartNewRun();
            var casa = run.Grid.Words.Single(w => w.Entry.Id == "casa");
            run.ApplyArticleModResult(casa, true);
            var tip = UiText.WordTooltip(casa, run);
            StringAssert.Contains("la casa · una casa", tip);
            StringAssert.Contains("Haus", tip);
            StringAssert.Contains("= 3,3", tip);   // 2 × 1.1 × 1.5
            var comer = run.Grid.Words.Single(w => w.Entry.Id == "comer");
            run.ApplyConjugationModResult(comer, new[] { true, false, true, true, true, true });
            tip = UiText.WordTooltip(comer, run);
            StringAssert.Contains("<color=#B06060>tú: comes</color>", tip);
            StringAssert.Contains("yo: como", tip);
        }

        [Test]
        public void HotSwapExchangesHeldAndPlacedWord()
        {
            var db = LoadSample();
            var run = new RunState(cfg, db, new SystemRng(1));
            run.StartNewRun();
            var dos = run.Grid.Words.Single(w => w.Entry.Id == "dos");      // line at (0,3)..(3,3)
            var perro = run.CreateWord(db.Get("perro"), ShapeFamily.Line, 0);   // size 3
            perro.Location = WordLocation.Held;
            Assert.IsFalse(run.Grid.CanPlace(perro, new Vector2Int(0, 3), 0));
            Assert.AreEqual(dos, run.SwapCandidate(run.Grid, perro, new Vector2Int(0, 3), 0));
            var displaced = run.PlaceWithSwap(run.Grid, perro, new Vector2Int(0, 3), 0);
            Assert.AreEqual(dos, displaced);
            Assert.AreEqual(WordLocation.Held, dos.Location);
            Assert.AreEqual(WordLocation.Grid, perro.Location);
            Assert.IsFalse(run.Grid.Contains(dos));
            // overlapping two words at once is not a swap
            var big = run.CreateWord(db.Get("informacion"), ShapeFamily.Line, 0);
            big.Size = 6;
            Assert.IsTrue(run.Grid.AllInside(big, new Vector2Int(1, 0), 1));
            Assert.IsNull(run.SwapCandidate(run.Grid, big, new Vector2Int(1, 0), 1));
        }

        [Test]
        public void ShedStoresWordsWithoutScoring()
        {
            var run = new RunState(cfg, LoadSample(), new SystemRng(1));
            run.StartNewRun();
            Assert.AreEqual(cfg.shedWidth, run.Shed.Width);
            var casa = run.Grid.Words.Single(w => w.Entry.Id == "casa");
            Assert.IsTrue(run.PlaceIn(run.Shed, casa, Vector2Int.zero, 0));
            Assert.AreEqual(WordLocation.Shed, casa.Location);
            Assert.IsFalse(run.Grid.Contains(casa));
            // casa no longer scores, and grande loses its noun synergy: comer 3.3 + grande 4 + dos 1
            Assert.AreEqual(8.3, run.RoundScorePreview, 1e-6);
            Assert.IsTrue(run.OwnedIds().Contains("casa"), "shed words stay owned");
        }

        [Test]
        public void ShopOfferShapeIsUsedOnPurchase()
        {
            var run = new RunState(cfg, LoadSample(), new SystemRng(3));
            run.StartNewRun();
            run.Gold = 100;
            var offer = run.Shop.Words[0];
            var w = run.CompleteWordPurchase(offer, null);
            Assert.AreEqual(offer.Family, w.Family);
        }

        // ------------------------------------------------------------ vocabulary validation

        [Test]
        public void VocabularyValidationSkipsInvalidEntries()
        {
            const string json = @"{ ""version"": 1, ""entries"": [
                { ""id"": ""ok"", ""es"": ""sí"", ""de"": [""ja""], ""type"": ""other"" },
                { ""id"": ""Bad Id"", ""es"": ""x"", ""de"": [""x""], ""type"": ""other"" },
                { ""id"": ""nonoun"", ""es"": ""gato"", ""de"": [""Katze""], ""type"": ""noun"" },
                { ""id"": ""noforms"", ""es"": ""ir"", ""de"": [""gehen""], ""type"": ""verb"", ""verb"": { ""irregular"": true } },
                { ""id"": ""ok"", ""es"": ""sí"", ""de"": [""ja""], ""type"": ""other"" },
                { ""id"": ""badtype"", ""es"": ""y"", ""de"": [""und""], ""type"": ""conj"" }
            ]}";
            var db = VocabularyLoader.Parse(json);
            Assert.AreEqual(1, db.Entries.Count);
            Assert.AreEqual(5, db.Warnings.Count);
        }

        [Test]
        public void LiveVocabularyFileParses()
        {
            var json = Resources.Load<TextAsset>("Vocabulary/vocabulary");
            Assert.IsNotNull(json, "vocabulary.json missing");
            var db = VocabularyLoader.Parse(json.text);
            Assert.Greater(db.Entries.Count, 0, string.Join("\n", db.Warnings));
        }

        [Test]
        public void SampleVocabularyLoads()
        {
            var db = LoadSample();
            Assert.AreEqual(17, db.Entries.Count);
            Assert.AreEqual(WordType.Phrase, db.Get("que_tal").Type);
            Assert.AreEqual("el", db.Get("agua").Noun.articleDefinite);
        }
    }
}
