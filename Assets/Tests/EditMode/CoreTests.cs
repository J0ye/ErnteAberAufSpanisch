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
        VocabularyDatabase db;

        [SetUp]
        public void SetUp()
        {
            cfg = ScriptableObject.CreateInstance<BalanceConfig>();
            cfg.randomizeStartVerbAndNoun = false;   // most tests rely on the fixed yo / comer / pan start
            calc = new ComplexityCalculator(cfg);
            db = LoadSample();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(cfg);

        static VocabularyDatabase LoadSample()
        {
            var d = VocabularyLoader.Parse(TestVocabulary.Json);
            Assert.IsEmpty(d.Warnings, string.Join("\n", d.Warnings));
            return d;
        }

        RunState StartRun(int seed = 1)
        {
            var run = new RunState(cfg, db, new SystemRng(seed));
            run.StartNewRun();
            return run;
        }

        /// <summary>A run with an empty field of the given size (max 12×7) and an empty shed.</summary>
        RunState EmptyField(int w = 12, int h = 7)
        {
            var run = StartRun();
            run.Grid.Clear();
            run.Shed.Clear();
            run.Grid.SetSize(w, h);
            return run;
        }

        /// <summary>Plants a word. person ≥ 0 conjugates a verb (removing the infinitive cell), form ≥ 0 inflects an adjective.</summary>
        static WordInstance Put(RunState run, string id, int x, int y, int person = -1, int form = 0)
        {
            var w = run.CreateWord(run.Vocab.Get(id), 0);
            if (person >= 0)
            {
                w.VerbPerson = person;
                w.Size = run.Complexity.ConjugatedSize(w.Stats);
            }
            w.AdjectiveForm = form;
            Assert.IsTrue(run.PlaceIn(run.Grid, w, new Vector2Int(x, y)), $"cannot place {id} at {x},{y}");
            return w;
        }

        static List<Structure> Structures(RunState run) => run.Analysis.Structures;

        static bool Has(RunState run, StructureCategory c, params WordInstance[] words) =>
            Structures(run).Any(s => s.Category == c && s.Words.Select(t => t.Id).SequenceEqual(words.Select(w => w.Id)));

        // ------------------------------------------------------------ stats (section 3)

        [TestCase("yo", WordType.Pronoun, false, 1, 1, 1)]
        [TestCase("la", WordType.Article, false, 1, 1, 1)]
        [TestCase("y", WordType.Conjunction, false, 1, 1, 1)]
        [TestCase("dónde", WordType.QuestionWord, false, 1, 1, 1)]
        [TestCase("pan", WordType.Noun, false, 3, 1, 4)]
        [TestCase("casa", WordType.Noun, false, 3, 2, 4)]
        [TestCase("comer", WordType.Verb, false, 3, 3, 2)]
        [TestCase("ser", WordType.Verb, true, 3, 4, 2)]
        [TestCase("siempre", WordType.Other, false, 2, 5, 2)]
        [TestCase("¿qué tal?", WordType.Phrase, false, 2, 6, 2)]
        [TestCase("mañana", WordType.Other, false, 1, 8, 1)]
        [TestCase("información", WordType.Noun, false, 1, 17, 0)]
        public void StatsTable(string es, WordType type, bool irregular, int size, int points, int gold)
        {
            var s = calc.Compute(es, type, irregular);
            Assert.AreEqual(size, s.BaseSize, "size");
            Assert.AreEqual(points, s.BasePoints, "base points");
            Assert.AreEqual(gold, s.Gold, "gold");
        }

        [TestCase("pan", 3)]
        [TestCase("casa", 4)]
        [TestCase("comer", 5)]
        [TestCase("siempre", 7)]
        [TestCase("¿qué tal?", 8)]
        [TestCase("mañana", 9)]
        [TestCase("información", 13)]
        public void ComplexityK(string es, int k) => Assert.AreEqual(k, calc.ComputeK(es, false));

        [Test]
        public void IrregularVerbK() => Assert.AreEqual(6, calc.ComputeK("ser", true));

        [Test]
        public void ConjugatedVerbLosesTheInfinitiveCell()
        {
            Assert.AreEqual(2, calc.ConjugatedSize(calc.Compute("comer", WordType.Verb, false)));
            Assert.AreEqual(2, calc.ConjugatedSize(calc.Compute("ser", WordType.Verb, true)));
        }

        [Test]
        public void FunctionWordsCostSix()
        {
            foreach (var t in new[] { WordType.Pronoun, WordType.Article, WordType.Conjunction, WordType.QuestionWord })
                Assert.AreEqual(6, calc.Compute("nosotros", t, false).Price, t.ToString());
        }

        [Test]
        public void JokerPriceIsCeilOfDiscount()
        {
            Assert.AreEqual(9, calc.JokerPrice(15));  // 9.0
            Assert.AreEqual(8, calc.JokerPrice(13));  // 7.8 -> 8
        }

        // ------------------------------------------------------------ growth schedule (section 2)

        [TestCase(1, 6, 2)]
        [TestCase(3, 6, 2)]
        [TestCase(4, 6, 3)]
        [TestCase(8, 7, 3)]
        [TestCase(11, 7, 4)]
        [TestCase(15, 8, 4)]
        [TestCase(18, 8, 5)]
        [TestCase(22, 9, 5)]
        [TestCase(25, 9, 6)]
        [TestCase(29, 10, 6)]
        [TestCase(32, 10, 7)]
        [TestCase(36, 11, 7)]
        [TestCase(39, 12, 7)]
        [TestCase(60, 12, 7)]
        public void GrowthSchedule_(int round, int w, int h)
        {
            var (gw, gh) = GrowthSchedule.SizeAt(cfg, round);
            Assert.AreEqual((w, h), (gw, gh), $"round {round}");
        }

        [Test]
        public void FieldGrowsAtRoundStartAndKeepsWords()
        {
            var run = StartRun();
            var yo = run.Grid.Words.Single(w => w.Entry.Id == "yo");
            for (int r = 1; r < 4; r++) { run.Harvest(ForcedOutcome.Pass); run.AdvanceRound(); }
            Assert.AreEqual(4, run.Round);
            Assert.IsTrue(run.FieldGrewThisRound);
            Assert.AreEqual(6, run.Grid.Width);
            Assert.AreEqual(3, run.Grid.Height);
            Assert.AreEqual(new Vector2Int(0, 0), yo.Anchor, "new rows are added at the bottom");
            run.Harvest(ForcedOutcome.Pass); run.AdvanceRound();
            Assert.IsFalse(run.FieldGrewThisRound);
        }

        // ------------------------------------------------------------ start state and round 1 (section 5)

        [Test]
        public void StartState()
        {
            var run = StartRun();
            Assert.AreEqual((6, 2), (run.Grid.Width, run.Grid.Height));
            var yo = run.Grid.Words.Single(w => w.Entry.Id == "yo");
            var comer = run.Grid.Words.Single(w => w.Entry.Id == "comer");
            var pan = run.Shed.Words.Single(w => w.Entry.Id == "pan");
            Assert.AreEqual(new Vector2Int(0, 0), yo.Anchor);
            Assert.AreEqual(new Vector2Int(1, 0), comer.Anchor);
            Assert.AreEqual(3, comer.Size);
            Assert.AreEqual(3, pan.Size);
            Assert.AreEqual(5, run.Gold);
            Assert.AreEqual(1, run.GrammarActionsLeft);
            Assert.IsTrue(run.ScarecrowAvailable);
            Assert.AreEqual(10.0, run.Target, 1e-9);
        }

        [Test]
        public void Round1_StartLayout_Scores4()
        {
            var run = StartRun();
            Assert.AreEqual(4.0, run.RoundScorePreview, 1e-9);
            Assert.IsEmpty(Structures(run));
        }

        [Test]
        public void Round1_Conjugated_YoComo_Scores6()
        {
            var run = StartRun();
            var comer = run.Grid.Words.Single(w => w.Entry.Id == "comer");
            run.ApplyConjugation(comer, 0, true);
            Assert.AreEqual("como", comer.Shown);
            Assert.AreEqual(2, comer.Size);
            Assert.AreEqual(0, run.GrammarActionsLeft);
            Assert.AreEqual(6.0, run.RoundScorePreview, 1e-9);
            Assert.AreEqual(StructureCategory.Satzteil, Structures(run).Single().Category);
        }

        [Test]
        public void Round1_PanUnderComo_Scores9_Fails()
        {
            var run = StartRun();
            var comer = run.Grid.Words.Single(w => w.Entry.Id == "comer");
            run.ApplyConjugation(comer, 0, true);
            var pan = run.Shed.Words.Single();
            Assert.IsTrue(run.PlaceIn(run.Grid, pan, new Vector2Int(1, 1)));
            Assert.AreEqual(9.0, run.RoundScorePreview, 1e-9, string.Join("\n", Structures(run)));
            Assert.AreEqual(2, Structures(run).Count(s => s.Category == StructureCategory.Satzteil));
            var res = run.Harvest();
            Assert.IsFalse(res.Passed);
            Assert.IsTrue(res.ScarecrowSaved);
        }

        [Test]
        public void Round1_YoComoPan_Scores12_5_Passes()
        {
            var run = StartRun();
            var comer = run.Grid.Words.Single(w => w.Entry.Id == "comer");
            run.ApplyConjugation(comer, 0, true);
            Assert.IsTrue(run.PlaceIn(run.Grid, run.Shed.Words.Single(), new Vector2Int(3, 0)));
            Assert.AreEqual(12.5, run.RoundScorePreview, 1e-9);
            var res = run.Harvest();
            Assert.IsTrue(res.Passed);
            Assert.AreEqual(1, res.Count(StructureCategory.Satz));
            Assert.AreEqual(10, res.GoldIncome);
            Assert.AreEqual(15, run.Gold);
            Assert.AreEqual(12.5, run.Rank, 1e-9);
        }

        [Test]
        public void TargetCurve()
        {
            var sc = new ScoreCalculator(cfg);
            Assert.AreEqual(10.0, sc.Target(1), 1e-9);
            Assert.AreEqual(59.9, sc.Target(10), 0.05);
            Assert.AreEqual(437.4, sc.Target(20), 0.05);
        }

        // ------------------------------------------------------------ article merge

        [Test]
        public void Merge_FittingArticle_KeepsNounSizeAndAddsPointsGoldPrice()
        {
            var run = EmptyField();
            var pan = Put(run, "pan", 0, 0);
            var el = run.CreateWord(run.Vocab.Get("el"), 2);
            el.Location = WordLocation.Held;
            int size = pan.Size, bp = pan.Stats.BasePoints + el.Stats.BasePoints, gold = pan.Stats.Gold + el.Stats.Gold;

            Assert.AreSame(pan, run.MergeCandidate(run.Grid, el, new Vector2Int(1, 0)));
            Assert.IsTrue(run.MergeArticle(el, pan));

            Assert.AreEqual(size, pan.Size);
            Assert.AreEqual("el pan", pan.Shown);
            Assert.AreEqual(bp, pan.BasePoints);
            Assert.AreEqual(bp, run.ScoreOf(pan).BasePoints);
            Assert.AreEqual(cfg.baseIncome + gold, run.IncomePreview);
            Assert.AreEqual(2, pan.TotalPricePaid);
            Assert.AreEqual(1, run.Grid.Words.Count, "the article is no longer a separate word");
            Assert.AreEqual(WordLocation.Merged, el.Location);
        }

        [Test]
        public void Merge_WrongArticle_OrSecondArticle_Rejected()
        {
            var run = EmptyField();
            var pan = Put(run, "pan", 0, 0);
            var la = run.CreateWord(run.Vocab.Get("la"), 0);
            Assert.IsNull(run.MergeCandidate(run.Grid, la, new Vector2Int(0, 0)), "la does not fit el pan");
            Assert.IsFalse(run.MergeArticle(la, pan));

            Assert.IsTrue(run.MergeArticle(run.CreateWord(run.Vocab.Get("un"), 0), pan));
            Assert.IsFalse(run.MergeArticle(run.CreateWord(run.Vocab.Get("el"), 0), pan), "only one article per noun");

            var yo = Put(run, "yo", 0, 1);
            Assert.IsFalse(run.MergeArticle(run.CreateWord(run.Vocab.Get("el"), 0), yo), "articles merge into nouns only");
        }

        [Test]
        public void Merge_CountsAsArticleNounPhrase()
        {
            var run = EmptyField();
            var pan = Put(run, "pan", 0, 0);
            Assert.AreEqual(0, Structures(run).Count);
            run.MergeArticle(run.CreateWord(run.Vocab.Get("el"), 0), pan);
            Assert.IsTrue(Has(run, StructureCategory.Satzteil, pan), "el pan alone is an NP of two words");

            // a separate article in front of a merged noun does not form a second NP
            var run2 = EmptyField();
            var el = Put(run2, "el", 0, 0);
            var pan2 = Put(run2, "pan", 1, 0);
            run2.MergeArticle(run2.CreateWord(run2.Vocab.Get("el"), 0), pan2);
            Assert.IsFalse(Structures(run2).Any(st => st.Contains(el.Id)));
        }

        [Test]
        public void Merge_CompostRefundsBothWords()
        {
            var run = EmptyField();
            var pan = run.CreateWord(run.Vocab.Get("pan"), 4);
            Assert.IsTrue(run.PlaceIn(run.Grid, pan, Vector2Int.zero));
            run.MergeArticle(run.CreateWord(run.Vocab.Get("el"), 2), pan);
            int gold = run.Gold;
            Assert.AreEqual(run.CompostRefund(pan), run.Compost(pan));
            Assert.AreEqual(gold + (int)System.Math.Floor(cfg.discardRefund * 6), run.Gold);
        }

        [Test]
        public void StartWords_Randomized_VerbAndNounVaryPronounFixed()
        {
            cfg.randomizeStartVerbAndNoun = true;
            var verbs = new HashSet<string>();
            var nouns = new HashSet<string>();
            for (int seed = 0; seed < 40; seed++)
            {
                var run = StartRun(seed);
                var all = run.Grid.Words.Concat(run.Shed.Words).ToList();
                Assert.AreEqual(1, all.Count(w => w.Entry.Id == "yo"), $"seed {seed}");
                var verb = all.Single(w => w.Entry.Type == WordType.Verb);
                var noun = all.Single(w => w.Entry.Type == WordType.Noun);
                Assert.IsTrue(verb.Entry.Verb.transitive, $"seed {seed}: {verb.Entry.Id}");
                verbs.Add(verb.Entry.Id);
                nouns.Add(noun.Entry.Id);
            }
            int transitiveVerbs = db.Entries.Count(e => e.Type == WordType.Verb && e.Verb.transitive && !e.Verb.copula);
            if (transitiveVerbs > 1) Assert.Greater(verbs.Count, 1, "start verb should vary between runs");
            Assert.Greater(nouns.Count, 1, "start noun should vary between runs");
        }

        // ------------------------------------------------------------ sentence engine (section 4)

        [Test]
        public void Engine_YoComoPan_Satz()
        {
            var run = EmptyField();
            var yo = Put(run, "yo", 0, 0);
            var como = Put(run, "comer", 1, 0, person: 0);
            var pan = Put(run, "pan", 3, 0);
            Assert.IsTrue(Has(run, StructureCategory.Satz, yo, como, pan));
            Assert.AreEqual(1, Structures(run).Count);
        }

        static string MagnifierText(RunState run, WordInstance w)
        {
            var byId = run.Grid.Words.ToDictionary(x => x.Id);
            return UiText.MagnifierTooltip(w, run.Analysis.StructuresOf(w.Id).ToList(),
                id => byId.TryGetValue(id, out var x) ? x : null, run.ScoreOf(w));
        }

        [Test]
        public void Magnifier_ShowsSentenceAndGloss()
        {
            var run = EmptyField();
            Put(run, "yo", 0, 0);
            var como = Put(run, "comer", 1, 0, person: 0);
            Put(run, "pan", 3, 0);
            var satz = Structures(run).Single();
            Assert.AreEqual("Yo como pan.", UiText.SentenceText(satz));
            string s = MagnifierText(run, como);
            StringAssert.Contains("Teil von 1 Struktur", s);
            StringAssert.Contains("Yo como pan.", s);
            StringAssert.Contains("ich · essen [yo] · Brot", s);
            StringAssert.Contains(UiText.StructureLabel(StructureCategory.Satz, satz.Bonus), s);
            StringAssert.Contains(UiText.F1(run.ScoreOf(como).Total) + " Pkt", s);
        }

        [Test]
        public void Magnifier_ListsEveryStructureOfAWord()
        {
            var run = StartRun();
            var comer = run.Grid.Words.Single(w => w.Entry.Id == "comer");
            run.ApplyConjugation(comer, 0, true);
            Assert.IsTrue(run.PlaceIn(run.Grid, run.Shed.Words.Single(), new Vector2Int(1, 1)));
            var shared = run.Grid.Words.FirstOrDefault(w => run.Analysis.StructuresOf(w.Id).Count() == 2);
            Assert.IsNotNull(shared, string.Join("\n", Structures(run)));
            string s = MagnifierText(run, shared);
            StringAssert.Contains("Teil von 2 Strukturen", s);
            foreach (var st in run.Analysis.StructuresOf(shared.Id))
                StringAssert.Contains(UiText.SentenceText(st), s);
        }

        [Test]
        public void Magnifier_WordWithoutStructure()
        {
            var run = EmptyField();
            var pan = Put(run, "pan", 0, 0);
            StringAssert.Contains(UiText.MagnifierNoStructure, MagnifierText(run, pan));
            Assert.AreEqual("ich", UiText.GlossWord(Put(run, "yo", 6, 3)));
        }

        [Test]
        public void Engine_InfinitiveMatchesNothing()
        {
            var run = EmptyField();
            Put(run, "yo", 0, 0);
            Put(run, "comer", 1, 0);
            Put(run, "pan", 4, 0);
            Assert.IsEmpty(Structures(run));
        }

        [Test]
        public void Engine_AgreementFails_TuComo()
        {
            var run = EmptyField();
            Put(run, "tu", 0, 0);
            Put(run, "comer", 1, 0, person: 0);
            Assert.IsEmpty(Structures(run));
        }

        [Test]
        public void Engine_ElPerroComePan_Satz()
        {
            var run = EmptyField();
            var el = Put(run, "el", 0, 0);
            var perro = Put(run, "perro", 1, 0);
            var come = Put(run, "comer", 3, 0, person: 2);
            var pan = Put(run, "pan", 5, 0);
            Assert.IsTrue(Has(run, StructureCategory.Satz, el, perro, come, pan));
        }

        [Test]
        public void Engine_LaPerro_Nothing()
        {
            var run = EmptyField();
            Put(run, "la", 0, 0);
            Put(run, "perro", 1, 0);
            Assert.IsEmpty(Structures(run));
        }

        [Test]
        public void Engine_ElAgua_Satzteil()
        {
            var run = EmptyField();
            var el = Put(run, "el", 0, 0);
            var agua = Put(run, "agua", 1, 0);
            Assert.IsTrue(Has(run, StructureCategory.Satzteil, el, agua));
        }

        [Test]
        public void Engine_LaCasaEsGrande_Satz()
        {
            var run = EmptyField();
            var la = Put(run, "la", 0, 0);
            var casa = Put(run, "casa", 1, 0);
            var es = Put(run, "ser", 4, 0, person: 2);
            var grande = Put(run, "grande", 6, 0);
            Assert.AreEqual("es", es.Shown);
            Assert.IsTrue(Has(run, StructureCategory.Satz, la, casa, es, grande));
        }

        [Test]
        public void Engine_LaCasaEsRojo_NoSatz_UntilInflectedToRoja()
        {
            var run = EmptyField();
            var la = Put(run, "la", 0, 0);
            var casa = Put(run, "casa", 1, 0);
            var es = Put(run, "ser", 4, 0, person: 2);
            var rojo = Put(run, "rojo", 6, 0);
            // no sentence; only the article + noun phrase "la casa" remains a Satzteil
            Assert.IsFalse(Structures(run).Any(s => s.Category >= StructureCategory.Satz), string.Join("\n", Structures(run)));
            Assert.IsFalse(Structures(run).Any(s => s.Contains(rojo.Id) || s.Contains(es.Id)));

            run.GrammarActionsLeft = 1;
            run.ApplyInflection(rojo, 1, true);
            Assert.AreEqual("roja", rojo.Shown);
            Assert.IsTrue(Has(run, StructureCategory.Satz, la, casa, es, rojo));
        }

        [Test]
        public void Engine_YoVivo_Satz()
        {
            var run = EmptyField();
            var yo = Put(run, "yo", 0, 0);
            var vivo = Put(run, "vivir", 1, 0, person: 0);
            Assert.IsTrue(Has(run, StructureCategory.Satz, yo, vivo));
        }

        [Test]
        public void Engine_Questions()
        {
            var run = EmptyField();
            var que = Put(run, "que", 0, 0);
            var comes = Put(run, "comer", 1, 0, person: 1);
            var quien = Put(run, "quien", 0, 2);
            var come = Put(run, "comer", 1, 2, person: 2);
            var pan = Put(run, "pan", 3, 2);
            var donde = Put(run, "donde", 0, 4);
            var vives = Put(run, "vivir", 1, 4, person: 1);
            Assert.IsTrue(Has(run, StructureCategory.Frage, que, comes), string.Join("\n", Structures(run)));
            Assert.IsTrue(Has(run, StructureCategory.Frage, quien, come, pan));
            Assert.IsTrue(Has(run, StructureCategory.Frage, donde, vives));
        }

        [Test]
        public void Engine_CoordinatedSubject_Satz()
        {
            // too long for one row: stacked in column 0, read top to bottom
            var run = EmptyField();
            var words = new[]
            {
                Put(run, "el", 0, 0), Put(run, "perro", 0, 1), Put(run, "y", 0, 2), Put(run, "el", 0, 3),
                Put(run, "gato", 0, 4), Put(run, "comer", 0, 5, person: 5), Put(run, "pan", 0, 6)
            };
            Assert.AreEqual("comen", words[5].Shown);
            var satz = Structures(run).Single(s => s.Category == StructureCategory.Satz && s.Words.Count == 7);
            Assert.AreEqual(LineDirection.Down, satz.Direction);
        }

        [Test]
        public void Engine_StackedVertically_Satz()
        {
            var run = EmptyField();
            var yo = Put(run, "yo", 0, 0);
            var como = Put(run, "comer", 0, 1, person: 0);
            var pan = Put(run, "pan", 0, 2);
            Assert.IsTrue(Has(run, StructureCategory.Satz, yo, como, pan));
        }

        [Test]
        public void Engine_CrossingWordCollectsBothBonuses()
        {
            var run = EmptyField();
            Put(run, "yo", 0, 0);
            Put(run, "comer", 1, 0, person: 0);
            var pan = Put(run, "pan", 3, 0);
            var grande = Put(run, "grande", 3, 1);   // ↓ pan grande: NP of two words
            Assert.IsTrue(Has(run, StructureCategory.Satzteil, pan, grande), string.Join("\n", Structures(run)));
            Assert.AreEqual(pan.Stats.BasePoints * 3.0, run.ScoreOf(pan).Total, 1e-9);
        }

        [Test]
        public void Engine_GapBreaksSequence()
        {
            var run = EmptyField();
            Put(run, "yo", 0, 0);
            Put(run, "comer", 2, 0, person: 0);
            Assert.IsEmpty(Structures(run));
        }

        [Test]
        public void Engine_PatternsAreData()
        {
            var p = SentencePattern.Parse(StructureCategory.Frage, "Q:object V:transitive Subject?");
            Assert.AreEqual(3, p.Elements.Count);
            Assert.IsTrue(p.Elements[2].Optional);
            Assert.Throws<System.FormatException>(() => SentencePattern.Parse(StructureCategory.Satz, "Subject Banana"));
            // a pattern added only as data is matched without code changes
            cfg.sentencePatterns = cfg.sentencePatterns.Concat(new[] { new SentencePatternDef(StructureCategory.Satzteil, "Pronoun Y Pronoun") }).ToArray();
            var run = EmptyField();
            var yo = Put(run, "yo", 0, 0);
            var y = Put(run, "y", 1, 0);
            var tu = Put(run, "tu", 2, 0);
            Assert.IsTrue(Has(run, StructureCategory.Satzteil, yo, y, tu));
        }

        // ------------------------------------------------------------ word bars, grammar action, scarecrow

        [Test]
        public void BarsOccupyCellsToTheRight()
        {
            var run = EmptyField(6, 2);
            var comer = Put(run, "comer", 1, 0);
            CollectionAssert.AreEqual(new[] { new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(3, 0) }, comer.Cells().ToList());
            Assert.AreEqual(comer, run.Grid.OccupantAt(new Vector2Int(3, 0)));
            Assert.IsNull(run.Grid.OccupantAt(new Vector2Int(4, 0)));
            var pan = run.CreateWord(db.Get("pan"), 0);
            Assert.IsFalse(run.Grid.CanPlace(pan, new Vector2Int(4, 0)), "would stick out at the right edge");
        }

        [Test]
        public void ConjugationShrinksAtRightEnd_LaterChangeKeepsSize()
        {
            var run = StartRun();
            var comer = run.Grid.Words.Single(w => w.Entry.Id == "comer");
            run.ApplyConjugation(comer, 0, true);
            Assert.AreEqual(new Vector2Int(1, 0), comer.Anchor);
            Assert.IsNull(run.Grid.OccupantAt(new Vector2Int(3, 0)));
            run.GrammarActionsLeft = 1;
            run.ApplyConjugation(comer, 2, true);
            Assert.AreEqual("come", comer.Shown);
            Assert.AreEqual(2, comer.Size);
        }

        [Test]
        public void FailedGrammarActionUsesActionAndChangesNothing()
        {
            var run = StartRun();
            var comer = run.Grid.Words.Single(w => w.Entry.Id == "comer");
            run.ApplyConjugation(comer, 0, false);
            Assert.AreEqual(0, run.GrammarActionsLeft);
            Assert.IsFalse(comer.IsConjugated);
            Assert.AreEqual(3, comer.Size);
            run.Harvest(ForcedOutcome.Pass);
            run.AdvanceRound();
            Assert.AreEqual(1, run.GrammarActionsLeft, "refreshed per round, unused ones do not carry over");
        }

        [Test]
        public void Scarecrow_VerbRegrowsInPlace()
        {
            var run = StartRun();
            var comer = run.Grid.Words.Single(w => w.Entry.Id == "comer");
            run.ApplyConjugation(comer, 0, true);
            var res = run.Harvest(ForcedOutcome.Fail);
            Assert.IsTrue(res.ScarecrowSaved);
            Assert.IsFalse(comer.IsConjugated);
            Assert.AreEqual(3, comer.Size);
            Assert.AreEqual(WordLocation.Grid, comer.Location);
        }

        [Test]
        public void Scarecrow_VerbMovesToShedWhenBlocked()
        {
            var run = StartRun();
            var comer = run.Grid.Words.Single(w => w.Entry.Id == "comer");
            run.ApplyConjugation(comer, 0, true);
            run.PlaceIn(run.Grid, run.Shed.Words.Single(), new Vector2Int(3, 0));   // pan right of como
            run.ApplyScarecrowPenalty();
            Assert.AreEqual(WordLocation.Shed, comer.Location);
            Assert.AreEqual(3, comer.Size);
        }

        [Test]
        public void Scarecrow_VerbCompostedWhenShedFull()
        {
            var run = StartRun();
            var comer = run.Grid.Words.Single(w => w.Entry.Id == "comer");
            run.ApplyConjugation(comer, 0, true);
            run.PlaceIn(run.Grid, run.Shed.Words.Single(), new Vector2Int(3, 0));
            foreach (var (id, row) in new[] { ("casa", 0), ("perro", 1), ("gato", 2) })
            {
                var filler = run.CreateWord(db.Get(id), 0);
                filler.Size = 5;
                Assert.IsTrue(run.PlaceIn(run.Shed, filler, new Vector2Int(0, row)));
            }
            run.ApplyScarecrowPenalty();
            Assert.IsFalse(run.OwnedWords().Contains(comer));
        }

        [Test]
        public void Scarecrow_AdjectiveFallsBack()
        {
            var run = EmptyField();
            var rojo = Put(run, "rojo", 0, 0, form: 3);
            Assert.AreEqual("rojas", rojo.Shown);
            run.ApplyScarecrowPenalty();
            Assert.AreEqual("rojo", rojo.Shown);
        }

        [Test]
        public void ScarecrowSavesOnceThenGameOver()
        {
            var run = StartRun();
            Assert.IsTrue(run.Harvest(ForcedOutcome.Fail).ScarecrowSaved);
            Assert.IsFalse(run.ScarecrowAvailable);
            run.AdvanceRound();
            Assert.IsTrue(run.Harvest(ForcedOutcome.Fail).GameOver);
        }

        // ------------------------------------------------------------ placement

        [Test]
        public void HotSwapExchangesHeldAndPlacedWord()
        {
            var run = StartRun();
            var comer = run.Grid.Words.Single(w => w.Entry.Id == "comer");   // (1,0)..(3,0)
            var pan = run.Shed.Words.Single();
            run.PickUp(pan);
            Assert.IsFalse(run.Grid.CanPlace(pan, new Vector2Int(1, 0)));
            Assert.AreEqual(comer, run.SwapCandidate(run.Grid, pan, new Vector2Int(1, 0)));
            var displaced = run.PlaceWithSwap(run.Grid, pan, new Vector2Int(1, 0));
            Assert.AreEqual(comer, displaced);
            Assert.AreEqual(WordLocation.Held, comer.Location);
            Assert.AreEqual(WordLocation.Grid, pan.Location);
            // overlapping two words at once is not a swap
            run.PlaceIn(run.Grid, comer, new Vector2Int(1, 1));
            var big = run.CreateWord(db.Get("casa"), 0);
            big.Size = 2;
            run.PlaceIn(run.Grid, big, new Vector2Int(4, 1));
            var held = run.CreateWord(db.Get("gato"), 0);
            held.Size = 3;
            Assert.IsNull(run.SwapCandidate(run.Grid, held, new Vector2Int(3, 1)));
        }

        [Test]
        public void ShedWordsDoNotScore()
        {
            var run = StartRun();
            var comer = run.Grid.Words.Single(w => w.Entry.Id == "comer");
            Assert.IsTrue(run.PlaceIn(run.Shed, comer, new Vector2Int(0, 1)));
            Assert.AreEqual(1.0, run.RoundScorePreview, 1e-9);
            Assert.IsTrue(run.OwnedIds().Contains("comer"), "shed words stay owned");
        }

        [Test]
        public void CompostRefundsHalf()
        {
            var run = StartRun();
            var w = run.CreateWord(db.Get("siempre"), 13);
            Assert.AreEqual(6, run.Compost(w));
        }

        // ------------------------------------------------------------ shop (section 6)

        [Test]
        public void Shop_HasFiveWordsAndJoker()
        {
            var run = StartRun();
            Assert.AreEqual(5, run.Shop.Words.Count);
            Assert.IsNotNull(run.Shop.Joker);
            CollectionAssert.Contains(ShopGenerator.JokerTypes, run.Shop.Joker.Type);
            Assert.AreEqual(5, run.Shop.Words.Select(o => o.Entry.Id).Distinct().Count());
        }

        [Test]
        public void Shop_FunctionWordsStayEligibleWhenOwned_ContentWordsDoNot()
        {
            var gen = new ShopGenerator(cfg, calc);
            var eligible = gen.Eligible(db.Entries, new HashSet<string> { "yo", "comer" }, 1);
            Assert.IsTrue(eligible.Any(e => e.Id == "yo"));
            Assert.IsFalse(eligible.Any(e => e.Id == "comer"));
            Assert.IsFalse(eligible.Any(e => e.Id == "buenos_dias"), "phrase above the complexity gate");
        }

        [Test]
        public void Shop_DeficitRoles()
        {
            var gen = new ShopGenerator(cfg, calc);
            var roles = gen.RolesByDeficit(new[] { WordType.Verb, WordType.Verb });
            Assert.AreEqual(NeedRole.Noun, roles[0]);
        }

        [Test]
        public void Shop_VerbsWithoutNouns_OffersANoun()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                var run = new RunState(cfg, db, new SystemRng(seed));
                run.StartNewRun();
                run.Grid.Clear();
                run.Shed.Clear();
                Put(run, "comer", 0, 0, person: 0);
                Put(run, "vivir", 0, 1, person: 0);
                run.RefillShop();
                Assert.IsTrue(run.Shop.Words.Any(o => o.Entry.Type == WordType.Noun), $"seed {seed}");
            }
        }

        [Test]
        public void Shop_AlmostStructure_YoComo_OffersANoun()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                var run = new RunState(cfg, db, new SystemRng(seed));
                run.StartNewRun();
                run.Shed.Clear();
                var comer = run.Grid.Words.Single(w => w.Entry.Id == "comer");
                run.ApplyConjugation(comer, 0, true);   // "yo como", cell (3,0) free
                run.RefillShop();
                Assert.IsTrue(run.Shop.Completing.Contains("pan"), "pan would complete 'yo como _'");
                Assert.IsFalse(run.Shop.Completing.Contains("siempre"));
                Assert.IsTrue(run.Shop.Words.Any(o => o.Entry.Type == WordType.Noun), $"seed {seed}");
                Assert.IsTrue(run.Shop.Words.Any(o => run.Shop.Completing.Contains(o.Entry.Id)), $"seed {seed}");
            }
        }

        [Test]
        public void Shop_ArticlesSkipQuiz_PronounsAlwaysSpanishToGerman()
        {
            var gen = new ShopGenerator(cfg, calc);
            var rng = new SystemRng(5);
            for (int i = 0; i < 40; i++)
                foreach (var o in gen.MakeWordOffers(db.Entries, new List<VocabEntry>(), null, null, 1, rng))
                {
                    Assert.AreEqual(o.Entry.Type == WordType.Article, o.NoQuiz, o.Entry.Id);
                    if (o.Entry.Type == WordType.Pronoun) Assert.IsTrue(o.SpanishToGerman, o.Entry.Id);
                }

            var run = StartRun();
            run.Gold = 100;
            var offer = new WordOffer { Entry = db.Get("la"), Stats = calc.Compute(db.Get("la")) };
            Assert.IsTrue(offer.NoQuiz);
            var w = run.CompleteWordPurchase(offer, null);
            Assert.AreEqual(94, run.Gold);
            Assert.AreEqual(WordLocation.Held, w.Location);
        }

        [Test]
        public void JokerAcceptsPhrase()
        {
            var run = StartRun();
            var e = run.ValidateJoker("buenos dias", WordType.Phrase, null, out var reason);
            Assert.IsNotNull(e, reason);
            Assert.IsNull(run.ValidateJoker("buenos dias", WordType.Other, null, out _));
        }

        // ------------------------------------------------------------ answer checking (section 8)

        [Test]
        public void GermanAnswers()
        {
            var ac = new AnswerChecker(AccentMode.Lenient);
            var sol = new[] { "Information", "Auskunft" };
            Assert.IsTrue(ac.CheckGerman("  die   Information ", sol).IsCorrect);
            Assert.IsTrue(ac.CheckGerman("eine Auskunft", sol).IsCorrect);
            Assert.AreEqual("Auskunft", ac.CheckGerman("auskunft!", sol).Solution);
            Assert.IsFalse(ac.CheckGerman("Katze", new[] { "Hund" }).IsCorrect);
            Assert.IsFalse(ac.CheckGerman("", new[] { "Hund" }).IsCorrect);
        }

        [Test]
        public void SpanishAnswersStripArticlesOnlyBeforeAnotherWord()
        {
            var ac = new AnswerChecker(AccentMode.Strict);
            Assert.AreEqual(AnswerResult.Correct, ac.CheckSpanish("la casa", "casa").Result);
            Assert.AreEqual(AnswerResult.Correct, ac.CheckSpanish("el agua", db.Get("agua")).Result);
            Assert.AreEqual(AnswerResult.Correct, ac.CheckSpanish("¿Qué?", "qué").Result);
            Assert.AreEqual(AnswerResult.Wrong, ac.CheckSpanish("gato", "perro").Result);
            // article entries: never stripped
            Assert.AreEqual(AnswerResult.Correct, ac.CheckSpanish("el", db.Get("el")).Result);
            Assert.AreEqual(AnswerResult.Wrong, ac.CheckSpanish("la", db.Get("el")).Result);
            Assert.AreEqual(AnswerResult.Wrong, ac.CheckSpanish("el la", db.Get("la")).Result);
            Assert.IsTrue(ac.CheckGerman("die", db.Get("la")).IsCorrect);
            Assert.AreEqual("la", AnswerChecker.StripLeadingArticle("la", new[] { "la" }));
        }

        [Test]
        public void Accents()
        {
            var lenient = new AnswerChecker(AccentMode.Lenient);
            Assert.AreEqual(AnswerResult.CorrectIgnoringAccents, lenient.CheckSpanish("manana", "mañana").Result);
            Assert.AreEqual(AnswerResult.CorrectIgnoringAccents, lenient.CheckExact("comeis", "coméis").Result);
            var strict = new AnswerChecker(AccentMode.Strict);
            Assert.AreEqual(AnswerResult.Wrong, strict.CheckSpanish("que", "qué").Result);
        }

        [Test]
        public void ApostrophesAreIgnored()
        {
            var ac = new AnswerChecker(AccentMode.Strict);
            var sol = new[] { "wie geht's", "wie geht es dir" };
            Assert.IsTrue(ac.CheckGerman("Wie gehts?", sol).IsCorrect);
            Assert.IsTrue(ac.CheckGerman("wie geht’s", sol).IsCorrect);
            Assert.IsTrue(new AnswerChecker(AccentMode.Lenient).CheckSpanish("que tal", "¿qué tal?").IsCorrect);
        }

        // ------------------------------------------------------------ tooltip and run log

        [Test]
        public void TooltipShowsFormLemmaFeaturesStructuresAndBreakdown()
        {
            var run = StartRun();
            var comer = run.Grid.Words.Single(w => w.Entry.Id == "comer");
            run.ApplyConjugation(comer, 0, true);
            run.PlaceIn(run.Grid, run.Shed.Words.Single(), new Vector2Int(3, 0));
            var tip = UiText.WordTooltip(comer, run);
            StringAssert.Contains("como</b> – comer, yo", tip);
            StringAssert.Contains("essen", tip);
            StringAssert.Contains("Satz +1,5: yo como pan", tip);
            StringAssert.Contains("3 × (1 + 1,5) = 7,5", tip);
            var casa = run.CreateWord(db.Get("casa"), 0);
            casa.Location = WordLocation.Shed;
            StringAssert.Contains("Nomen, f., Sg.", UiText.WordTooltip(casa, run));
        }

        // ------------------------------------------------------------ vocabulary (section 8)

        [Test]
        public void VocabularyRejectsVersion1()
        {
            var d = VocabularyLoader.Parse(@"{ ""version"": 1, ""entries"": [ { ""id"": ""ok"", ""es"": ""sí"", ""de"": [""ja""], ""type"": ""other"" } ] }", "alt.json");
            Assert.AreEqual(0, d.Entries.Count);
            Assert.IsNotNull(d.Error);
            StringAssert.Contains("alt.json", d.Error);
        }

        [Test]
        public void VocabularyValidationSkipsInvalidEntries()
        {
            const string json = @"{ ""version"": 2, ""entries"": [
                { ""id"": ""ok"", ""es"": ""sí"", ""de"": [""ja""], ""type"": ""other"" },
                { ""id"": ""Bad Id"", ""es"": ""x"", ""de"": [""x""], ""type"": ""other"" },
                { ""id"": ""nonoun"", ""es"": ""gato"", ""de"": [""Katze""], ""type"": ""noun"" },
                { ""id"": ""noforms"", ""es"": ""ir"", ""de"": [""gehen""], ""type"": ""verb"", ""verb"": { ""irregular"": true } },
                { ""id"": ""ok"", ""es"": ""sí"", ""de"": [""ja""], ""type"": ""other"" },
                { ""id"": ""badtype"", ""es"": ""y"", ""de"": [""und""], ""type"": ""conj"" },
                { ""id"": ""adjmsg"", ""es"": ""rojo"", ""de"": [""rot""], ""type"": ""adjective"",
                  ""adjective"": { ""forms"": { ""msg"": ""roja"", ""fsg"": ""roja"", ""mpl"": ""rojos"", ""fpl"": ""rojas"" } } },
                { ""id"": ""badpron"", ""es"": ""yo"", ""de"": [""ich""], ""type"": ""pronoun"", ""pronoun"": { ""person"": 4, ""number"": ""sg"", ""gender"": ""x"" } },
                { ""id"": ""badq"", ""es"": ""qué"", ""de"": [""was""], ""type"": ""questionWord"", ""questionWord"": { ""role"": ""time"" } },
                { ""id"": ""okart"", ""es"": ""los"", ""de"": [""die""], ""type"": ""article"", ""article"": { ""definite"": true, ""gender"": ""m"", ""number"": ""pl"" } }
            ]}";
            var d = VocabularyLoader.Parse(json);
            Assert.AreEqual(2, d.Entries.Count, string.Join("\n", d.Warnings));
            Assert.AreEqual(8, d.Warnings.Count);
        }

        [Test]
        public void LiveVocabularyFileParses()
        {
            var json = Resources.Load<TextAsset>("Vocabulary/vocabulary");
            Assert.IsNotNull(json, "vocabulary.json missing");
            var d = VocabularyLoader.Parse(json.text);
            Assert.IsNull(d.Error, d.Error);
            Assert.Greater(d.Entries.Count, 0, string.Join("\n", d.Warnings));
        }

        [Test]
        public void SampleVocabularyLoads()
        {
            Assert.AreEqual(22, db.Entries.Count);
            Assert.AreEqual(WordType.Phrase, db.Get("buenos_dias").Type);
            Assert.AreEqual("el", db.Get("agua").Noun.articleDefinite);
            Assert.AreEqual("roja", db.Get("rojo").Adjective.forms.fsg);
            Assert.IsTrue(db.Get("ser").Verb.copula);
        }

        [Test]
        public void Search_FindsGermanSpanishAndForms_IgnoringCaseAndAccents()
        {
            Assert.AreEqual("comer", db.Search("essen")[0].Id, "German → Spanish");
            Assert.AreEqual("comer", db.Search("comer")[0].Id, "Spanish");
            Assert.AreEqual("quien", db.Search("QUIEN")[0].Id, "case and accents ignored");
            Assert.AreEqual("rojo", db.Search("roja")[0].Id, "adjective forms");
            Assert.IsTrue(db.Search("como").Any(e => e.Id == "comer"), "verb forms");
            Assert.IsEmpty(db.Search("   "));
            Assert.IsEmpty(db.Search("xyzxyz"));
        }

        [Test]
        public void VocabDetails_ShowsAllFields()
        {
            string s = UiText.VocabDetails(db.Get("comer"));
            StringAssert.Contains("essen", s);
            StringAssert.Contains("como", s);
            StringAssert.Contains("comer", s);
        }
    }
}
