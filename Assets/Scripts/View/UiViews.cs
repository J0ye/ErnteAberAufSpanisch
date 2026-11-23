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
        Button harvest;
        GameController ctl;
        RectTransform hudRoot;

        public void Init(GameController c, RectTransform canvasRect)
        {
            ctl = c;
            hudRoot = Ui.Rect("HudRoot", canvasRect).Stretch();
            var canvas = hudRoot;
            var panel = Ui.Panel("HUD", canvas);
            panel.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(6, -6), new Vector2(408, 54));
            Ui.VLayout(panel.gameObject, 0, new RectOffset(10, 10, 4, 4), TextAnchor.MiddleLeft);
            line1 = Ui.Text("Line1", panel.transform, "", 15);
            line2 = Ui.Text("Line2", panel.transform, "", 17);
            line2.fontStyle = FontStyles.Bold;
            foreach (var t in new[] { line1, line2 })
            {
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.enableAutoSizing = true; t.fontSizeMin = 11; t.fontSizeMax = t.fontSize;
            }

            harvest = Ui.Button("Harvest", canvas, UiText.Harvest, 22, () => ctl.OnHarvestClicked(), Pal.Gold);
            ((RectTransform)harvest.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(420, -6), new Vector2(128, 54));
            harvest.GetComponentInChildren<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

            hint = Ui.Text("Hint", canvas, "", 14, Color.white, TextAlignmentOptions.Center);
            hint.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-90, 4), new Vector2(560, 22));
            hint.outlineWidth = 0.2f;
            hint.outlineColor = new Color32(40, 30, 20, 255);
        }

        public void Refresh(RunState run, bool canHarvest, string hintText)
        {
            if (run == null || run.Grid == null) return;
            double s = run.RoundScorePreview, t = run.Target;
            string scoreColor = ScoreCalculator.Reaches(s, t) ? "#3C963C" : "#BE463C";
            line1.text = $"{UiText.HudRound(run.Round)}  ·  {UiText.HudRank(run.Rank)}  ·  {UiText.HudActions(run.ActionsLeft, run.MaxActions)}  ·  " +
                         $"<color={(run.ScarecrowAvailable ? "#3A2A1E" : "#82705F")}>{UiText.HudScarecrow(run.ScarecrowAvailable)}</color>";
            line2.text = $"<color={scoreColor}>{UiText.HudScore(s, t)}</color>   <color=#B07F10>{UiText.HudGold(run.Gold)}</color> " +
                         $"<size=80%>({UiText.HudIncome(run.IncomePreview)})</size>";
            harvest.interactable = canHarvest;
            hint.text = hintText ?? "";
        }

        public void SetVisible(bool v) => hudRoot.gameObject.SetActive(v);
    }

    // ==================================================================== Shop (drawer)

    /// <summary>
    /// Simplified, transparent shop in a full-height drawer that slides in from the right edge.
    /// The handle sits on the drawer's left side and travels with it. Previews use the board's cell size.
    /// </summary>
    public class ShopView : MonoBehaviour
    {
        GameController ctl;
        RectTransform drawer, wordRow, otherRow;
        Button reroll, harvest;
        TextMeshProUGUI handleLabel, gold;
        bool open = true;
        public bool IsOpen => open;

        const float PanelW = 600, Gap = 8, WordCardH = 276, HeaderH = 44;
        static float WordCardW => (PanelW - 4 * Gap) / 3f;
        const float OpenX = 0f, ClosedX = PanelW + 2f;
        // alpha blends much stronger than the number suggests in this URP 2D setup: 250 ≈ subtle see-through
        static readonly Color PanelColor = new Color32(22, 28, 40, 250);
        static readonly Color CardColor = new Color(1f, 1f, 1f, 0.12f);
        static readonly Color CardDisabled = new Color(1f, 1f, 1f, 0.05f);
        static readonly Color Muted = new Color(1f, 1f, 1f, 0.6f);
        static readonly Color PointsColor = new Color32(255, 236, 160, 255);
        static readonly Color GoldColor = new Color32(246, 196, 70, 255);

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

            wordRow = Row("Words", -HeaderH, WordCardH);
            otherRow = Row("Other", -HeaderH - WordCardH - Gap, 0);
            // the second row stretches to the bottom of the screen
            otherRow.anchorMin = new Vector2(0, 0);
            otherRow.anchorMax = new Vector2(1, 1);
            otherRow.offsetMin = new Vector2(Gap, Gap);
            otherRow.offsetMax = new Vector2(-Gap, -HeaderH - WordCardH - Gap);
            UpdateHandleLabel();
        }

        RectTransform Row(string name, float y, float h)
        {
            var r = Ui.Rect(name, drawer);
            r.anchorMin = new Vector2(0, 1);
            r.anchorMax = new Vector2(1, 1);
            r.pivot = new Vector2(0.5f, 1);
            r.offsetMin = new Vector2(Gap, y - h);
            r.offsetMax = new Vector2(-Gap, y);
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
            Ui.Clear(wordRow);
            Ui.Clear(otherRow);
            gold.text = UiText.HudGold(run.Gold);
            reroll.SetLabel(UiText.Reroll(run.RerollPrice));
            reroll.interactable = interactive && run.CanAfford(run.RerollPrice);
            harvest.interactable = interactive;

            for (int i = 0; i < run.Cfg.wordOfferCount; i++)
            {
                if (i < run.Shop.Words.Count) WordCard(run, run.Shop.Words[i], interactive);
                else EmptyCard(UiText.NoMoreWords);
            }
            JokerCard(run, interactive);
            foreach (var o in run.Shop.Upgrades) UpgradeCard(run, o, interactive);
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
            t.enableAutoSizing = true; t.fontSizeMin = 12; t.fontSizeMax = size;
            if (bold) t.fontStyle = FontStyles.Bold;
            return t;
        }

        void EmptyCard(string text)
        {
            var c = Card(wordRow, false);
            Txt(c, text, 18, 100, 30, Muted);
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
            rt.offsetMax = new Vector2(-10, 8 + 34);
            b.GetComponentInChildren<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
            b.interactable = interactable;
            if (tooltip != null) b.gameObject.AddComponent<UiTooltipTrigger>().Text = tooltip;
        }

        /// <summary>Preview of the space the word takes, at the same cell size as the board.</summary>
        void ShapePreview(RectTransform card, WordType type, ShapeFamily family, int size, float y, float h)
        {
            var area = Ui.Rect("Shape", card);
            area.anchorMin = new Vector2(0, 1);
            area.anchorMax = new Vector2(1, 1);
            area.pivot = new Vector2(0.5f, 1);
            area.offsetMin = new Vector2(4, -y - h);
            area.offsetMax = new Vector2(-4, -y);
            var cells = Shapes.LocalCells(family, size, 0);
            int minX = cells.Min(p => p.x), maxX = cells.Max(p => p.x), minY = cells.Min(p => p.y), maxY = cells.Max(p => p.y);
            float cell = IsoLayout.BoardCellCanvas;
            var mid = new Vector2((minX + maxX) / 2f, (minY + maxY) / 2f);
            foreach (var p in cells)
            {
                var pos = (new Vector2(p.x, p.y) - mid) * cell;
                var bg = Ui.Image("Cell", area, new Color32(122, 84, 54, 150), Ui.Sprites.boardCell, false);
                bg.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, Vector2.one * cell);
                var icon = Ui.Image("Icon", area, Color.white, Ui.Sprites.Icon(type), false);
                icon.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, Vector2.one * cell * 0.9f);
            }
        }

        void WordCard(RunState run, WordOffer o, bool interactive)
        {
            bool afford = run.CanAfford(o.Stats.Price);
            var c = Card(wordRow, o.Available && afford);
            Txt(c, o.Prompt, 28, 6, 34, Color.white, true);
            Txt(c, $"<color=#{ColorUtility.ToHtmlStringRGB(PointsColor)}>{o.Stats.BasePoints} Pkt</color>   " +
                   $"<color=#{ColorUtility.ToHtmlStringRGB(GoldColor)}>{o.Stats.Gold} Gold</color>", 21, 40, 24, Color.white);
            // 5 cells is the tallest shape (corner family) → 5 × cell = 160 canvas units
            ShapePreview(c, o.Entry.Type, o.Family, o.Stats.UnmoddedSize, 66, 5 * IsoLayout.BoardCellCanvas);
            string label = o.Sold ? UiText.Sold : o.Missed ? UiText.Missed : UiText.Price(o.Stats.Price);
            CardButton(c, label, () => ctl.OnBuyWord(o), interactive && o.Available && afford,
                () => o.Available && !run.CanAfford(o.Stats.Price) ? UiText.NotEnoughGold : null);
        }

        void JokerCard(RunState run, bool interactive)
        {
            var j = run.Shop.Joker;
            if (j == null) { Card(otherRow, false); return; }
            var c = Card(otherRow, j.Available, () => UiText.JokerCard(j.Type, run.Cfg.jokerDiscount));
            Txt(c, $"{UiText.JokerTitle}: <color=#{ColorUtility.ToHtmlStringRGB(SpriteSet.TypeColor(j.Type))}>{UiText.TypeName(j.Type)}</color>",
                22, 6, 28, GoldColor, true);
            Txt(c, UiText.AttemptsLeft(j.AttemptsLeft), 15, 34, 18, Muted);
            CardButton(c, j.Available ? UiText.JokerEnter : UiText.JokerUsed, () => ctl.OnJoker(), interactive && j.Available);
        }

        void UpgradeCard(RunState run, UpgradeOffer o, bool interactive)
        {
            string why = run.WhyCannotBuy(o);
            var c = Card(otherRow, why == null, () => UiText.OfferEffect(o, run.Cfg));
            bool rare = o.Kind == OfferKind.XMult || o.Kind == OfferKind.FieldExpansion || o.Kind == OfferKind.ExtraAction;
            Txt(c, UiText.OfferName(o), 22, 6, 28, rare ? new Color32(220, 170, 255, 255) : Color.white, true);
            if (o.Kind == OfferKind.ArticleMod || o.Kind == OfferKind.ConjugationMod)
            {
                var icon = Ui.Image("Icon", c, Color.white, o.Kind == OfferKind.ArticleMod ? Ui.Sprites.articleIcon : Ui.Sprites.starIcon, false);
                icon.preserveAspect = true;
                icon.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -34), new Vector2(20, 20));
            }
            CardButton(c, o.Sold ? UiText.Sold : UiText.Price(run.PriceOf(o)), () => ctl.OnBuyUpgrade(o), interactive && why == null,
                () => run.WhyCannotBuy(o));
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
            stats.text = UiText.GameOverStats(run.Rank, run.Round, run.WordsPlanted, run.ModsLearned);
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
        }

        public void Hide() => root.gameObject.SetActive(false);
    }

    // ==================================================================== Debug overlay (F1)

    public class DebugOverlay : MonoBehaviour
    {
        RectTransform root;

        public void Init(GameController ctl, RectTransform canvas)
        {
            var panel = Ui.Panel("Debug", canvas, new Color32(30, 30, 40, 230));
            root = panel.rectTransform;
            root.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8, -20), new Vector2(170, 300));
            Ui.VLayout(panel.gameObject, 4, new RectOffset(8, 8, 8, 8), TextAnchor.UpperCenter);
            Ui.Text("Title", root, UiText.DebugTitle, 14, Color.white, TextAlignmentOptions.Center);
            void Btn(string label, Action a)
            {
                var b = Ui.Button(label, root, label, 12, a, new Color32(80, 80, 100, 255));
                Ui.Size(b, -1, 26);
            }
            Btn(UiText.DebugGold, ctl.DebugAddGold);
            Btn(UiText.DebugRoundDown, () => ctl.DebugChangeRound(-1));
            Btn(UiText.DebugRoundUp, () => ctl.DebugChangeRound(+1));
            Btn(UiText.DebugPass, () => ctl.DebugForceHarvest(ForcedOutcome.Pass));
            Btn(UiText.DebugFail, () => ctl.DebugForceHarvest(ForcedOutcome.Fail));
            Btn(UiText.DebugRefill, ctl.DebugRefillShop);
            Btn(UiText.DebugReload, ctl.DebugReloadVocabulary);
            Btn(UiText.DebugLabels, ctl.DebugToggleLabels);
            root.gameObject.SetActive(false);
        }

        public void Toggle()
        {
            root.gameObject.SetActive(!root.gameObject.activeSelf);
            root.SetAsLastSibling();
        }
    }
}
