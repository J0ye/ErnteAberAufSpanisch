using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Cosecha.View
{
    // ==================================================================== HUD (compact strip at the top-left)

    public class HudView : MonoBehaviour
    {
        TextMeshProUGUI line1, line2, hint;
        Button harvest, grammar;
        GameController ctl;
        RectTransform hudRoot;

        public void Init(GameController c, RectTransform canvasRect)
        {
            ctl = c;
            hudRoot = Ui.Rect("HudRoot", canvasRect).Stretch();
            var canvas = hudRoot;
            var panel = Ui.Panel("HUD", canvas);
            panel.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(6, -6), new Vector2(392, 54));
            Ui.VLayout(panel.gameObject, 0, new RectOffset(10, 10, 4, 4), TextAnchor.MiddleLeft);
            line1 = Ui.Text("Line1", panel.transform, "", 15);
            line2 = Ui.Text("Line2", panel.transform, "", 17);
            line2.fontStyle = FontStyles.Bold;
            foreach (var t in new[] { line1, line2 })
            {
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.enableAutoSizing = true; t.fontSizeMin = 10; t.fontSizeMax = t.fontSize;
            }

            grammar = Ui.Button("Grammar", canvas, UiText.GrammarButton(0), 15, () => ctl.OnGrammarClicked(), Pal.ButtonAlt);
            ((RectTransform)grammar.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(404, -6), new Vector2(112, 54));
            grammar.gameObject.AddComponent<UiTooltipTrigger>().Text = () => UiText.GrammarButtonTip;

            harvest = Ui.Button("Harvest", canvas, UiText.Harvest, 22, () => ctl.OnHarvestClicked(), Pal.Gold);
            ((RectTransform)harvest.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(522, -6), new Vector2(118, 54));
            harvest.GetComponentInChildren<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

            hint = Ui.Text("Hint", canvas, "", 14, Color.white, TextAlignmentOptions.Center);
            hint.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-90, 4), new Vector2(560, 22));
            hint.outlineWidth = 0.2f;
            hint.outlineColor = new Color32(40, 30, 20, 255);
        }

        public void Refresh(RunState run, bool canAct, string hintText)
        {
            if (run == null || run.Grid == null) return;
            double s = run.RoundScorePreview, t = run.Target;
            string scoreColor = ScoreCalculator.Reaches(s, t) ? "#3C963C" : "#BE463C";
            line1.text = $"{UiText.HudRound(run.Round)}  ·  {UiText.HudRank(run.Rank)}  ·  {UiText.HudGrammar(run.GrammarActionsLeft)}  ·  " +
                         $"<color={(run.ScarecrowAvailable ? "#3A2A1E" : "#82705F")}>{UiText.HudScarecrow(run.ScarecrowAvailable)}</color>";
            line2.text = $"<color={scoreColor}>{UiText.HudScore(s, t)}</color>   <color=#B07F10>{UiText.HudGold(run.Gold)}</color> " +
                         $"<size=80%>({UiText.HudIncome(run.IncomePreview)})</size>";
            harvest.interactable = canAct;
            grammar.SetLabel(UiText.GrammarButton(run.GrammarActionsLeft));
            grammar.interactable = canAct && run.HasGrammarTarget;
            hint.text = hintText ?? "";
        }

        public void SetVisible(bool v) => hudRoot.gameObject.SetActive(v);
    }

    // ==================================================================== Shop (drawer)

    /// <summary>
    /// Simplified, transparent shop in a full-height drawer that slides in from the right edge.
    /// The handle sits on the drawer's left side and travels with it. A 2×3 card grid: 5 word offers and the joker.
    /// Bar previews use the board's cell size.
    /// </summary>
    public class ShopView : MonoBehaviour
    {
        GameController ctl;
        RectTransform drawer, topRow, bottomRow;
        Button reroll, harvest;
        TextMeshProUGUI handleLabel, gold;
        bool open = true;
        public bool IsOpen => open;

        const float PanelW = 600, Gap = 8, HeaderH = 44;
        const float OpenX = 0f, ClosedX = PanelW + 2f;
        // alpha blends much stronger than the number suggests in this URP 2D setup: 250 ≈ subtle see-through
        static readonly Color PanelColor = new Color32(22, 28, 40, 250);
        static readonly Color CardColor = new Color(1f, 1f, 1f, 0.12f);
        static readonly Color CardDisabled = new Color(1f, 1f, 1f, 0.05f);
        static readonly Color Muted = new Color(1f, 1f, 1f, 0.6f);
        static readonly Color PointsColor = new Color32(255, 236, 160, 255);
        static readonly Color GoldColor = new Color32(246, 196, 70, 255);
        static readonly Color HintColor = new Color32(150, 230, 140, 255);

        public void Init(GameController c, RectTransform canvas)
        {
            ctl = c;
            drawer = Ui.Rect("ShopDrawer", canvas);
            // full screen height, anchored to the right edge
            drawer.anchorMin = new Vector2(1, 0);
            drawer.anchorMax = new Vector2(1, 1);
            drawer.pivot = new Vector2(1, 0.5f);
            drawer.sizeDelta = new Vector2(PanelW, 0);
            drawer.anchoredPosition = new Vector2(OpenX, 0);
            var bg = drawer.gameObject.AddComponent<Image>();
            bg.sprite = Ui.Sprites.pixel;
            bg.color = PanelColor;

            var handle = Ui.Button("Handle", drawer, "", 17, Toggle, PanelColor);
            handle.GetComponent<Image>().sprite = Ui.Sprites.pixel;
            ((RectTransform)handle.transform).Place(new Vector2(0, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(36, 150));
            handleLabel = handle.GetComponentInChildren<TextMeshProUGUI>();
            handleLabel.textWrappingMode = TextWrappingModes.Normal;
            handleLabel.lineSpacing = -20;
            handleLabel.fontStyle = FontStyles.Bold;

            // header: title, gold, reroll, harvest (the open drawer covers the HUD's Ernten button)
            var title = Ui.Text("Title", drawer, UiText.ShopTitle, 22, Color.white);
            title.fontStyle = FontStyles.Bold;
            title.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -8), new Vector2(90, 32));
            gold = Ui.Text("Gold", drawer, "", 20, GoldColor);
            gold.fontStyle = FontStyles.Bold;
            gold.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(104, -8), new Vector2(120, 32));

            harvest = Ui.Button("Harvest", drawer, UiText.Harvest, 18, () => ctl.OnHarvestClicked(), Pal.Gold);
            harvest.GetComponent<Image>().sprite = Ui.Sprites.pixel;
            harvest.GetComponentInChildren<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
            ((RectTransform)harvest.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-Gap, -8), new Vector2(110, 32));
            reroll = Ui.Button("Reroll", drawer, "", 17, () => ctl.OnReroll(), new Color(1, 1, 1, 0.18f));
            reroll.GetComponent<Image>().sprite = Ui.Sprites.pixel;
            ((RectTransform)reroll.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-Gap - 118, -8), new Vector2(180, 32));

            topRow = Row("Top", true);
            bottomRow = Row("Bottom", false);
            UpdateHandleLabel();
        }

        /// <summary>A card row filling the upper or lower half of the drawer below the header.</summary>
        RectTransform Row(string name, bool top)
        {
            var r = Ui.Rect(name, drawer);
            r.anchorMin = new Vector2(0, top ? 0.5f : 0f);
            r.anchorMax = new Vector2(1, top ? 1f : 0.5f);
            // the header takes HeaderH at the top; split the rest evenly
            r.offsetMin = new Vector2(Gap, top ? -HeaderH / 2f + Gap / 2f : Gap);
            r.offsetMax = new Vector2(-Gap, top ? -HeaderH : -HeaderH / 2f - Gap / 2f);
            var hl = Ui.HLayout(r.gameObject, Gap, TextAnchor.UpperLeft);
            hl.childForceExpandWidth = true;
            hl.childForceExpandHeight = true;
            return r;
        }

        public void SetOpen(bool value)
        {
            open = value;
            UpdateHandleLabel();
        }

        public void Toggle() => SetOpen(!open);

        void UpdateHandleLabel() => handleLabel.text = (open ? "»" : "«") + "\nL\na\nd\ne\nn";

        void Update()
        {
            float target = open ? OpenX : ClosedX;
            var p = drawer.anchoredPosition;
            p.x = Mathf.Lerp(p.x, target, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            if (Mathf.Abs(p.x - target) < 0.5f) p.x = target;
            drawer.anchoredPosition = p;
        }

        public void Refresh(RunState run, bool interactive)
        {
            if (run == null) return;
            Ui.Clear(topRow);
            Ui.Clear(bottomRow);
            gold.text = UiText.HudGold(run.Gold);
            reroll.SetLabel(UiText.Reroll(run.RerollPrice));
            reroll.interactable = interactive && run.CanAfford(run.RerollPrice);
            harvest.interactable = interactive;

            int n = run.Cfg.wordOfferCount;
            for (int i = 0; i < n; i++)
            {
                var row = i < 3 ? topRow : bottomRow;
                if (i < run.Shop.Words.Count) WordCard(row, run, run.Shop.Words[i], interactive);
                else EmptyCard(row, UiText.NoMoreWords);
            }
            JokerCard(run, interactive);
        }

        RectTransform Card(RectTransform row, bool enabled, Func<string> tooltip = null)
        {
            var img = Ui.Image("Card", row, enabled ? CardColor : CardDisabled, Ui.Sprites.pixel);
            if (tooltip != null) img.gameObject.AddComponent<UiTooltipTrigger>().Text = tooltip;
            if (!enabled) img.gameObject.AddComponent<CanvasGroup>().alpha = 0.55f;
            return img.rectTransform;
        }

        static TextMeshProUGUI Txt(RectTransform card, string text, float size, float y, float h, Color color, bool bold = false)
        {
            var t = Ui.Text("Text", card, text, size, color, TextAlignmentOptions.Center);
            t.rectTransform.anchorMin = new Vector2(0, 1);
            t.rectTransform.anchorMax = new Vector2(1, 1);
            t.rectTransform.pivot = new Vector2(0.5f, 1);
            t.rectTransform.offsetMin = new Vector2(6, -y - h);
            t.rectTransform.offsetMax = new Vector2(-6, -y);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.enableAutoSizing = true; t.fontSizeMin = 11; t.fontSizeMax = size;
            if (bold) t.fontStyle = FontStyles.Bold;
            return t;
        }

        void EmptyCard(RectTransform row, string text)
        {
            var c = Card(row, false);
            Txt(c, text, 18, 60, 30, Muted);
        }

        void CardButton(RectTransform c, string label, Action onClick, bool interactable, Func<string> tooltip = null)
        {
            var b = Ui.Button("Buy", c, label, 20, onClick, new Color32(86, 150, 70, 230));
            b.GetComponent<Image>().sprite = Ui.Sprites.pixel;
            var rt = (RectTransform)b.transform;
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.offsetMin = new Vector2(10, 8);
            rt.offsetMax = new Vector2(-10, 8 + 32);
            b.GetComponentInChildren<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
            b.interactable = interactable;
            if (tooltip != null) b.gameObject.AddComponent<UiTooltipTrigger>().Text = tooltip;
        }

        /// <summary>Preview of the bar the word takes, at the same cell size as the board, with its outline.</summary>
        void BarPreview(RectTransform card, WordType type, int size, float y)
        {
            float cell = IsoLayout.BoardCellCanvas;
            var area = Ui.Rect("Bar", card);
            area.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -y), new Vector2(size * cell, cell));
            for (int i = 0; i < size; i++)
            {
                var pos = new Vector2((i + 0.5f) * cell, -cell / 2f);
                var bg = Ui.Image("Cell", area, new Color32(122, 84, 54, 150), Ui.Sprites.boardCell, false);
                bg.rectTransform.Place(new Vector2(0, 1), new Vector2(0.5f, 0.5f), pos, Vector2.one * cell);
                var icon = Ui.Image("Icon", area, Color.white, Ui.Sprites.Icon(type), false);
                icon.rectTransform.Place(new Vector2(0, 1), new Vector2(0.5f, 0.5f), pos, Vector2.one * cell * 0.9f);
            }
            // one continuous outline around all cells
            var col = SpriteSet.OutlineColor(type);
            const float t = 2.5f;
            void Edge(Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta)
            {
                var e = Ui.Image("Outline", area, col, Ui.Sprites.pixel, false);
                e.rectTransform.anchorMin = anchorMin;
                e.rectTransform.anchorMax = anchorMax;
                e.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                e.rectTransform.anchoredPosition = Vector2.zero;
                e.rectTransform.sizeDelta = sizeDelta;
            }
            Edge(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, t));
            Edge(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, t));
            Edge(new Vector2(0, 0), new Vector2(0, 1), new Vector2(t, 0));
            Edge(new Vector2(1, 0), new Vector2(1, 1), new Vector2(t, 0));
        }

        void WordCard(RectTransform row, RunState run, WordOffer o, bool interactive)
        {
            bool afford = run.CanAfford(o.Stats.Price);
            bool completes = run.Shop.Completing.Contains(o.Entry.Id);
            var c = Card(row, o.Available && afford, () => UiText.WordCardTip(o) + (completes ? "\n" + UiText.CompletesHint : ""));
            Txt(c, o.NoQuiz ? o.Entry.Es : o.Prompt, 26, 6, 32, Color.white, true);
            Txt(c, $"<color=#{ColorUtility.ToHtmlStringRGB(SpriteSet.TypeColor(o.Entry.Type))}>{UiText.TypeName(o.Entry.Type)}</color>", 15, 38, 18, Color.white);
            Txt(c, $"<color=#{ColorUtility.ToHtmlStringRGB(PointsColor)}>{o.Stats.BasePoints} Pkt</color>   " +
                   $"<color=#{ColorUtility.ToHtmlStringRGB(GoldColor)}>{o.Stats.Gold} Gold</color>", 19, 58, 22, Color.white);
            BarPreview(c, o.Entry.Type, o.Stats.BaseSize, 86);
            if (completes) Txt(c, "★ " + UiText.CompletesShort, 13, 86 + IsoLayout.BoardCellCanvas + 4, 16, HintColor);
            string label = o.Sold ? UiText.Sold : o.Missed ? UiText.Missed : UiText.Price(o.Stats.Price);
            CardButton(c, label, () => ctl.OnBuyWord(o), interactive && o.Available && afford,
                () => o.Available && !run.CanAfford(o.Stats.Price) ? UiText.NotEnoughGold : null);
        }

        void JokerCard(RunState run, bool interactive)
        {
            var j = run.Shop.Joker;
            if (j == null) { Card(bottomRow, false); return; }
            var c = Card(bottomRow, j.Available, () => UiText.JokerCard(j.Type, run.Cfg.jokerDiscount));
            Txt(c, UiText.JokerTitle, 24, 6, 30, GoldColor, true);
            Txt(c, $"<color=#{ColorUtility.ToHtmlStringRGB(SpriteSet.TypeColor(j.Type))}>{UiText.TypeName(j.Type)}</color>", 20, 38, 24, Color.white, true);
            Txt(c, UiText.AttemptsLeft(j.AttemptsLeft), 15, 66, 18, Muted);
            CardButton(c, j.Available ? UiText.JokerEnter : UiText.JokerUsed, () => ctl.OnJoker(), interactive && j.Available);
        }

        public void SetVisible(bool v) => drawer.gameObject.SetActive(v);
    }

    // ==================================================================== Tooltip and toast

    public class TooltipView : MonoBehaviour
    {
        RectTransform canvas, panel;
        TextMeshProUGUI text;
        Camera cam;

        public void Init(RectTransform canvasRect, Camera camera)
        {
            canvas = canvasRect; cam = camera;
            var img = Ui.Panel("Tooltip", canvas, new Color32(255, 250, 235, 245));
            img.raycastTarget = false;
            panel = img.rectTransform;
            panel.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 1), Vector2.zero, new Vector2(260, 40));
            Ui.VLayout(img.gameObject, 0, new RectOffset(8, 8, 6, 6), TextAnchor.UpperLeft);
            var fitter = img.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            text = Ui.Text("Text", panel, "", 13);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            panel.gameObject.SetActive(false);
            UiTooltipTrigger.Show = Show;
            UiTooltipTrigger.Hide = Hide;
        }

        public void Show(string s)
        {
            text.text = s;
            panel.gameObject.SetActive(true);
            panel.SetAsLastSibling();
            Follow();
        }

        public void Hide() { if (panel != null) panel.gameObject.SetActive(false); }

        void LateUpdate() { if (panel.gameObject.activeSelf) Follow(); }

        void Follow()
        {
            if (Mouse.current == null) return;
            var mp = Mouse.current.position.ReadValue();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, mp, cam, out var local);
            var size = panel.rect.size;
            var half = canvas.rect.size / 2f;
            var pos = local + new Vector2(14, -14);
            if (pos.x + size.x > half.x) pos.x = local.x - size.x - 10;
            if (pos.y - size.y < -half.y) pos.y = local.y + size.y + 10;
            panel.anchoredPosition = pos;
        }
    }

    public class ToastView : MonoBehaviour
    {
        TextMeshProUGUI text;
        Image bg;
        float hideAt;

        public void Init(RectTransform canvas)
        {
            bg = Ui.Panel("Toast", canvas, new Color32(60, 45, 30, 225));
            bg.raycastTarget = false;
            bg.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-90, 34), new Vector2(380, 40));
            Ui.VLayout(bg.gameObject, 0, new RectOffset(10, 10, 6, 6), TextAnchor.MiddleCenter);
            var fitter = bg.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            text = Ui.Text("Text", bg.transform, "", 14, Color.white, TextAlignmentOptions.Center);
            bg.gameObject.SetActive(false);
        }

        public void Show(string msg, float seconds = 3.5f)
        {
            if (string.IsNullOrEmpty(msg)) return;
            text.text = msg;
            bg.gameObject.SetActive(true);
            bg.transform.SetAsLastSibling();
            hideAt = Time.time + seconds;
        }

        void Update()
        {
            if (bg.gameObject.activeSelf && Time.time > hideAt) bg.gameObject.SetActive(false);
        }
    }

    // ==================================================================== Generic modal dialog

    public class Dialog : MonoBehaviour
    {
        RectTransform dim, panel;
        readonly List<TMP_InputField> inputs = new List<TMP_InputField>();
        TMP_InputField lastInput;
        int lastCaret;
        public bool IsOpen => dim != null && dim.gameObject.activeSelf;

        public void Init(RectTransform canvas)
        {
            var d = Ui.Image("DialogDim", canvas, Pal.Dim);
            dim = d.rectTransform;
            dim.Stretch();
            var p = Ui.Panel("Dialog", dim);
            panel = p.rectTransform;
            panel.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(460, 200));
            Ui.VLayout(p.gameObject, 4, new RectOffset(16, 16, 10, 12), TextAnchor.UpperCenter);
            var fitter = p.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            dim.gameObject.SetActive(false);
        }

        public void Open(string title, float width = 460)
        {
            Ui.Clear(panel);
            inputs.Clear();
            lastInput = null;
            panel.sizeDelta = new Vector2(width, panel.sizeDelta.y);
            dim.gameObject.SetActive(true);
            dim.SetAsLastSibling();
            var t = AddText(title, 20);
            t.fontStyle = FontStyles.Bold;
        }

        public void Close()
        {
            dim.gameObject.SetActive(false);
            Ui.Clear(panel);
            inputs.Clear();
        }

        public TextMeshProUGUI AddText(string s, float size = 15, Color? color = null) =>
            Ui.Text("Text", panel, s, size, color ?? Pal.Text, TextAlignmentOptions.Center);

        public TMP_InputField AddInput(string label, Action onSubmit, float labelWidth = 0)
        {
            var row = Ui.Rect("Row", panel);
            Ui.HLayout(row.gameObject, 8, TextAnchor.MiddleCenter);
            Ui.Size(row, -1, 27);
            if (!string.IsNullOrEmpty(label))
            {
                var l = Ui.Text("Label", row, label, 14, Pal.Text, TextAlignmentOptions.MidlineRight);
                Ui.Size(l, labelWidth > 0 ? labelWidth : 150, 27);
            }
            var input = Ui.Input("Input", row, 16);
            Ui.Size(input, 220, 27);
            input.onSubmit.AddListener(_ => onSubmit?.Invoke());
            inputs.Add(input);
            return input;
        }

        public void AddAccentButtons()
        {
            var row = Ui.Rect("Accents", panel);
            Ui.HLayout(row.gameObject, 4, TextAnchor.MiddleCenter);
            Ui.Size(row, -1, 26);
            foreach (var ch in new[] { "á", "é", "í", "ó", "ú", "ñ", "ü", "¿", "¡" })
            {
                string c = ch;
                var b = Ui.Button("Accent " + c, row, c, 15, () => Insert(c), Pal.ButtonAlt);
                Ui.Size(b, 28, 26);
                b.navigation = new Navigation { mode = Navigation.Mode.None };
            }
        }

        /// <summary>Practice mode: hidden answer line, shown by <see cref="AddRevealButton"/>.</summary>
        public TextMeshProUGUI AddAnswerLine()
        {
            var t = AddText("", 15, new Color32(50, 90, 150, 255));
            t.fontStyle = FontStyles.Bold;
            t.gameObject.SetActive(false);
            return t;
        }

        /// <summary>
        /// Toggles the answer line. <paramref name="answer"/> is evaluated every time the answer is shown,
        /// so the joker can show a different random word each time.
        /// </summary>
        public Button AddRevealButton(RectTransform row, TextMeshProUGUI answerLine, Func<string> answer)
        {
            bool shown = false;
            Button b = null;
            b = AddButton(row, UiText.ShowAnswer, () =>
            {
                shown = !shown;
                if (shown) answerLine.text = answer();
                answerLine.gameObject.SetActive(shown);
                b.SetLabel(shown ? UiText.HideAnswer : UiText.ShowAnswer);
            }, Pal.ButtonAlt, 160);
            return b;
        }

        public RectTransform AddButtonRow()
        {
            var row = Ui.Rect("Buttons", panel);
            Ui.HLayout(row.gameObject, 10, TextAnchor.MiddleCenter);
            Ui.Size(row, -1, 32);
            return row;
        }

        public Button AddButton(RectTransform row, string label, Action onClick, Color? color = null, float width = 140)
        {
            var b = Ui.Button(label, row, label, 15, onClick, color);
            Ui.Size(b, width, 32);
            return b;
        }

        public void Focus(TMP_InputField input)
        {
            if (input == null || !input.interactable) return;
            StartCoroutine(FocusRoutine(input, input.text.Length));
        }

        IEnumerator FocusRoutine(TMP_InputField input, int caret)
        {
            yield return null;
            if (input == null) yield break;
            input.Select();
            input.ActivateInputField();
            yield return null;
            if (input == null) yield break;
            input.stringPosition = Mathf.Clamp(caret, 0, input.text.Length);
            lastInput = input;
            lastCaret = input.stringPosition;
        }

        void Insert(string c)
        {
            var target = lastInput;
            if (target == null || !target.interactable)
                target = inputs.Find(i => i != null && i.interactable);
            if (target == null) return;
            int pos = target == lastInput ? Mathf.Clamp(lastCaret, 0, target.text.Length) : target.text.Length;
            target.text = target.text.Insert(pos, c);
            StartCoroutine(FocusRoutine(target, pos + c.Length));
        }

        void Update()
        {
            if (!IsOpen) return;
            foreach (var i in inputs)
                if (i != null && i.isFocused)
                {
                    lastInput = i;
                    lastCaret = i.stringPosition;
                }
        }
    }

    // ==================================================================== Dictionary search (practice mode only)

    /// <summary>
    /// Button in the lower-left corner that opens a dictionary panel covering the left half of the screen.
    /// Looks up German or Spanish words and shows every field of the matching vocabulary entries.
    /// </summary>
    public class SearchView : MonoBehaviour
    {
        const int MaxResults = 40;
        const float HeaderH = 44, InputH = 30, StatusH = 22, Pad = 10;

        Func<VocabularyDatabase> vocab;
        RectTransform root, panel, content;
        Button openButton;
        TMP_InputField input;
        TextMeshProUGUI status;
        ScrollRect scroll;

        public bool IsOpen => panel != null && panel.gameObject.activeSelf;

        public void Init(Func<VocabularyDatabase> vocabulary, RectTransform canvas)
        {
            vocab = vocabulary;
            root = Ui.Rect("Search", canvas).Stretch();

            openButton = Ui.Button("SearchButton", root, UiText.SearchButton, 16, Open, Pal.ButtonAlt);
            ((RectTransform)openButton.transform).Place(Vector2.zero, Vector2.zero, new Vector2(6, 6), new Vector2(130, 34));
            openButton.GetComponentInChildren<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

            // panel: from the left edge to the middle of the screen, full height
            var bg = Ui.Panel("SearchPanel", root);
            panel = bg.rectTransform;
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = new Vector2(0.5f, 1f);
            panel.pivot = new Vector2(0, 0.5f);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;

            var title = Ui.Text("Title", panel, UiText.SearchTitle, 22, Pal.Text, TextAlignmentOptions.MidlineLeft);
            title.fontStyle = FontStyles.Bold;
            TopStrip(title.rectTransform, Pad, HeaderH, Pad, 120);

            var close = Ui.Button("Close", panel, UiText.Close, 15, Close, Pal.ButtonDanger);
            ((RectTransform)close.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-Pad, -Pad + 2), new Vector2(110, 32));

            input = Ui.Input("Query", panel, 16, UiText.SearchPlaceholder);
            TopStrip((RectTransform)input.transform, HeaderH + 4, InputH, Pad, Pad);
            input.onValueChanged.AddListener(_ => Refresh());

            status = Ui.Text("Status", panel, UiText.SearchEmptyQuery, 13, Pal.TextMuted, TextAlignmentOptions.MidlineLeft);
            TopStrip(status.rectTransform, HeaderH + InputH + 8, StatusH, Pad, Pad);

            BuildScroll(HeaderH + InputH + StatusH + 12);
            panel.gameObject.SetActive(false);
            root.gameObject.SetActive(false);
        }

        /// <summary>Stretches <paramref name="rt"/> horizontally across the panel at a fixed distance from the top.</summary>
        static void TopStrip(RectTransform rt, float top, float height, float left, float right)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(left, -top - height);
            rt.offsetMax = new Vector2(-right, -top);
        }

        void BuildScroll(float top)
        {
            var area = Ui.Image("Results", panel, new Color(0, 0, 0, 0));   // transparent, catches the scroll wheel
            var rt = area.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(Pad, Pad);
            rt.offsetMax = new Vector2(-Pad, -top);

            var viewport = Ui.Rect("Viewport", rt).Stretch();
            viewport.gameObject.AddComponent<RectMask2D>();

            content = Ui.Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            Ui.VLayout(content.gameObject, 6, new RectOffset(0, 0, 0, 4), TextAnchor.UpperLeft);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll = area.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 35;
        }

        /// <summary>The dictionary is only offered during a run in practice mode (not in exam mode).</summary>
        public void SetAvailable(bool available)
        {
            if (!available) Close();
            root.gameObject.SetActive(available);
        }

        public void Open()
        {
            panel.gameObject.SetActive(true);
            openButton.gameObject.SetActive(false);
            Refresh();
            input.Select();
            input.ActivateInputField();
        }

        public void Close()
        {
            if (input.isFocused) input.DeactivateInputField();
            panel.gameObject.SetActive(false);
            openButton.gameObject.SetActive(true);
        }

        void Refresh()
        {
            Ui.Clear(content);
            string q = input.text?.Trim() ?? "";
            var db = vocab();
            if (q.Length == 0 || db == null) { status.text = UiText.SearchEmptyQuery; return; }
            var hits = db.Search(q, MaxResults);
            status.text = hits.Count == 0 ? UiText.SearchNoResults(q) : UiText.SearchResultCount(hits.Count, MaxResults);
            foreach (var e in hits)
            {
                var card = Ui.Image("Entry", content, Pal.Card, Ui.Sprites.button, false);
                Ui.VLayout(card.gameObject, 0, new RectOffset(10, 10, 6, 8), TextAnchor.UpperLeft);
                var t = Ui.Text("Text", card.transform, UiText.VocabDetails(e), 14, Pal.Text, TextAlignmentOptions.TopLeft);
                t.textWrappingMode = TextWrappingModes.Normal;
            }
            scroll.verticalNormalizedPosition = 1;
        }
    }

    // ==================================================================== Magnifier ("Lupe")

    /// <summary>
    /// Button in the lower-left corner that picks up the magnifier. While held, a magnifier icon follows the cursor;
    /// the controller shows the structures of the hovered word in the tooltip.
    /// </summary>
    public class MagnifierView : MonoBehaviour
    {
        RectTransform root, cursorIcon, canvas;
        Button button;
        Camera cam;
        bool held, enabledNow;

        public void Init(GameController ctl, RectTransform canvasRect, Camera camera)
        {
            canvas = canvasRect; cam = camera;
            root = Ui.Rect("Magnifier", canvas).Stretch();

            button = Ui.Button("MagnifierButton", root, UiText.MagnifierButton, 16, ctl.OnMagnifierClicked, Pal.ButtonAlt);
            button.GetComponentInChildren<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
            button.gameObject.AddComponent<UiTooltipTrigger>().Text = () =>
                held ? null : enabledNow ? UiText.MagnifierActiveTip : UiText.MagnifierTip;

            if (Ui.Sprites.magnifier != null)
            {
                var icon = Ui.Image("MagnifierCursor", root, Color.white, Ui.Sprites.magnifier, false);
                icon.preserveAspect = true;
                cursorIcon = icon.rectTransform;
                // pivot at the lens centre, so the lens sits on the hovered word
                cursorIcon.Place(new Vector2(0.5f, 0.5f), new Vector2(0.4f, 0.6f), Vector2.zero, new Vector2(40, 40));
                cursorIcon.gameObject.SetActive(false);
            }
            root.gameObject.SetActive(false);
        }

        public void SetVisible(bool v)
        {
            root.gameObject.SetActive(v);
            if (!v) SetHeld(false);
        }

        /// <param name="enabled">The magnifier can be picked up (planning mode, at least one structure on the field).</param>
        /// <param name="searchVisible">The dictionary button takes the corner; the magnifier sits to its right.</param>
        public void Refresh(bool enabled, bool isHeld, bool searchVisible)
        {
            enabledNow = enabled;
            SetHeld(isHeld);
            button.interactable = enabled || isHeld;
            ((RectTransform)button.transform).Place(Vector2.zero, Vector2.zero, new Vector2(searchVisible ? 142 : 6, 6), new Vector2(110, 34));
        }

        void SetHeld(bool value)
        {
            held = value;
            button.SetLabel(held ? UiText.MagnifierPutDown : UiText.MagnifierButton);
            if (cursorIcon != null)
            {
                cursorIcon.gameObject.SetActive(held);
                if (held) { cursorIcon.SetAsLastSibling(); Follow(); }
            }
        }

        void LateUpdate() { if (held) Follow(); }

        void Follow()
        {
            if (cursorIcon == null || Mouse.current == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, Mouse.current.position.ReadValue(), cam, out var local);
            cursorIcon.anchoredPosition = local;
        }
    }

    // ==================================================================== Menus

    public class MainMenuView : MonoBehaviour
    {
        RectTransform root;
        TextMeshProUGUI board;
        Toggle examToggle;

        public void Init(GameController ctl, RectTransform canvas)
        {
            var dim = Ui.Image("MainMenu", canvas, new Color32(20, 30, 50, 110));
            root = dim.rectTransform;
            root.Stretch();
            var panel = Ui.Panel("Panel", root);
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560, 416));
            Ui.VLayout(panel.gameObject, 8, new RectOffset(20, 20, 16, 16), TextAnchor.UpperCenter);
            var title = Ui.Text("Title", panel.transform, UiText.Title, 38, Pal.Gold, TextAlignmentOptions.Center);
            title.fontStyle = FontStyles.Bold;
            Ui.Text("Subtitle", panel.transform, UiText.Subtitle, 15, Pal.TextMuted, TextAlignmentOptions.Center);
            var b = Ui.Button("NewGame", panel.transform, UiText.NewGame, 20, ctl.NewGame);
            Ui.Size(b, -1, 40);
            examToggle = CreateToggle(panel.transform, UiText.ExamModeToggle, ctl.ExamMode, ctl.SetExamMode);
            Ui.Text("BoardTitle", panel.transform, UiText.LeaderboardTitle, 17, Pal.Text, TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
            board = Ui.Text("Board", panel.transform, "", 12, Pal.Text, TextAlignmentOptions.TopLeft);
            board.textWrappingMode = TextWrappingModes.NoWrap;
            Ui.Size(board, -1, 132);
            var q = Ui.Button("Quit", panel.transform, UiText.Quit, 16, ctl.Quit, Pal.ButtonDanger);
            Ui.Size(q, -1, 30);
        }

        static Toggle CreateToggle(Transform parent, string label, bool value, Action<bool> onChange)
        {
            var row = Ui.Rect("ExamModeToggle", parent);
            Ui.Size(row, -1, 28);
            var hit = row.gameObject.AddComponent<Image>();      // whole row is clickable
            hit.color = new Color(0, 0, 0, 0);
            var toggle = row.gameObject.AddComponent<Toggle>();

            var box = Ui.Image("Box", row, Color.white, Ui.Sprites.button);
            box.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-150, 0), new Vector2(24, 24));
            box.gameObject.AddComponent<Outline>().effectColor = Pal.PanelBorder;
            var check = Ui.Image("Check", box.transform, Pal.Button, Ui.Sprites.button, false);
            check.rectTransform.Stretch(4);

            var text = Ui.Text("Label", row, label, 16, Pal.Text, TextAlignmentOptions.MidlineLeft);
            text.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 0.5f), new Vector2(-130, 0), new Vector2(320, 28));

            toggle.targetGraphic = box;
            toggle.graphic = check;
            toggle.isOn = value;
            toggle.onValueChanged.AddListener(v => onChange(v));
            return toggle;
        }

        public void Show(List<LeaderboardEntry> top, bool examMode)
        {
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            examToggle.SetIsOnWithoutNotify(examMode);
            if (top.Count == 0) board.text = UiText.LeaderboardEmpty;
            else
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < top.Count; i++) sb.AppendLine(UiText.LeaderboardRow(i + 1, top[i]));
                board.text = sb.ToString();
            }
        }

        public void Hide() => root.gameObject.SetActive(false);
    }

    public class GameOverView : MonoBehaviour
    {
        RectTransform root;
        TextMeshProUGUI stats;

        public void Init(GameController ctl, RectTransform canvas)
        {
            var dim = Ui.Image("GameOver", canvas, Pal.Dim);
            root = dim.rectTransform;
            root.Stretch();
            var panel = Ui.Panel("Panel", root);
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 260));
            Ui.VLayout(panel.gameObject, 10, new RectOffset(20, 20, 16, 16), TextAnchor.UpperCenter);
            var t = Ui.Text("Title", panel.transform, UiText.GameOverTitle, 30, Pal.Bad, TextAlignmentOptions.Center);
            t.fontStyle = FontStyles.Bold;
            stats = Ui.Text("Stats", panel.transform, "", 16, Pal.Text, TextAlignmentOptions.Center);
            var b = Ui.Button("Menu", panel.transform, UiText.BackToMenu, 18, ctl.BackToMenu);
            Ui.Size(b, -1, 38);
            root.gameObject.SetActive(false);
        }

        public void Show(RunState run)
        {
            stats.text = UiText.GameOverStats(run.Rank, run.Round, run.WordsPlanted, run.GrammarLearned);
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
        }

        public void Hide() => root.gameObject.SetActive(false);
    }

    // ==================================================================== Debug overlay (F1)

    public class DebugOverlay : MonoBehaviour
    {
        RectTransform root;
        TextMeshProUGUI structures;

        public void Init(GameController ctl, RectTransform canvas)
        {
            var panel = Ui.Panel("Debug", canvas, new Color32(30, 30, 40, 230));
            root = panel.rectTransform;
            root.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8, -20), new Vector2(230, 380));
            Ui.VLayout(panel.gameObject, 4, new RectOffset(8, 8, 8, 8), TextAnchor.UpperCenter);
            Ui.Text("Title", root, UiText.DebugTitle, 14, Color.white, TextAlignmentOptions.Center);
            void Btn(string label, Action a)
            {
                var b = Ui.Button(label, root, label, 12, a, new Color32(80, 80, 100, 255));
                Ui.Size(b, -1, 22);
            }
            Btn(UiText.DebugGold, ctl.DebugAddGold);
            Btn(UiText.DebugGrammar, ctl.DebugAddGrammar);
            Btn(UiText.DebugRoundDown, () => ctl.DebugChangeRound(-1));
            Btn(UiText.DebugRoundUp, () => ctl.DebugChangeRound(+1));
            Btn(UiText.DebugPass, () => ctl.DebugForceHarvest(ForcedOutcome.Pass));
            Btn(UiText.DebugFail, () => ctl.DebugForceHarvest(ForcedOutcome.Fail));
            Btn(UiText.DebugRefill, ctl.DebugRefillShop);
            Btn(UiText.DebugReload, ctl.DebugReloadVocabulary);
            Btn(UiText.DebugLabels, ctl.DebugToggleLabels);
            Ui.Text("StructTitle", root, UiText.DebugStructuresTitle, 13, Color.white, TextAlignmentOptions.Left).fontStyle = FontStyles.Bold;
            structures = Ui.Text("Structures", root, UiText.DebugNoStructures, 11, new Color(1, 1, 1, 0.85f), TextAlignmentOptions.TopLeft);
            structures.textWrappingMode = TextWrappingModes.Normal;
            structures.enableAutoSizing = true; structures.fontSizeMin = 7; structures.fontSizeMax = 11;
            Ui.Size(structures, -1, 110);
            root.gameObject.SetActive(false);
        }

        /// <summary>Lists every detected structure with its direction and words.</summary>
        public void Refresh(RunState run)
        {
            if (structures == null) return;
            var list = run?.Grid != null ? run.Analysis.Structures : null;
            structures.text = list == null || list.Count == 0
                ? UiText.DebugNoStructures
                : string.Join("\n", list.OrderByDescending(x => (int)x.Category).Select(UiText.StructureLine));
        }

        public void Toggle()
        {
            root.gameObject.SetActive(!root.gameObject.activeSelf);
            root.SetAsLastSibling();
        }
    }
}
