using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Cosecha.View
{
    /// <summary>Sorting orders. Ground objects are painter-sorted by screen y; the sky boards sit above the ground.</summary>
    public static class Sorting
    {
        public const int Sky = -200, Clouds = -150, Mesa = -100, Tiles = -50, Depth = 1000;
        public const int BoardBg = 10000, BoardCells = 10001, BoardProps = 10002, BoardCrops = 10003, BoardGlow = 10004,
            Signs = 11000, Held = 12000, HeldSigns = 12500, Labels = 12900, Floating = 13000;

        public static int ByY(float worldY, int baseOrder = Depth) => baseOrder - Mathf.RoundToInt(worldY * IsoLayout.PPU);
        /// <summary>Signs: two orders per pixel row (sign, then its text/icons).</summary>
        public static int SignByY(float worldY, int baseOrder = Signs) => baseOrder - 2 * Mathf.RoundToInt(worldY * IsoLayout.PPU);
    }

    public class SkyView : MonoBehaviour
    {
        SpriteSet sprites;
        BalanceConfig cfg;
        float nextCloud;
        readonly List<(Transform t, float speed)> clouds = new List<(Transform, float)>();

        public void Init(SpriteSet s, BalanceConfig c)
        {
            sprites = s; cfg = c;
            World.Sprite("Sky", transform, s.sky, Sorting.Sky);
            World.Sprite("Mesa", transform, s.mesa, Sorting.Mesa);
            var house = World.Sprite("House", transform, s.house, 0);
            house.transform.position = IsoLayout.World(IsoLayout.HouseBottomLeftPx);
            house.sortingOrder = Sorting.ByY(house.transform.position.y);
            SpawnCloud(Random.Range(-12f, -4f));
            SpawnCloud(Random.Range(0f, 8f));
            SpawnCloud(Random.Range(8f, 14f));
            nextCloud = Time.time + Random.Range(cfg.cloudSpawnMinSeconds, cfg.cloudSpawnMaxSeconds);
        }

        void SpawnCloud(float x)
        {
            int size = Random.Range(0, 3);
            var sr = World.Sprite("Cloud", transform, sprites.Cloud(size), Sorting.Clouds + size);
            sr.transform.position = new Vector3(x, Random.Range(0.6f, 7.8f), 0);
            float speed = Random.Range(cfg.cloudSpeedMin, cfg.cloudSpeedMax) * (1f - 0.12f * size);
            clouds.Add((sr.transform, speed));
        }

        void Update()
        {
            if (cfg == null) return;
            if (Time.time >= nextCloud)
            {
                SpawnCloud(-18f);
                nextCloud = Time.time + Random.Range(cfg.cloudSpawnMinSeconds, cfg.cloudSpawnMaxSeconds);
            }
            for (int i = clouds.Count - 1; i >= 0; i--)
            {
                var (t, speed) = clouds[i];
                t.position += Vector3.right * speed * Time.deltaTime;
                if (t.position.x > 18f)
                {
                    Destroy(t.gameObject);
                    clouds.RemoveAt(i);
                }
            }
        }
    }

    /// <summary>Helpers for world-space sprites and text.</summary>
    public static class World
    {
        public static TMP_FontAsset Font;

        public static SpriteRenderer Sprite(string name, Transform parent, Sprite sprite, int order, Color? color = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            sr.color = color ?? Color.white;
            return sr;
        }

        public static TextMeshPro Text(string name, Transform parent, string text, float size, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshPro>();
            if (Font != null) t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.sizeDelta = new Vector2(6, 1);
            t.sortingOrder = order;
            return t;
        }
    }

    public enum PreviewState { Valid, Swap, Invalid }

    /// <summary>
    /// Renders the interactive sky boards (field + shed, flat), the compost heap and the decorative
    /// isometric ground field that mirrors the field board. Pure presentation.
    /// </summary>
    public class FieldView : MonoBehaviour
    {
        public static readonly FlatGrid FieldGrid = new FlatGrid(IsoLayout.BoardTopLeftPx, anchoredTop: true);
        public static readonly FlatGrid ShedGrid = new FlatGrid(IsoLayout.ShedBoardOriginPx);
        static readonly IsoGrid Ground = new IsoGrid(IsoLayout.FieldOriginPx);

        SpriteSet sprites;
        Transform boardRoot, wordsRoot, groundRoot, glowRoot, heldRoot, floatRoot;
        SpriteRenderer scarecrow;
        GameObject shedDecor, compostDecor;
        readonly Dictionary<int, Transform> wordObjects = new Dictionary<int, Transform>();
        readonly List<SpriteRenderer> glowPool = new List<SpriteRenderer>();
        int glowUsed;
        public bool ShowScoreLabels;

        static readonly Color SignText = new Color32(50, 30, 15, 255);
        // world text sizes (TMP world units), scaled with the 20 px board cells
        const float SignSize = 3.4f, LabelSize = 3.2f, SignHeight = 0.82f, SignOffsetY = 0.5f;
        static readonly Color BoardBg = new Color(0.08f, 0.12f, 0.2f, 0.28f);
        static readonly Color FieldCell = new Color32(122, 84, 54, 120);
        static readonly Color ShedCell = new Color32(176, 132, 86, 120);
        static readonly Color SwapGlow = new Color(1f, 0.85f, 0.25f, 0.6f);

        public void Init(SpriteSet s)
        {
            sprites = s;
            boardRoot = Child("Board");
            wordsRoot = Child("Words");
            groundRoot = Child("Ground");
            glowRoot = Child("Glow");
            heldRoot = Child("Held");
            floatRoot = Child("Floating");

            scarecrow = World.Sprite("Scarecrow", transform, s.scarecrow, 0);
            scarecrow.transform.position = IsoLayout.World(IsoLayout.ScarecrowPx);
            scarecrow.sortingOrder = Sorting.ByY(scarecrow.transform.position.y);

            // shed roof + label and compost heap live next to the field board in the sky
            shedDecor = new GameObject("ShedDecor");
            shedDecor.transform.SetParent(transform, false);
            compostDecor = new GameObject("CompostDecor");
            compostDecor.transform.SetParent(transform, false);
            var compost = World.Sprite("Compost", compostDecor.transform, s.compost, Sorting.BoardProps);
            compost.transform.position = IsoLayout.World(IsoLayout.CompostPx);
            compost.transform.localScale = Vector3.one * IsoLayout.CompostScale;
            Label(compostDecor.transform, UiText.Compost, compost.transform.position + new Vector3(0, -1.45f, 0));
        }

        Transform Child(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(transform, false);
            return t;
        }

        static void Label(Transform parent, string text, Vector3 pos)
        {
            var t = World.Text(text + "Label", parent, text, LabelSize, Color.white, Sorting.Labels);
            t.transform.position = pos;
            t.outlineWidth = 0.25f;
            t.outlineColor = new Color32(40, 30, 20, 255);
        }

        // ------------------------------------------------------------ hit tests

        public static bool IsOverCompost(Vector2 world)
        {
            var d = IsoLayout.Px(world) - IsoLayout.CompostPx;
            return Mathf.Abs(d.x) <= IsoLayout.CompostHalfSizePx.x && Mathf.Abs(d.y) <= IsoLayout.CompostHalfSizePx.y;
        }

        public static FlatGrid GridOf(WordInstance w) => w.Location == WordLocation.Shed ? ShedGrid : FieldGrid;

        // ------------------------------------------------------------ rendering

        public void Rebuild(RunState run)
        {
            Ui.Clear(boardRoot);
            Ui.Clear(wordsRoot);
            Ui.Clear(groundRoot);
            Ui.Clear(shedDecor.transform);
            wordObjects.Clear();
            bool inRun = run != null && run.Grid != null;
            compostDecor.SetActive(inRun);
            if (!inRun)
            {
                scarecrow.gameObject.SetActive(true);
                return;
            }

            FieldGrid.Rows = run.Grid.Height;   // the field board is anchored at its top edge
            Board(run.Grid, FieldGrid, FieldCell);
            Board(run.Shed, ShedGrid, ShedCell);
            var roof = World.Sprite("ShedRoof", shedDecor.transform, sprites.shedRoof, Sorting.BoardProps);
            roof.transform.position = ShedGrid.Corner(run.Shed.Width / 2f, run.Shed.Height) + new Vector3(0, 0.15f, 0);
            Label(shedDecor.transform, UiText.Shed, roof.transform.position + new Vector3(0, 0.95f, 0));

            // ground mirror: soil tiles for the field size, iso crops for every planted cell
            for (int y = 0; y < run.Grid.Height; y++)
                for (int x = 0; x < run.Grid.Width; x++)
                {
                    var sr = World.Sprite("Tile", groundRoot, sprites.soilTile, Sorting.Tiles);
                    sr.transform.position = Ground.CellCenter(new Vector2Int(x, y));
                }
            foreach (var w in run.Grid.Words)
                foreach (var c in w.Cells())
                {
                    var pos = Ground.CellCenter(c);
                    var sr = World.Sprite("Crop", groundRoot, sprites.Crop(w.Type), Sorting.ByY(pos.y));
                    sr.transform.position = pos;
                }

            foreach (var w in run.Grid.Words) wordObjects[w.Id] = BuildWord(w, FieldGrid, wordsRoot, Sorting.BoardCrops, Sorting.Signs, 1f, run);
            foreach (var w in run.Shed.Words) wordObjects[w.Id] = BuildWord(w, ShedGrid, wordsRoot, Sorting.BoardCrops, Sorting.Signs, 1f, run);

            scarecrow.gameObject.SetActive(run.ScarecrowAvailable);
        }

        void Board(GridModel g, FlatGrid grid, Color cellColor)
        {
            if (g == null) return;
            var bg = World.Sprite("BoardBg", boardRoot, sprites.pixel, Sorting.BoardBg, BoardBg);
            bg.drawMode = SpriteDrawMode.Sliced;
            float pad = 3f / IsoLayout.PPU;
            var size = new Vector2(g.Width, g.Height) * IsoLayout.BoardCellPx / IsoLayout.PPU;
            bg.size = size + Vector2.one * pad * 2;
            bg.transform.position = grid.Corner(g.Width / 2f, g.Height / 2f);
            for (int y = 0; y < g.Height; y++)
                for (int x = 0; x < g.Width; x++)
                {
                    var sr = World.Sprite($"Cell {x},{y}", boardRoot, sprites.boardCell, Sorting.BoardCells, cellColor);
                    sr.transform.position = grid.CellCenter(new Vector2Int(x, y));
                }
        }

        Transform BuildWord(WordInstance w, FlatGrid grid, Transform parent, int cropOrder, int signBase, float alpha, RunState run)
        {
            var root = new GameObject($"Word {w.Entry.Es}").transform;
            root.SetParent(parent, false);
            var anchorPos = grid.CellCenter(w.Anchor);
            root.position = anchorPos;
            foreach (var c in w.Cells())
            {
                var sr = World.Sprite("Icon", root, sprites.Icon(w.Type), cropOrder, new Color(1, 1, 1, alpha));
                sr.transform.position = grid.CellCenter(c);
            }
            AddSign(root, w, new Vector3(0, SignOffsetY, 0), Sorting.SignByY(anchorPos.y, signBase));
            if (ShowScoreLabels && run != null && w.Location == WordLocation.Grid)
            {
                var b = run.ScoreOf(w);
                var lbl = World.Text("Score", root, UiText.F1(b.Total), SignSize, Color.yellow, Sorting.Floating - 5);
                lbl.transform.localPosition = new Vector3(0, -0.25f, 0);
                lbl.outlineWidth = 0.25f;
                lbl.outlineColor = Color.black;
            }
            return root;
        }

        void AddSign(Transform root, WordInstance w, Vector3 offset, int signOrder)
        {
            var text = World.Text("SignText", root, w.Entry.Es, SignSize, SignText, signOrder + 1);
            text.ForceMeshUpdate();
            float width = Mathf.Max(1.0f, text.preferredWidth + 0.75f);
            var sign = World.Sprite("Sign", root, sprites.sign, signOrder);
            sign.drawMode = SpriteDrawMode.Sliced;
            sign.size = new Vector2(width, SignHeight);
            sign.transform.localPosition = offset;
            text.transform.localPosition = offset + new Vector3(0.14f, 0.04f, 0);

            var badge = World.Sprite("TypeBadge", root, sprites.pixel, signOrder + 1, SpriteSet.TypeColor(w.Type));
            badge.transform.localPosition = offset + new Vector3(-width / 2f + 0.3f, 0.03f, 0);
            badge.transform.localScale = new Vector3(0.8f, 0.8f, 1f);

            float iconX = width / 2f;
            if (w.HasArticleMod)
            {
                var icon = World.Sprite("ArticleIcon", root, sprites.articleIcon, signOrder + 1);
                icon.transform.localPosition = offset + new Vector3(iconX, 0.45f, 0);
                icon.transform.localScale = Vector3.one * 1.4f;
                iconX -= 0.5f;
            }
            if (w.HasConjugationMod)
            {
                var icon = World.Sprite("StarIcon", root, sprites.starIcon, signOrder + 1);
                icon.transform.localPosition = offset + new Vector3(iconX, 0.45f, 0);
                icon.transform.localScale = Vector3.one * 1.4f;
            }
        }

        // ------------------------------------------------------------ held word / glow

        (int id, Vector2Int anchor, int rot, FlatGrid grid, PreviewState state, int size) heldKey;
        Transform heldWord;
        Vector3 heldAnchorCenter;

        /// <summary>Shows the held word. <paramref name="grid"/> null = floating at the cursor.</summary>
        public void ShowHeld(WordInstance w, FlatGrid grid, Vector2Int anchor, int rotation, PreviewState state, Vector2 cursorWorld,
            WordInstance swapTarget)
        {
            if (w == null) { HideHeld(); return; }
            var key = (w.Id, anchor, rotation, grid, state, w.Size);
            if (heldWord != null && key.Equals(heldKey))
            {
                if (grid == null) heldWord.position = (Vector3)cursorWorld - heldAnchorCenter;
                return;
            }
            heldKey = key;
            Ui.Clear(heldRoot);
            BeginGlow();
            var g = grid ?? FieldGrid;
            var root = new GameObject("HeldWord").transform;
            heldWord = root;
            root.SetParent(heldRoot, false);
            heldAnchorCenter = g.CellCenter(anchor);
            Color glow = state == PreviewState.Valid ? Pal.PreviewValid : state == PreviewState.Swap ? SwapGlow : Pal.PreviewInvalid;
            if (swapTarget != null && state == PreviewState.Swap)
                foreach (var c in swapTarget.Cells()) Glow(g.CellCenter(c), new Color(1f, 1f, 1f, 0.35f));
            foreach (var c in Shapes.Cells(w.Family, w.Size, rotation, anchor))
            {
                var pos = g.CellCenter(c);
                var sr = World.Sprite("Icon", root, sprites.Icon(w.Type), Sorting.Held, new Color(1, 1, 1, 0.9f));
                sr.transform.localPosition = pos;
                if (grid != null) Glow(pos, glow);
            }
            var signRoot = new GameObject("Sign").transform;
            signRoot.SetParent(root, false);
            signRoot.localPosition = heldAnchorCenter;
            AddSign(signRoot, w, new Vector3(0, SignOffsetY, 0), Sorting.HeldSigns);
            root.position = grid != null ? Vector3.zero : (Vector3)cursorWorld - heldAnchorCenter;
            EndGlow();
        }

        public void HideHeld()
        {
            Ui.Clear(heldRoot);
            heldWord = null;
        }

        public void ShowTargets(IEnumerable<WordInstance> targets, WordInstance hovered)
        {
            BeginGlow();
            if (targets != null)
                foreach (var w in targets)
                {
                    var col = w == hovered ? new Color(1f, 1f, 0.6f, 0.85f) : Pal.TargetGlow;
                    foreach (var c in w.Cells()) Glow(FieldGrid.CellCenter(c), col);
                }
            EndGlow();
        }

        public void ClearGlow() { BeginGlow(); EndGlow(); }

        void BeginGlow() => glowUsed = 0;

        void Glow(Vector3 pos, Color col)
        {
            SpriteRenderer sr;
            if (glowUsed < glowPool.Count) sr = glowPool[glowUsed];
            else { sr = World.Sprite("Glow", glowRoot, sprites.boardHighlight, Sorting.BoardGlow); glowPool.Add(sr); }
            glowUsed++;
            sr.gameObject.SetActive(true);
            sr.transform.position = pos;
            sr.color = col;
        }

        void EndGlow()
        {
            for (int i = glowUsed; i < glowPool.Count; i++) glowPool[i].gameObject.SetActive(false);
        }

        // ------------------------------------------------------------ harvest feedback

        public Transform WordObject(int id) => wordObjects.TryGetValue(id, out var t) ? t : null;

        public IEnumerator Bounce(Transform t, float duration)
        {
            if (t == null) yield break;
            float time = 0;
            while (time < duration && t != null)
            {
                time += Time.deltaTime;
                float k = Mathf.Sin(Mathf.Clamp01(time / duration) * Mathf.PI);
                t.localScale = Vector3.one * (1f + 0.15f * k);
                yield return null;
            }
            if (t != null) t.localScale = Vector3.one;
        }

        public void FloatText(Vector3 pos, string text, Color color, float duration)
        {
            var t = World.Text("Float", floatRoot, text, 5.2f, color, Sorting.Floating);
            t.outlineWidth = 0.3f;
            t.outlineColor = new Color32(40, 25, 10, 255);
            t.fontStyle = FontStyles.Bold;
            t.transform.position = pos;
            StartCoroutine(FloatRoutine(t, duration));
        }

        IEnumerator FloatRoutine(TextMeshPro t, float duration)
        {
            float time = 0;
            var start = t.transform.position;
            while (time < duration && t != null)
            {
                time += Time.deltaTime;
                float k = time / duration;
                t.transform.position = start + Vector3.up * (0.9f * k);
                t.alpha = 1f - Mathf.Clamp01((k - 0.6f) / 0.4f);
                yield return null;
            }
            if (t != null) Destroy(t.gameObject);
        }

        public void ClearFloating() => Ui.Clear(floatRoot);

        public IEnumerator ScarecrowFall()
        {
            if (!scarecrow.gameObject.activeSelf) yield break;
            float time = 0;
            while (time < 0.5f)
            {
                time += Time.deltaTime;
                scarecrow.transform.rotation = Quaternion.Euler(0, 0, -90f * Mathf.Clamp01(time / 0.5f));
                yield return null;
            }
            scarecrow.gameObject.SetActive(false);
            scarecrow.transform.rotation = Quaternion.identity;
        }
    }
}
