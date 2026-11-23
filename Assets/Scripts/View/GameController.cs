using System;
using System.Collections;
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
    /// All game rules live in RunState and the calculators.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        [SerializeField] BalanceConfig config;
        [SerializeField] SpriteSet sprites;

        enum Mode { Menu, Planning, Holding, Targeting, Modal, Harvesting, GameOver }

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
        TextMeshProUGUI bigText;
        Leaderboard leaderboard;
        RunLogger logger;

        // held word (attached to the cursor)
        WordInstance held;
        bool heldIsNew;
        WordLocation originLocation;
        Vector2Int originAnchor;
        int originRotation;
        int originSlot;
        int heldRotation;
        Vector2Int grabOffset;

        // mod targeting
        UpgradeOffer pendingMod;
        int pendingModPrice;

        bool skipHarvest;
        bool worldTooltipShown;

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
            field.Init(sprites);

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
            var db = VocabularyLoader.Parse(json);
            foreach (var w in db.Warnings) Debug.LogWarning("[Vokabeln] " + w);
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
            gameOver.Hide();
            leaderboard.Load();
            menu.Show(leaderboard.Top(config.leaderboardSize), ExamMode);
        }

        public void NewGame()
        {
            if (vocab.Entries.Count == 0) { toast.Show("Keine gültigen Vokabeln geladen."); return; }
            run = new RunState(config, vocab, new SystemRng());
            run.StartNewRun();
            runIsPractice = !ExamMode;
            menu.Hide();
            gameOver.Hide();
            hud.SetVisible(true);
            shop.SetVisible(true);
            shop.SetOpen(true);
            mode = Mode.Planning;
            RefreshAll();
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

        void RefreshAll()
        {
            field.Rebuild(run);
            string hint = mode == Mode.Holding ? UiText.HoldingHint : mode == Mode.Targeting ? UiText.TargetingHint : "";
            hud.Refresh(run, mode == Mode.Planning, hint);
            shop.Refresh(run, mode == Mode.Planning);
        }

        // ================================================================ input

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame) debugOverlay.Toggle();
            if (mouse == null) return;

            if (mode == Mode.Harvesting)
            {
                if (mouse.leftButton.wasPressedThisFrame) skipHarvest = true;
                return;
            }
            if (run == null || (mode != Mode.Planning && mode != Mode.Holding && mode != Mode.Targeting)) return;

            Vector2 mp = mouse.position.ReadValue();
            Vector2 world = cam.ScreenToWorldPoint(new Vector3(mp.x, mp.y, -cam.transform.position.z));
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            var cell = FieldView.FieldGrid.WorldToCell(world);

            switch (mode)
            {
                case Mode.Planning: UpdatePlanning(world, overUi, mouse); break;
                case Mode.Holding: UpdateHolding(world, overUi, mouse, kb); break;
                case Mode.Targeting: UpdateTargeting(world, cell, overUi, mouse, kb); break;
            }
        }

        /// <summary>Which grid (field or shed) the cursor is over, and the cell in it.</summary>
        bool GridAt(Vector2 world, out GridModel grid, out FlatGrid iso, out Vector2Int cell)
        {
            cell = FieldView.FieldGrid.WorldToCell(world);
            if (run.Grid.IsInside(cell)) { grid = run.Grid; iso = FieldView.FieldGrid; return true; }
            cell = FieldView.ShedGrid.WorldToCell(world);
            if (run.Shed.IsInside(cell)) { grid = run.Shed; iso = FieldView.ShedGrid; return true; }
            grid = null; iso = null;
            return false;
        }

        WordInstance WordAt(Vector2 world, Vector2Int unused = default)
        {
            if (GridAt(world, out var grid, out _, out var cell))
            {
                var w = grid.OccupantAt(cell);
                if (w != null) return w;
            }
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
            if (!overUi && hovered != null && mouse.leftButton.wasPressedThisFrame)
            {
                GridAt(world, out _, out _, out var cell);
                PickUpExisting(hovered, cell);
            }
        }

        void PickUpExisting(WordInstance w, Vector2Int clickedCell)
        {
            heldIsNew = false;
            originLocation = w.Location;
            originAnchor = w.Anchor;
            originRotation = w.Rotation;
            originSlot = w.WheelbarrowSlot;
            heldRotation = w.Rotation;
            grabOffset = w.Location == WordLocation.Grid || w.Location == WordLocation.Shed ? w.Anchor - clickedCell : Vector2Int.zero;
            run.PickUp(w);
            BeginHolding(w);
        }

        void StartHoldingNew(WordInstance w)
        {
            if (w == null) { mode = Mode.Planning; RefreshAll(); return; }
            heldIsNew = true;
            heldRotation = 0;
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
            if ((kb != null && kb.rKey.wasPressedThisFrame) || mouse.rightButton.wasPressedThisFrame)
            {
                heldRotation = (heldRotation + 1) % 4;
                grabOffset = Shapes.Rotate(grabOffset, 1);
            }

            bool overGrid = GridAt(world, out var grid, out var iso, out var cell);
            var anchor = cell + grabOffset;
            var state = PreviewState.Invalid;
            WordInstance swap = null;
            if (overGrid)
            {
                if (grid.CanPlace(held, anchor, heldRotation)) state = PreviewState.Valid;
                else if ((swap = run.SwapCandidate(grid, held, anchor, heldRotation)) != null) state = PreviewState.Swap;
            }
            field.ShowHeld(held, overGrid ? iso : null, anchor, heldRotation, state, world, swap);

            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                CancelHolding();
                return;
            }

            if (!mouse.leftButton.wasPressedThisFrame || overUi) return;

            if (overGrid)
            {
                if (state == PreviewState.Valid && run.PlaceIn(grid, held, anchor, heldRotation)) EndHolding();
                else if (state == PreviewState.Swap) HotSwap(grid, anchor, cell);
                return;
            }
            if (FieldView.IsOverCompost(world))
            {
                string es = held.Entry.Es;
                int refund = run.Compost(held);
                toast.Show(UiText.Composted(es, refund));
                EndHolding();
            }
        }

        /// <summary>Plants the held word and picks up the word it replaces, keeping that word's shape under the cursor.</summary>
        void HotSwap(GridModel grid, Vector2Int anchor, Vector2Int cursorCell)
        {
            var displaced = run.PlaceWithSwap(grid, held, anchor, heldRotation);
            if (displaced == null) return;
            heldIsNew = false;
            originLocation = grid.Location;
            originAnchor = displaced.Anchor;
            originRotation = displaced.Rotation;
            originSlot = -1;
            heldRotation = displaced.Rotation;
            grabOffset = displaced.Anchor - cursorCell;
            field.HideHeld();
            BeginHolding(displaced);
        }

        bool StoreAnywhere(WordInstance w)
        {
            if (run.PutInFreeWheelbarrowSlot(w)) return true;
            return run.Shed.FindFirstFit(w, out var a, out var r) && run.PlaceIn(run.Shed, w, a, r);
        }

        void CancelHolding()
        {
            bool ok;
            if (heldIsNew) ok = StoreAnywhere(held);
            else
            {
                ok = originLocation == WordLocation.Grid ? run.PlaceIn(run.Grid, held, originAnchor, originRotation)
                    : originLocation == WordLocation.Shed ? run.PlaceIn(run.Shed, held, originAnchor, originRotation)
                    : run.PutInWheelbarrow(held, originSlot);
                if (!ok) ok = StoreAnywhere(held);
            }
            if (ok) EndHolding();
            else toast.Show(UiText.ShedFull);
        }

        void UpdateTargeting(Vector2 world, Vector2Int cell, bool overUi, Mouse mouse, Keyboard kb)
        {
            var hovered = overUi ? null : WordAt(world, cell);
            var targets = run.Grid.Words.Where(w => run.IsValidModTarget(pendingMod.Kind, w)).ToList();
            field.ShowTargets(targets, targets.Contains(hovered) ? hovered : null);
            UpdateWorldTooltip(hovered, overUi);

            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                run.CancelModPurchase(pendingMod, pendingModPrice);
                pendingMod = null;
                field.ClearGlow();
                mode = Mode.Planning;
                RefreshAll();
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame && !overUi && hovered != null && run.IsValidModTarget(pendingMod.Kind, hovered))
            {
                var kind = pendingMod.Kind;
                pendingMod = null;
                field.ClearGlow();
                tooltip.Hide();
                worldTooltipShown = false;
                mode = Mode.Modal;
                RefreshAll();
                if (kind == OfferKind.ArticleMod) ArticleQuiz(hovered);
                else ConjugationQuiz(hovered);
            }
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

        public void OnBuyUpgrade(UpgradeOffer o)
        {
            if (mode != Mode.Planning) return;
            string why = run.WhyCannotBuy(o);
            if (why != null) { toast.Show(why); return; }

            if (o.IsMod)
            {
                if (!run.BeginModPurchase(o, out pendingModPrice)) return;
                pendingMod = o;
                mode = Mode.Targeting;
                RefreshAll();
                return;
            }
            if (o.Kind == OfferKind.FieldExpansion)
            {
                FieldExpansionChoice(o);
                return;
            }
            if (run.BuyUpgrade(o)) RefreshAll();
        }

        void FieldExpansionChoice(UpgradeOffer o)
        {
            mode = Mode.Modal;
            RefreshAll();
            dialog.Open(UiText.FieldChoiceTitle);
            dialog.AddText($"{run.Grid.Width} × {run.Grid.Height}  (max {run.Grid.MaxWidth} × {run.Grid.MaxHeight})", 15);
            var row = dialog.AddButtonRow();
            var col = dialog.AddButton(row, UiText.AddColumn, () => { run.BuyFieldExpansion(o, true); CloseDialog(); }, null, 120);
            col.interactable = run.Grid.CanAddColumn;
            var rowBtn = dialog.AddButton(row, UiText.AddRow, () => { run.BuyFieldExpansion(o, false); CloseDialog(); }, null, 120);
            rowBtn.interactable = run.Grid.CanAddRow;
            dialog.AddButton(row, UiText.Cancel, CloseDialog, Pal.ButtonDanger, 120);
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

            dialog.Open(UiText.PurchaseQuizTitle(o.SpanishToGerman));
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
            dialog.Focus(input);

            submit = () =>
            {
                if (done) return;
                if (string.IsNullOrWhiteSpace(input.text)) { dialog.Focus(input); return; }
                run.RegisterQuizAttempt();
                var res = o.SpanishToGerman ? checker.CheckGerman(input.text, o.Entry.De) : checker.CheckSpanish(input.text, o.Entry.Es);
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
                bool accentOnly = run.Checker.CheckSpanish(input.text, entry.Es).Result == AnswerResult.CorrectIgnoringAccents;
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

        void ArticleQuiz(WordInstance w)
        {
            var e = w.Entry;
            var checker = run.Checker;
            int attempts = config.quizAttempts;
            bool done = false;
            Action submit = null;

            dialog.Open(UiText.ArticleQuizTitle);
            dialog.AddText(UiText.ArticlePrompt(e), 16);
            var def = dialog.AddInput(UiText.ArticleDefinite, () => submit(), 110);
            var indef = dialog.AddInput(UiText.ArticleIndefinite, () => submit(), 110);
            dialog.AddAccentButtons();
            var feedback = dialog.AddText(UiText.AttemptsLeft(attempts), 14, Pal.TextMuted);
            var answerLine = ExamMode ? null : dialog.AddAnswerLine();
            var row = dialog.AddButtonRow();
            var submitBtn = dialog.AddButton(row, UiText.Submit, () => submit());
            if (!ExamMode)
                dialog.AddRevealButton(row, answerLine, () => $"{e.Noun.articleDefinite} {e.Es} · {e.Noun.articleIndefinite} {e.Es}");
            dialog.Focus(def);

            submit = () =>
            {
                if (done) return;
                if (string.IsNullOrWhiteSpace(def.text) || string.IsNullOrWhiteSpace(indef.text))
                {
                    dialog.Focus(string.IsNullOrWhiteSpace(def.text) ? def : indef);
                    return;
                }
                run.RegisterQuizAttempt();
                bool ok1 = checker.CheckExact(def.text, e.Noun.articleDefinite).IsCorrect;
                bool ok2 = checker.CheckExact(indef.text, e.Noun.articleIndefinite).IsCorrect;
                def.image.color = ok1 ? Pal.InputLockedGood : Pal.InputWrong;
                indef.image.color = ok2 ? Pal.InputLockedGood : Pal.InputWrong;
                if (ok1 && ok2)
                {
                    done = true;
                    run.ApplyArticleModResult(w, true);
                    dialog.Close();
                    toast.Show(UiText.ArticleSuccess(config.articleMultiplier));
                    mode = Mode.Planning;
                    RefreshAll();
                    return;
                }
                attempts--;
                if (attempts > 0)
                {
                    feedback.text = UiText.Wrong(attempts);
                    feedback.color = Pal.Bad;
                    dialog.Focus(ok1 ? indef : def);
                    return;
                }
                done = true;
                run.ApplyArticleModResult(w, false);
                feedback.text = UiText.ModFailed + "\n" + UiText.Solution($"{e.Noun.articleDefinite} {e.Es} · {e.Noun.articleIndefinite} {e.Es}");
                feedback.color = Pal.Bad;
                def.interactable = indef.interactable = false;
                submitBtn.gameObject.SetActive(false);
                dialog.AddButton(row, UiText.Continue, CloseDialog);
            };
        }

        void ConjugationQuiz(WordInstance w)
        {
            var e = w.Entry;
            var forms = e.PresentForms;
            var checker = run.Checker;
            var correct = (bool[])w.ConjugationCorrect.Clone();
            int submits = config.quizAttempts;
            bool done = false;
            Action submit = null;

            dialog.Open(UiText.ConjugationQuizTitle, 500);
            dialog.AddText(UiText.ConjugationPrompt(e), 16);
            var inputs = new TMP_InputField[6];
            for (int i = 0; i < 6; i++)
            {
                inputs[i] = dialog.AddInput(UiText.Persons[i], () => submit(), 170);
                if (correct[i])
                {
                    inputs[i].text = forms[i];
                    inputs[i].interactable = false;
                    inputs[i].image.color = Pal.InputLockedGood;
                }
            }
            dialog.AddAccentButtons();
            var feedback = dialog.AddText(UiText.AttemptsLeft(submits), 14, Pal.TextMuted);
            var answerLine = ExamMode ? null : dialog.AddAnswerLine();
            var row = dialog.AddButtonRow();
            var submitBtn = dialog.AddButton(row, UiText.Submit, () => submit());
            if (!ExamMode)
                dialog.AddRevealButton(row, answerLine, () =>
                    string.Join(" · ", forms));   // one line, in the order of the input fields, so the dialog keeps fitting the screen
            dialog.Focus(inputs.FirstOrDefault(x => x.interactable));

            submit = () =>
            {
                if (done) return;
                run.RegisterQuizAttempt();
                bool accentHint = false;
                for (int i = 0; i < 6; i++)
                {
                    if (correct[i]) continue;
                    var r = checker.CheckExact(inputs[i].text, forms[i]);
                    if (r.IsCorrect)
                    {
                        correct[i] = true;
                        accentHint |= r.Result == AnswerResult.CorrectIgnoringAccents;
                        inputs[i].text = forms[i];
                        inputs[i].interactable = false;
                        inputs[i].image.color = Pal.InputLockedGood;
                    }
                    else inputs[i].image.color = Pal.InputWrong;
                }
                submits--;
                bool all = correct.All(b => b);
                if (!all && submits > 0)
                {
                    feedback.text = (accentHint ? "Akzente ergänzt. " : "") + UiText.AttemptsLeft(submits);
                    feedback.color = Pal.Bad;
                    dialog.Focus(inputs.FirstOrDefault(x => x.interactable));
                    return;
                }
                done = true;
                int before = w.CorrectForms;
                int oldSize = w.Size;
                run.ApplyConjugationModResult(w, correct);
                for (int i = 0; i < 6; i++)
                    if (!correct[i])
                    {
                        inputs[i].text = forms[i];
                        inputs[i].textComponent.color = Pal.Bad;
                        inputs[i].image.color = Pal.InputWrong;
                        inputs[i].interactable = false;
                    }
                string msg = UiText.ConjugationResult(w.CorrectForms, oldSize, w.Size);
                if (w.CorrectForms == before) msg = UiText.ModFailed + "\n" + msg;
                feedback.text = msg;
                feedback.color = w.CorrectForms > before ? Pal.Good : Pal.Bad;
                submitBtn.gameObject.SetActive(false);
                dialog.AddButton(row, UiText.Continue, CloseDialog);
            };
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
            var res = run.Harvest(forced);

            // 1) words bounce and pop their points
            float total = config.harvestAnimationSeconds;
            int n = Mathf.Max(1, res.WordScores.Count);
            float per = Mathf.Min(0.25f, total * 0.55f / n);
            foreach (var ws in res.WordScores)
            {
                if (skipHarvest) break;
                StartCoroutine(field.Bounce(field.WordObject(ws.Word.Id), 0.25f));
                field.FloatText(FieldView.FieldGrid.CellCenter(ws.Anchor) + Vector3.up * 0.8f, "+" + UiText.F1(ws.Score), new Color32(255, 220, 80, 255), 0.8f);
                yield return Wait(per);
            }

            // 2) total counts up
            bool reached = ScoreCalculator.Reaches(res.Score, res.Target);
            float countTime = total * 0.25f, t = 0;
            while (t < countTime && !skipHarvest)
            {
                t += Time.deltaTime;
                bigText.text = UiText.HudScore(res.Score * Mathf.Clamp01(t / countTime), res.Target);
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
                    modsLearned = run.ModsLearned,
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
        }

        // ================================================================ debug overlay

        bool DebugReady => run != null && mode == Mode.Planning;

        public void DebugAddGold()
        {
            if (run == null) return;
            run.Gold += 50;
            RefreshAll();
        }

        public void DebugChangeRound(int delta)
        {
            if (!DebugReady) return;
            run.DebugSetRound(run.Round + delta);
            RefreshAll();
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
            toast.Show(UiText.DebugReloaded(vocab.Entries.Count, vocab.Warnings.Count));
            if (run != null) RefreshAll();
        }

        public void DebugToggleLabels()
        {
            field.ShowScoreLabels = !field.ShowScoreLabels;
            if (run != null) field.Rebuild(run);
        }
    }
}
