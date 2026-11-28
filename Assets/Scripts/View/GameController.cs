using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cosecha.View;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Cosecha
{
    /// <summary>
    /// Owns the RunState, builds the presentation and forwards input to the rules.
    /// All game rules live in RunState, the calculators and the SentenceEngine.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        [SerializeField] BalanceConfig config;
        [SerializeField] SpriteSet sprites;

        enum Mode { Menu, Planning, Holding, Targeting, Inspecting, Modal, Harvesting, GameOver }

        const string VocabularyFile = "Assets/Resources/Vocabulary/vocabulary.json";

        public BalanceConfig Cfg => config;
        RunState run;
        VocabularyDatabase vocab;
        Mode mode = Mode.Menu;

        Camera cam;
        RectTransform canvasRect;
        FieldView field;
        HudView hud;
        ShopView shop;
        TooltipView tooltip;
        ToastView toast;
        Dialog dialog;
        MainMenuView menu;
        GameOverView gameOver;
        DebugOverlay debugOverlay;
        SearchView search;
        MagnifierView magnifier;
        TextMeshProUGUI bigText;
        Leaderboard leaderboard;
        RunLogger logger;

        // held word (attached to the cursor)
        WordInstance held;
        bool heldIsNew;
        WordLocation originLocation;
        Vector2Int originAnchor;
        int originSlot;
        Vector2Int grabOffset;

        bool skipHarvest;
        bool worldTooltipShown;
        Coroutine bannerRoutine;

        const string ExamModePref = "Cosecha.ExamMode";
        /// <summary>True = exam mode (default): no answer help in quizzes.</summary>
        public bool ExamMode { get; private set; } = true;
        bool runIsPractice;

        public void SetExamMode(bool on)
        {
            ExamMode = on;
            PlayerPrefs.SetInt(ExamModePref, on ? 1 : 0);
            PlayerPrefs.Save();
        }

        // ================================================================ setup

        void Awake()
        {
            if (config == null) config = ScriptableObject.CreateInstance<BalanceConfig>();
            ExamMode = PlayerPrefs.GetInt(ExamModePref, 1) == 1;
            Ui.Sprites = sprites;
            World.Font = sprites.font;
            cam = Camera.main;

            leaderboard = new Leaderboard(Path.Combine(Application.persistentDataPath, "leaderboard.json"));
            logger = new RunLogger(Path.Combine(Application.persistentDataPath, "runlog.csv"));
            vocab = LoadVocabulary();

            var skyGo = new GameObject("Sky");
            skyGo.AddComponent<SkyView>().Init(sprites, config);
            var fieldGo = new GameObject("Field");
            field = fieldGo.AddComponent<FieldView>();
            field.Init(sprites, config);

            BuildCanvas();
        }

        void BuildCanvas()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                es.transform.SetParent(transform, false);
            }

            var canvasGo = new GameObject("UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = 5;
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 5;
            canvas.sortingOrder = 30000;   // above every painter-sorted world sprite (see Sorting)
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = IsoLayout.CanvasRef;   // smaller reference = everything 25 % bigger
            scaler.matchWidthOrHeight = 0.5f;
            canvasRect = (RectTransform)canvasGo.transform;

            hud = canvasGo.AddComponent<HudView>(); hud.Init(this, canvasRect);
            shop = canvasGo.AddComponent<ShopView>(); shop.Init(this, canvasRect);

            bigText = Ui.Text("BigScore", canvasRect, "", 54, Color.white, TextAlignmentOptions.Center);
            bigText.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-110, 60), new Vector2(460, 80));
            bigText.fontStyle = FontStyles.Bold;
            bigText.outlineWidth = 0.25f;
            bigText.outlineColor = new Color32(40, 25, 10, 255);

            debugOverlay = canvasGo.AddComponent<DebugOverlay>(); debugOverlay.Init(this, canvasRect);
            // the magnifier is created first, so the open dictionary panel covers its button
            magnifier = canvasGo.AddComponent<MagnifierView>(); magnifier.Init(this, canvasRect, cam);
            search = canvasGo.AddComponent<SearchView>(); search.Init(() => vocab, canvasRect);
            menu = canvasGo.AddComponent<MainMenuView>(); menu.Init(this, canvasRect);
            gameOver = canvasGo.AddComponent<GameOverView>(); gameOver.Init(this, canvasRect);
            dialog = canvasGo.AddComponent<Dialog>(); dialog.Init(canvasRect);
            toast = canvasGo.AddComponent<ToastView>(); toast.Init(canvasRect);
            tooltip = canvasGo.AddComponent<TooltipView>(); tooltip.Init(canvasRect, cam);
        }

        void Start() => ShowMenu();

        VocabularyDatabase LoadVocabulary()
        {
            string json = null;
#if UNITY_EDITOR
            string path = Path.Combine(Application.dataPath, "Resources/Vocabulary/vocabulary.json");
            if (File.Exists(path)) json = File.ReadAllText(path);
#endif
            if (json == null)
            {
                var ta = Resources.Load<TextAsset>("Vocabulary/vocabulary");
                json = ta != null ? ta.text : "{}";
            }
            var db = VocabularyLoader.Parse(json, VocabularyFile);
            if (db.Error != null) Debug.LogError("[Vokabeln] " + db.Error);
            foreach (var w in db.Warnings.Where(w => w != db.Error)) Debug.LogWarning("[Vokabeln] " + w);
            return db;
        }

        // ================================================================ menu / run lifecycle

        void ShowMenu()
        {
            mode = Mode.Menu;
            run = null;
            held = null;
            field.HideHeld();
            field.ClearGlow();
            field.Rebuild(null);
            hud.SetVisible(false);
            shop.SetVisible(false);
            search.SetAvailable(false);
            magnifier.SetVisible(false);
            gameOver.Hide();
            leaderboard.Load();
            menu.Show(leaderboard.Top(config.leaderboardSize), ExamMode);
        }

        public void NewGame()
        {
            if (vocab.Entries.Count == 0) { toast.Show(vocab.Error ?? UiText.NoVocabulary, 6f); return; }
            run = new RunState(config, vocab, new SystemRng());
            run.StartNewRun();
            runIsPractice = !ExamMode;
            menu.Hide();
            gameOver.Hide();
            hud.SetVisible(true);
            shop.SetVisible(true);
            shop.SetOpen(true);
            search.SetAvailable(!ExamMode);   // the dictionary is a learning aid: practice mode only
            magnifier.SetVisible(true);
            mode = Mode.Planning;
            RefreshAll(false);
        }

        public void BackToMenu() => ShowMenu();

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>Redraws everything. The sentence engine re-runs on the new field; new structures pop in.</summary>
        void RefreshAll(bool animateStructures = true)
        {
            field.Rebuild(run, animateStructures);
            string hint = mode == Mode.Holding ? UiText.HoldingHint
                : mode == Mode.Targeting ? UiText.GrammarTargetHint
                : mode == Mode.Inspecting ? UiText.MagnifierHint
                : mode == Mode.Planning ? UiText.PlanningHint : "";
            hud.Refresh(run, mode == Mode.Planning, hint);
            shop.Refresh(run, mode == Mode.Planning);
            debugOverlay.Refresh(run);
            magnifier.Refresh(run != null && mode == Mode.Planning && run.Analysis.Structures.Count > 0,
                mode == Mode.Inspecting, !ExamMode);
        }

        // ================================================================ input

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame) { debugOverlay.Toggle(); debugOverlay.Refresh(run); }
            // Escape closes the dictionary first (and nothing else that frame)
            if (kb != null && kb.escapeKey.wasPressedThisFrame && search.IsOpen && !dialog.IsOpen) { search.Close(); return; }
            if (mouse == null) return;

            if (mode == Mode.Harvesting)
            {
                if (mouse.leftButton.wasPressedThisFrame) skipHarvest = true;
                return;
            }
            if (run == null || (mode != Mode.Planning && mode != Mode.Holding && mode != Mode.Targeting && mode != Mode.Inspecting)) return;

            Vector2 mp = mouse.position.ReadValue();
            Vector2 world = cam.ScreenToWorldPoint(new Vector3(mp.x, mp.y, -cam.transform.position.z));
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            switch (mode)
            {
                case Mode.Planning: UpdatePlanning(world, overUi, mouse); break;
                case Mode.Holding: UpdateHolding(world, overUi, mouse, kb); break;
                case Mode.Targeting: UpdateTargeting(world, overUi, mouse, kb); break;
                case Mode.Inspecting: UpdateInspecting(world, overUi, mouse, kb); break;
            }
        }

        /// <summary>Which grid (field or shed) the cursor is over, and the cell in it.</summary>
        bool GridAt(Vector2 world, out GridModel grid, out FlatGrid flat, out Vector2Int cell)
        {
            cell = FieldView.FieldGrid.WorldToCell(world);
            if (run.Grid.IsInside(cell)) { grid = run.Grid; flat = FieldView.FieldGrid; return true; }
            cell = FieldView.ShedGrid.WorldToCell(world);
            if (run.Shed.IsInside(cell)) { grid = run.Shed; flat = FieldView.ShedGrid; return true; }
            grid = null; flat = null;
            return false;
        }

        WordInstance WordAt(Vector2 world)
        {
            if (GridAt(world, out var grid, out _, out var cell))
                return grid.OccupantAt(cell);
            return null;
        }

        void UpdateWorldTooltip(WordInstance hovered, bool overUi)
        {
            if (hovered != null && !overUi)
            {
                tooltip.Show(UiText.WordTooltip(hovered, run));
                worldTooltipShown = true;
            }
            else if (worldTooltipShown)
            {
                tooltip.Hide();
                worldTooltipShown = false;
            }
        }

        void UpdatePlanning(Vector2 world, bool overUi, Mouse mouse)
        {
            var hovered = overUi ? null : WordAt(world);
            UpdateWorldTooltip(hovered, overUi);
            if (overUi || hovered == null) return;
            if (mouse.leftButton.wasPressedThisFrame)
            {
                GridAt(world, out _, out _, out var cell);
                PickUpExisting(hovered, cell);
            }
            else if (mouse.rightButton.wasPressedThisFrame && RunState.IsGrammarTarget(hovered))
            {
                if (run.GrammarActionsLeft <= 0) toast.Show(UiText.NoGrammarAction);
                else OpenGrammarQuiz(hovered);
            }
        }

        void PickUpExisting(WordInstance w, Vector2Int clickedCell)
        {
            heldIsNew = false;
            originLocation = w.Location;
            originAnchor = w.Anchor;
            originSlot = w.WheelbarrowSlot;
            grabOffset = w.Location == WordLocation.Grid || w.Location == WordLocation.Shed ? new Vector2Int(w.Anchor.x - clickedCell.x, 0) : Vector2Int.zero;
            run.PickUp(w);
            BeginHolding(w);
        }

        void StartHoldingNew(WordInstance w)
        {
            if (w == null) { mode = Mode.Planning; RefreshAll(); return; }
            heldIsNew = true;
            grabOffset = Vector2Int.zero;
            BeginHolding(w);
        }

        void BeginHolding(WordInstance w)
        {
            held = w;
            mode = Mode.Holding;
            shop.SetOpen(false);   // auto-hide the shop drawer while a crop is held
            tooltip.Hide();
            worldTooltipShown = false;
            RefreshAll();
        }

        void EndHolding()
        {
            held = null;
            field.HideHeld();
            field.ClearGlow();
            mode = Mode.Planning;
            RefreshAll();
        }

        void UpdateHolding(Vector2 world, bool overUi, Mouse mouse, Keyboard kb)
        {
            // words are horizontal bars: no rotation (R and right-click do nothing)
            bool overGrid = GridAt(world, out var grid, out var flat, out var cell);
            var anchor = cell + grabOffset;
            var state = PreviewState.Invalid;
            WordInstance swap = null;
            if (overGrid)
            {
                // an article dropped on a fitting noun merges into it (takes priority over placing and swapping)
                if ((swap = run.MergeCandidate(grid, held, cell)) != null) state = PreviewState.Merge;
                else if (grid.CanPlace(held, anchor)) state = PreviewState.Valid;
                else if ((swap = run.SwapCandidate(grid, held, anchor)) != null) state = PreviewState.Swap;
            }
            field.ShowHeld(held, overGrid ? flat : null, anchor, state, world, swap);

            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                CancelHolding();
                return;
            }

            if (!mouse.leftButton.wasPressedThisFrame || overUi) return;

            if (overGrid)
            {
                if (state == PreviewState.Valid && run.PlaceIn(grid, held, anchor)) EndHolding();
                else if (state == PreviewState.Swap) HotSwap(grid, anchor, cell);
                else if (state == PreviewState.Merge)
                {
                    string article = held.Entry.Es, noun = swap.Entry.Es;
                    if (run.MergeArticle(held, swap))
                    {
                        toast.Show(UiText.ArticleMerged(article, noun, swap.BasePoints, swap.GoldYield));
                        EndHolding();
                    }
                }
                return;
            }
            if (FieldView.IsOverCompost(world))
            {
                string es = held.Shown;
                int refund = run.Compost(held);
                toast.Show(UiText.Composted(es, refund));
                EndHolding();
            }
        }

        /// <summary>Plants the held word and picks up the word it replaces, keeping that word under the cursor.</summary>
        void HotSwap(GridModel grid, Vector2Int anchor, Vector2Int cursorCell)
        {
            var displaced = run.PlaceWithSwap(grid, held, anchor);
            if (displaced == null) return;
            heldIsNew = false;
            originLocation = grid.Location;
            originAnchor = displaced.Anchor;
            originSlot = -1;
            grabOffset = new Vector2Int(displaced.Anchor.x - cursorCell.x, 0);
            field.HideHeld();
            BeginHolding(displaced);
        }

        void CancelHolding()
        {
            bool ok;
            if (heldIsNew) ok = run.StoreInShed(held);
            else
            {
                ok = originLocation == WordLocation.Grid ? run.PlaceIn(run.Grid, held, originAnchor)
                    : originLocation == WordLocation.Shed ? run.PlaceIn(run.Shed, held, originAnchor)
                    : run.PutInWheelbarrow(held, originSlot);
                if (!ok) ok = run.StoreInShed(held);
            }
            if (ok) EndHolding();
            else toast.Show(UiText.ShedFull);
        }

        // ================================================================ free grammar action

        public void OnGrammarClicked()
        {
            if (mode != Mode.Planning) return;
            if (run.GrammarActionsLeft <= 0) { toast.Show(UiText.NoGrammarAction); return; }
            if (!run.HasGrammarTarget) { toast.Show(UiText.NoGrammarTarget); return; }
            shop.SetOpen(false);
            mode = Mode.Targeting;
            RefreshAll();
        }

        void UpdateTargeting(Vector2 world, bool overUi, Mouse mouse, Keyboard kb)
        {
            var hovered = overUi ? null : WordAt(world);
            var targets = run.Grid.Words.Where(RunState.IsGrammarTarget).ToList();
            field.ShowTargets(targets, targets.Contains(hovered) ? hovered : null);
            UpdateWorldTooltip(hovered, overUi);

            if ((kb != null && kb.escapeKey.wasPressedThisFrame) || mouse.rightButton.wasPressedThisFrame)
            {
                field.ClearGlow();
                mode = Mode.Planning;
                RefreshAll();
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame && !overUi && run.CanUseGrammarAction(hovered))
                OpenGrammarQuiz(hovered);
        }

        /// <summary>
        /// Conjugate a verb (pick a person) or inflect an adjective (pick a form), then type the form. 3 attempts.
        /// The dialog can be cancelled until the first attempt; after that the action is used either way.
        /// </summary>
        void OpenGrammarQuiz(WordInstance w)
        {
            field.ClearGlow();
            tooltip.Hide();
            worldTooltipShown = false;
            mode = Mode.Modal;
            RefreshAll();

            bool verb = w.Type == WordType.Verb;
            var e = w.Entry;
            string[] forms = verb ? e.PresentForms : e.AdjectiveForms;
            string[] names = verb ? UiText.Persons : UiText.AdjectiveFormNames;
            var checker = run.Checker;
            int attempts = config.quizAttempts;
            int selected = -1;
            bool started = false, done = false;
            Action submit = null;
            TMP_InputField input = null;
            TextMeshProUGUI chosenLine = null, answerLine = null;

            dialog.Open(verb ? UiText.ConjugateTitle : UiText.InflectTitle, 520);
            dialog.AddText(verb ? UiText.ConjugatePrompt(e) : UiText.InflectPrompt(e), 15);
            var choices = new List<Button>();
            int perRow = verb ? 3 : 4;
            RectTransform choiceRow = null;
            for (int i = 0; i < names.Length; i++)
            {
                if (i % perRow == 0) choiceRow = dialog.AddButtonRow();
                int idx = i;
                var b = dialog.AddButton(choiceRow, names[i], () => Select(idx), Pal.ButtonAlt, verb ? 150 : 106);
                // a form the word already shows would change nothing
                b.interactable = verb ? w.VerbPerson != i : !string.Equals(forms[i], w.Shown, StringComparison.OrdinalIgnoreCase);
                choices.Add(b);
            }
            chosenLine = dialog.AddText(verb ? UiText.PickPersonFirst : UiText.PickFormFirst, 14, Pal.TextMuted);
            input = dialog.AddInput("", () => submit());
            dialog.AddAccentButtons();
            var feedback = dialog.AddText(UiText.AttemptsLeft(attempts), 14, Pal.TextMuted);
            answerLine = ExamMode ? null : dialog.AddAnswerLine();
            var row = dialog.AddButtonRow();
            var submitBtn = dialog.AddButton(row, UiText.Submit, () => submit());
            if (!ExamMode)
                dialog.AddRevealButton(row, answerLine, () => selected < 0 ? (verb ? UiText.PickPersonFirst : UiText.PickFormFirst) : forms[selected]);
            var cancelBtn = dialog.AddButton(row, UiText.Cancel, CloseDialog, Pal.ButtonDanger);

            void Select(int i)
            {
                if (started || done) return;
                selected = i;
                for (int k = 0; k < choices.Count; k++)
                    choices[k].GetComponent<Image>().color = k == i ? Pal.Button : Pal.ButtonAlt;
                chosenLine.text = $"<b>{names[i]}</b>: …";
                chosenLine.color = Pal.Text;
                if (answerLine != null) answerLine.text = forms[i];
                dialog.Focus(input);
            }

            submit = () =>
            {
                if (done) return;
                if (selected < 0)
                {
                    feedback.text = verb ? UiText.PickPersonFirst : UiText.PickFormFirst;
                    feedback.color = Pal.Bad;
                    return;
                }
                if (string.IsNullOrWhiteSpace(input.text)) { dialog.Focus(input); return; }
                if (!started)
                {
                    started = true;
                    cancelBtn.gameObject.SetActive(false);
                    foreach (var c in choices) c.interactable = false;
                }
                run.RegisterQuizAttempt();
                var res = checker.CheckExact(input.text, forms[selected]);
                if (res.IsCorrect)
                {
                    done = true;
                    if (verb) run.ApplyConjugation(w, selected, true);
                    else run.ApplyInflection(w, selected, true);
                    dialog.Close();
                    toast.Show(res.Result == AnswerResult.CorrectIgnoringAccents ? UiText.AccentHint(forms[selected]) : UiText.GrammarSuccess(forms[selected]), 2.5f);
                    mode = Mode.Planning;
                    RefreshAll();
                    return;
                }
                attempts--;
                if (attempts > 0)
                {
                    feedback.text = UiText.Wrong(attempts);
                    feedback.color = Pal.Bad;
                    input.text = "";
                    dialog.Focus(input);
                    return;
                }
                done = true;
                if (verb) run.ApplyConjugation(w, selected, false);
                else run.ApplyInflection(w, selected, false);
                feedback.text = UiText.GrammarFailed + "\n" + UiText.Solution(forms[selected]);
                feedback.color = Pal.Bad;
                input.interactable = false;
                submitBtn.gameObject.SetActive(false);
                dialog.AddButton(row, UiText.Continue, CloseDialog);
            };
        }

        // ================================================================ magnifier

        public void OnMagnifierClicked()
        {
            if (mode == Mode.Inspecting) { StopInspecting(); return; }
            if (mode != Mode.Planning || run.Analysis.Structures.Count == 0) return;
            shop.SetOpen(false);
            tooltip.Hide();
            worldTooltipShown = false;
            mode = Mode.Inspecting;
            RefreshAll();
        }

        void StopInspecting()
        {
            field.ClearGlow();
            tooltip.Hide();
            worldTooltipShown = false;
            mode = Mode.Planning;
            RefreshAll();
        }

        /// <summary>Hovering a field word shows every structure it is part of, with a German gloss; its sentences glow.</summary>
        void UpdateInspecting(Vector2 world, bool overUi, Mouse mouse, Keyboard kb)
        {
            if ((kb != null && kb.escapeKey.wasPressedThisFrame) || mouse.rightButton.wasPressedThisFrame)
            {
                StopInspecting();
                return;
            }

            var hovered = overUi ? null : WordAt(world);
            if (hovered != null && hovered.Location != WordLocation.Grid) hovered = null;   // the shed does not score
            if (hovered == null)
            {
                field.ShowTargets(Enumerable.Empty<WordInstance>(), null);
                if (worldTooltipShown) { tooltip.Hide(); worldTooltipShown = false; }
                // a click on empty field or sky puts the magnifier down (clicks on UI are handled by the UI)
                if (mouse.leftButton.wasPressedThisFrame && !overUi) StopInspecting();
                return;
            }

            var structures = run.Analysis.StructuresOf(hovered.Id).ToList();
            var byId = run.Grid.Words.ToDictionary(w => w.Id);
            var glow = structures.SelectMany(s => s.Words)
                .Select(t => byId.TryGetValue(t.Id, out var w) ? w : null)
                .Where(w => w != null).Distinct().ToList();
            if (!glow.Contains(hovered)) glow.Add(hovered);
            field.ShowTargets(glow, hovered);
            tooltip.Show(UiText.MagnifierTooltip(hovered, structures,
                id => byId.TryGetValue(id, out var w) ? w : null, run.ScoreOf(hovered)));
            worldTooltipShown = true;
        }

        // ================================================================ shop actions

        public void OnReroll()
        {
            if (mode != Mode.Planning) return;
            if (run.TryReroll()) RefreshAll();
        }

        public void OnBuyWord(WordOffer o)
        {
            if (mode != Mode.Planning || !o.Available || !run.CanAfford(o.Stats.Price)) return;
            if (o.NoQuiz)
            {
                // articles are bought without a quiz; the sentence rules practice them
                var w = run.CompleteWordPurchase(o, null);
                toast.Show(UiText.BoughtWithoutQuiz(o.Entry.Es), 2f);
                StartHoldingNew(w);
                return;
            }
            mode = Mode.Modal;
            RefreshAll();
            PurchaseQuiz(o);
        }

        public void OnJoker()
        {
            if (mode != Mode.Planning || run.Shop.Joker == null || !run.Shop.Joker.Available) return;
            mode = Mode.Modal;
            RefreshAll();
            JokerQuiz(run.Shop.Joker);
        }

        void CloseDialog()
        {
            dialog.Close();
            mode = Mode.Planning;
            RefreshAll();
        }

        // ================================================================ quizzes

        void PurchaseQuiz(WordOffer o)
        {
            var checker = run.Checker;
            int attempts = config.quizAttempts;
            bool done = false;
            Action submit = null;

            dialog.Open(UiText.PurchaseQuizTitle(o.SpanishToGerman), 520);
            dialog.AddText($"<b>{o.Prompt}</b>", 28);
            dialog.AddText(UiText.TypeName(o.Entry.Type), 13, Pal.TextMuted);
            var input = dialog.AddInput("", () => submit());
            dialog.AddAccentButtons();
            var feedback = dialog.AddText(UiText.AttemptsLeft(attempts), 14, Pal.TextMuted);
            var answerLine = ExamMode ? null : dialog.AddAnswerLine();
            var row = dialog.AddButtonRow();
            var submitBtn = dialog.AddButton(row, UiText.Submit, () => submit());
            if (!ExamMode)
                dialog.AddRevealButton(row, answerLine, () => o.SpanishToGerman ? string.Join(" / ", o.Entry.De) : SpanishWithArticle(o.Entry));
            // the purchase can be cancelled (for free) until the first answer is submitted
            var cancelBtn = dialog.AddButton(row, UiText.Cancel, CloseDialog, Pal.ButtonDanger);
            dialog.Focus(input);

            submit = () =>
            {
                if (done) return;
                if (string.IsNullOrWhiteSpace(input.text)) { dialog.Focus(input); return; }
                cancelBtn.gameObject.SetActive(false);
                run.RegisterQuizAttempt();
                var res = o.SpanishToGerman ? checker.CheckGerman(input.text, o.Entry) : checker.CheckSpanish(input.text, o.Entry);
                if (res.IsCorrect)
                {
                    done = true;
                    var w = run.CompleteWordPurchase(o, o.SpanishToGerman ? input.text.Trim() : null);
                    dialog.Close();
                    toast.Show(res.Result == AnswerResult.CorrectIgnoringAccents ? UiText.AccentHint(res.Solution) : UiText.Correct, 2.5f);
                    StartHoldingNew(w);
                    return;
                }
                attempts--;
                if (attempts > 0)
                {
                    feedback.text = UiText.Wrong(attempts);
                    feedback.color = Pal.Bad;
                    input.text = "";
                    dialog.Focus(input);
                    return;
                }
                done = true;
                run.FailWordPurchase(o);
                run.RegisterQuizFailure();
                string solution = o.SpanishToGerman ? string.Join(" / ", o.Entry.De) : o.Entry.Es;
                feedback.text = UiText.PurchaseFailed + "\n" + UiText.Solution(solution);
                feedback.color = Pal.Bad;
                input.interactable = false;
                submitBtn.gameObject.SetActive(false);
                dialog.AddButton(row, UiText.Continue, CloseDialog);
            };
        }

        void JokerQuiz(JokerOffer joker)
        {
            Action submit = null;
            dialog.Open(UiText.JokerQuizTitle(joker.Type));
            dialog.AddText(UiText.JokerPrompt(joker.Type), 14);
            var input = dialog.AddInput("", () => submit());
            dialog.AddAccentButtons();
            var feedback = dialog.AddText(UiText.AttemptsLeft(joker.AttemptsLeft), 14, Pal.TextMuted);
            var answerLine = ExamMode ? null : dialog.AddAnswerLine();
            var row = dialog.AddButtonRow();
            var submitBtn = dialog.AddButton(row, UiText.Submit, () => submit());
            if (!ExamMode)
            {
                VocabEntry lastExample = null;
                dialog.AddRevealButton(row, answerLine, () =>
                {
                    // a different random word of the required type every time the answer is shown
                    var owned = run.OwnedIds(held);
                    var pool = run.Vocab.Entries.Where(e => e.Type == joker.Type && !owned.Contains(e.Id)).ToList();
                    if (pool.Count == 0) return UiText.JokerNoExample;
                    if (pool.Count > 1) pool.Remove(lastExample);
                    lastExample = pool[UnityEngine.Random.Range(0, pool.Count)];
                    return UiText.JokerExample(lastExample);
                });
            }
            dialog.AddButton(row, UiText.Close, CloseDialog, Pal.ButtonDanger);
            dialog.Focus(input);

            submit = () =>
            {
                if (!joker.Available || string.IsNullOrWhiteSpace(input.text)) return;
                run.RegisterQuizAttempt();
                var entry = run.ValidateJoker(input.text, joker.Type, held, out string reason);
                if (entry == null)
                {
                    joker.AttemptsLeft--;
                    feedback.color = Pal.Bad;
                    if (joker.AttemptsLeft > 0)
                    {
                        feedback.text = reason + " " + UiText.AttemptsLeft(joker.AttemptsLeft);
                        input.text = "";
                        dialog.Focus(input);
                    }
                    else
                    {
                        run.RegisterQuizFailure();
                        feedback.text = reason + "\n" + UiText.JokerExhausted;
                        input.interactable = false;
                        submitBtn.gameObject.SetActive(false);
                    }
                    return;
                }
                bool accentOnly = run.Checker.CheckSpanish(input.text, entry).Result == AnswerResult.CorrectIgnoringAccents;
                JokerConfirm(entry, accentOnly);
            };
        }

        static string SpanishWithArticle(VocabEntry e) =>
            e.Noun != null ? $"{e.Noun.articleDefinite} {e.Es}" : e.Es;

        void JokerConfirm(VocabEntry entry, bool accentHint)
        {
            int price = run.JokerPrice(entry);
            var stats = run.Complexity.Compute(entry);
            dialog.Open(UiText.JokerQuizTitle(entry.Type));
            if (accentHint) dialog.AddText(UiText.AccentHint(entry.Es), 13, Pal.Good);
            dialog.AddText(UiText.JokerConfirm(entry, stats, price), 15);
            var row = dialog.AddButtonRow();
            var buy = dialog.AddButton(row, UiText.Buy(price), () =>
            {
                var w = run.BuyJoker(entry);
                dialog.Close();
                StartHoldingNew(w);
            });
            buy.interactable = run.CanAfford(price);
            if (!run.CanAfford(price)) dialog.AddText(UiText.NotEnoughGold, 13, Pal.Bad);
            dialog.AddButton(row, UiText.Cancel, CloseDialog, Pal.ButtonDanger);
        }

        // ================================================================ harvest

        public void OnHarvestClicked()
        {
            if (mode != Mode.Planning) return;
            StartCoroutine(HarvestRoutine(ForcedOutcome.None));
        }

        IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end && !skipHarvest) yield return null;
        }

        IEnumerator HarvestRoutine(ForcedOutcome forced)
        {
            mode = Mode.Harvesting;
            shop.SetOpen(false);   // auto-hide during the harvest
            skipHarvest = false;
            tooltip.Hide();
            worldTooltipShown = false;
            field.ClearGlow();
            hud.Refresh(run, false, "");
            shop.Refresh(run, false);

            var roundStats = run.CurrentRound;
            // structures light up in order Frage → Satz → Satzteil (collected before the scarecrow can change the field)
            var structures = field.StructureObjects.OrderByDescending(x => (int)x.s.Category).Select(x => x.t).ToList();
            var res = run.Harvest(forced);
            float total = config.harvestAnimationSeconds;   // ≤ 2.5 s by default, skippable by click

            // 1) structures light up one after another
            if (structures.Count > 0)
            {
                float perStructure = Mathf.Min(0.35f, total * 0.35f / structures.Count);
                foreach (var t in structures)
                {
                    if (skipHarvest) break;
                    StartCoroutine(field.LightUp(t, Mathf.Max(0.2f, perStructure * 1.4f)));
                    yield return Wait(perStructure);
                }
            }

            // 2) words bounce and pop their points
            int n = Mathf.Max(1, res.WordScores.Count);
            float per = Mathf.Min(0.2f, total * 0.3f / n);
            foreach (var ws in res.WordScores)
            {
                if (skipHarvest) break;
                StartCoroutine(field.Bounce(field.WordObject(ws.Word.Id), 0.25f));
                field.FloatText(FieldView.FieldGrid.CellCenter(ws.Anchor) + Vector3.up * 0.8f, "+" + UiText.F1(ws.Score), new Color32(255, 220, 80, 255), 0.8f);
                yield return Wait(per);
            }

            // 3) total counts up
            bool reached = ScoreCalculator.Reaches(res.Score, res.Target);
            float countTime = total * 0.2f, t0 = 0;
            while (t0 < countTime && !skipHarvest)
            {
                t0 += Time.deltaTime;
                bigText.text = UiText.HudScore(res.Score * Mathf.Clamp01(t0 / countTime), res.Target);
                yield return null;
            }
            bigText.text = UiText.HudScore(res.Score, res.Target);
            bigText.color = reached ? new Color32(150, 255, 140, 255) : new Color32(255, 140, 120, 255);
            yield return Wait(total * 0.15f);
            bigText.text = "";
            field.ClearFloating();

            logger.LogRound(run, res, roundStats);

            if (res.ScarecrowSaved)
            {
                yield return field.ScarecrowFall();
                toast.Show(UiText.ScarecrowSaved(res.GoldIncome) + "\n" + res.PenaltyMessage, 6f);
            }
            else if (res.Passed)
            {
                toast.Show(UiText.HarvestPassed(res.Score, res.GoldIncome), 3f);
            }

            if (res.GameOver)
            {
                leaderboard.Add(new LeaderboardEntry
                {
                    date = DateTime.Now.ToString("o"),
                    rank = Math.Round(run.Rank, 1),
                    roundReached = run.Round,
                    wordsPlanted = run.WordsPlanted,
                    modsLearned = run.GrammarLearned,
                    practice = runIsPractice
                });
                mode = Mode.GameOver;
                RefreshAll();
                gameOver.Show(run);
                yield break;
            }

            run.AdvanceRound();
            mode = Mode.Planning;
            shop.SetOpen(true);    // auto-open after the harvest
            RefreshAll();
            if (run.FieldGrewThisRound) ShowBanner(UiText.FieldGrows);
        }

        void ShowBanner(string text)
        {
            if (bannerRoutine != null) StopCoroutine(bannerRoutine);
            bannerRoutine = StartCoroutine(BannerRoutine(text));
        }

        IEnumerator BannerRoutine(string text)
        {
            bigText.text = text;
            bigText.color = new Color32(255, 236, 160, 255);
            float time = 0, d = 1.8f;
            while (time < d)
            {
                time += Time.deltaTime;
                bigText.alpha = 1f - Mathf.Clamp01((time - d * 0.6f) / (d * 0.4f));
                bigText.rectTransform.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(Mathf.Clamp01(time / 0.3f) * Mathf.PI));
                yield return null;
            }
            bigText.text = "";
            bigText.alpha = 1f;
            bigText.rectTransform.localScale = Vector3.one;
            bannerRoutine = null;
        }

        // ================================================================ debug overlay

        bool DebugReady => run != null && mode == Mode.Planning;

        public void DebugAddGold()
        {
            if (run == null) return;
            run.Gold += 50;
            RefreshAll();
        }

        public void DebugAddGrammar()
        {
            if (run == null) return;
            run.GrammarActionsLeft++;
            RefreshAll();
        }

        public void DebugChangeRound(int delta)
        {
            if (!DebugReady) return;
            var (w, h) = (run.Grid.Width, run.Grid.Height);
            run.DebugSetRound(run.Round + delta);
            RefreshAll();
            if (run.Grid.Width != w || run.Grid.Height != h) ShowBanner(UiText.FieldGrows);
        }

        public void DebugForceHarvest(ForcedOutcome outcome)
        {
            if (!DebugReady) return;
            StartCoroutine(HarvestRoutine(outcome));
        }

        public void DebugRefillShop()
        {
            if (!DebugReady) return;
            run.RefillShop();
            RefreshAll();
        }

        public void DebugReloadVocabulary()
        {
            vocab = LoadVocabulary();
            if (run != null)
            {
                run.SetVocabulary(vocab);
                run.RefreshChecker();
            }
            toast.Show(vocab.Error ?? UiText.DebugReloaded(vocab.Entries.Count, vocab.Warnings.Count));
            if (run != null) RefreshAll();
        }

        public void DebugToggleLabels()
        {
            field.ShowScoreLabels = !field.ShowScoreLabels;
            if (run != null) field.Rebuild(run, false);
        }
    }
}
