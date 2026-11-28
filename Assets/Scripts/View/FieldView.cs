using System.Collections;
using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Cosecha.View
{
    /// <summary>Sorting orders. Ground objects are painter-sorted by screen y; the sky boards sit above the ground.</summary>
    public static class Sorting
    {
        public const int Sky = -200, Clouds = -150, Mesa = -100, Tiles = -50, Depth = 1000;
        public const int BoardBg = 10000, BoardCells = 10001, BoardProps = 10002, BoardCrops = 10003, BoardOutline = 10004, BoardGlow = 10005, Twine = 10600,
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

    public enum PreviewState { Valid, Swap, Merge, Invalid }

    /// <summary>
    /// Renders the interactive sky boards (field + shed, flat), the compost heap, the sentence twines and the decorative
    /// isometric ground field that mirrors the field board. Pure presentation.
    /// </summary>
    public class FieldView : MonoBehaviour
    {
        /// <summary>The field board. Its cell pitch follows the field size (see <see cref="IsoLayout.FieldPitch"/>).</summary>
        public static readonly FlatGrid FieldGrid = new FlatGrid(IsoLayout.BoardTopLeftPx, IsoLayout.FieldMaxPitchPx);
        public static readonly FlatGrid ShedGrid = new FlatGrid(IsoLayout.ShedBoardTopLeftPx, IsoLayout.ShedPitchPx);
        static readonly IsoGrid Ground = new IsoGrid(IsoLayout.FieldOriginPx);

        SpriteSet sprites;
        BalanceConfig cfg;
        Transform boardRoot, wordsRoot, groundRoot, glowRoot, heldRoot, floatRoot, twineRoot;
        SpriteRenderer scarecrow;
        GameObject shedDecor, compostDecor;
        readonly Dictionary<int, Transform> wordObjects = new Dictionary<int, Transform>();
        readonly List<SpriteRenderer> glowPool = new List<SpriteRenderer>();
        int glowUsed;
        public bool ShowScoreLabels;

        static readonly Color SignText = new Color32(50, 30, 15, 255);
        // world text sizes (TMP world units) at the base cell size; signs grow with the cells (see TextScale)
        const float SignSize = 3.4f, MinSignSize = 2.6f, LabelSize = 3.2f, SignHeight = 0.82f;
        const float OutlinePx = 1.5f;
        static readonly Color BoardBg = new Color(0.08f, 0.12f, 0.2f, 0.28f);
        static readonly Color FieldCell = new Color32(122, 84, 54, 120);
        static readonly Color ShedCell = new Color32(176, 132, 86, 120);
        /// <summary>Backing strip of a word bar: fills the gaps between its cells so the word reads as one unit.</summary>
        static readonly Color BarBacking = new Color32(92, 62, 38, 190);
        static readonly Color SwapGlow = new Color(1f, 0.85f, 0.25f, 0.6f);
        static readonly Color MergeGlow = new Color(0.45f, 1f, 0.55f, 0.75f);

        public static Color TwineColor(StructureCategory c) =>
            c == StructureCategory.Frage ? new Color32(176, 104, 228, 255)
            : c == StructureCategory.Satz ? new Color32(246, 198, 48, 255)
            : new Color32(206, 168, 118, 255);

        /// <summary>Text and sign scale of a board: 1 at small cells, up to 1.6 at the largest field cells.</summary>
        public static float TextScale(FlatGrid g) => Mathf.Clamp(g.CellPx / 22f, 1f, 1.6f);

        public void Init(SpriteSet s, BalanceConfig c)
        {
            sprites = s;
            cfg = c;
            boardRoot = Child("Board");
            wordsRoot = Child("Words");
            groundRoot = Child("Ground");
            glowRoot = Child("Glow");
            twineRoot = Child("Twines");
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

        /// <summary>Sets the field board's cell size for the current field (shrinks only once the field no longer fits).</summary>
        public static void FitFieldGrid(GridModel g) => FieldGrid.SetPitch(IsoLayout.FieldPitch(g.Width, g.Height));

        /// <summary>Signs sit in the upper part of the bar, centered on it; the crops show below.</summary>
        static float SignRaise(FlatGrid grid) => grid.CellUnits * 0.22f;

        /// <summary>World position of a word's sign center.</summary>
        public static Vector3 SignCenter(FlatGrid grid, Vector2Int anchor, int size) =>
            grid.BarCenter(anchor, size) + new Vector3(0, SignRaise(grid), 0);

        // ------------------------------------------------------------ rendering

        public void Rebuild(RunState run, bool animateStructures = true)
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
                ClearStructures();
                scarecrow.gameObject.SetActive(true);
                return;
            }

            FitFieldGrid(run.Grid);
            Board(run.Grid, FieldGrid, FieldCell);
            Board(run.Shed, ShedGrid, ShedCell);
            var roof = World.Sprite("ShedRoof", shedDecor.transform, sprites.shedRoof, Sorting.BoardProps);
            // the roof art is drawn for 20 px cells: stretch it to the shed's width
            float roofScale = (run.Shed.Width * ShedGrid.PitchPx + 16f) / (run.Shed.Width * IsoLayout.BoardCellPx + 16f);
            roof.transform.localScale = new Vector3(roofScale, roofScale, 1f);
            roof.transform.position = ShedGrid.Corner(run.Shed.Width / 2f, 0) + new Vector3(0, 0.15f, 0);
            Label(shedDecor.transform, UiText.Shed, roof.transform.position + new Vector3(0, 0.95f * roofScale, 0));

            // ground mirror: soil tiles for the field size, iso crops for every planted cell.
            // Board row 0 is the top row; on the ground it is the far (upper) row.
            int h = run.Grid.Height;
            Vector2Int G(Vector2Int c) => new Vector2Int(c.x, h - 1 - c.y);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < run.Grid.Width; x++)
                {
                    var sr = World.Sprite("Tile", groundRoot, sprites.soilTile, Sorting.Tiles);
                    sr.transform.position = Ground.CellCenter(G(new Vector2Int(x, y)));
                }
            foreach (var w in run.Grid.Words)
                foreach (var c in w.Cells())
                {
                    var pos = Ground.CellCenter(G(c));
                    var sr = World.Sprite("Crop", groundRoot, sprites.Crop(w.Type), Sorting.ByY(pos.y));
                    sr.transform.position = pos;
                }

            foreach (var w in run.Grid.Words) wordObjects[w.Id] = BuildWord(w, FieldGrid, wordsRoot, run);
            foreach (var w in run.Shed.Words) wordObjects[w.Id] = BuildWord(w, ShedGrid, wordsRoot, run);

            scarecrow.gameObject.SetActive(run.ScarecrowAvailable);
            UpdateStructures(run, animateStructures);
        }

        void Board(GridModel g, FlatGrid grid, Color cellColor)
        {
            if (g == null) return;
            var bg = World.Sprite("BoardBg", boardRoot, sprites.pixel, Sorting.BoardBg, BoardBg);
            bg.drawMode = SpriteDrawMode.Sliced;
            float pad = 3f / IsoLayout.PPU;
            var size = new Vector2(g.Width, g.Height) * grid.PitchPx / IsoLayout.PPU;
            bg.size = size + Vector2.one * pad * 2;
            bg.transform.position = grid.Corner(g.Width / 2f, g.Height / 2f);
            for (int y = 0; y < g.Height; y++)
                for (int x = 0; x < g.Width; x++)
                {
                    var sr = World.Sprite($"Cell {x},{y}", boardRoot, sprites.boardCell, Sorting.BoardCells, cellColor);
                    sr.transform.position = grid.CellCenter(new Vector2Int(x, y));
                    sr.transform.localScale = Vector3.one * grid.SpriteScale;
                }
        }

        SpriteRenderer Rect(Transform parent, string name, Vector3 center, Vector2 size, Color color, int order)
        {
            var sr = World.Sprite(name, parent, sprites.pixel, order, color);
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
            sr.transform.position = center;
            return sr;
        }

        /// <summary>One continuous outline around all cells of a word bar.</summary>
        void Outline(Transform parent, FlatGrid grid, Vector2Int anchor, int size, Color color, int order)
        {
            float t = OutlinePx * TextScale(grid) / IsoLayout.PPU;
            var c = grid.BarCenter(anchor, size);
            var s = grid.BarSize(size);
            float hw = s.x / 2f, hh = s.y / 2f;
            Rect(parent, "Outline", c + new Vector3(0, hh - t / 2f, 0), new Vector2(s.x, t), color, order);
            Rect(parent, "Outline", c + new Vector3(0, -hh + t / 2f, 0), new Vector2(s.x, t), color, order);
            Rect(parent, "Outline", c + new Vector3(-hw + t / 2f, 0, 0), new Vector2(t, s.y), color, order);
            Rect(parent, "Outline", c + new Vector3(hw - t / 2f, 0, 0), new Vector2(t, s.y), color, order);
        }

        /// <summary>Draws a word bar (backing, crops, outline, sign) at world positions under <paramref name="root"/>.</summary>
        void DrawBar(Transform root, WordInstance w, FlatGrid grid, Vector2Int anchor, int baseOrder, int signOrder, float alpha)
        {
            Rect(root, "Backing", grid.BarCenter(anchor, w.Size), grid.BarSize(w.Size), BarBacking, baseOrder);
            float dropY = grid.CellUnits * 0.12f;
            foreach (var c in WordInstance.CellsAt(anchor, w.Size))
            {
                var sr = World.Sprite("Icon", root, sprites.Icon(w.Type), baseOrder + 1, new Color(1, 1, 1, alpha));
                sr.transform.position = grid.CellCenter(c) - new Vector3(0, dropY, 0);
                sr.transform.localScale = Vector3.one * grid.SpriteScale * 0.92f;
            }
            Outline(root, grid, anchor, w.Size, SpriteSet.OutlineColor(w.Type), baseOrder + 2);
            AddSign(root, w, grid, anchor, signOrder);
        }

        Transform BuildWord(WordInstance w, FlatGrid grid, Transform parent, RunState run)
        {
            var root = new GameObject($"Word {w.Entry.Es}").transform;
            root.SetParent(parent, false);
            var center = grid.BarCenter(w.Anchor, w.Size);
            root.position = center;   // bounces scale around the bar's center
            DrawBar(root, w, grid, w.Anchor, Sorting.BoardProps, Sorting.SignByY(center.y, Sorting.Signs), 1f);
            if (ShowScoreLabels && run != null && w.Location == WordLocation.Grid)
            {
                var b = run.ScoreOf(w);
                var lbl = World.Text("Score", root, UiText.F1(b.Total), SignSize * TextScale(grid), Color.yellow, Sorting.Floating - 5);
                lbl.transform.position = center - new Vector3(0, grid.CellUnits * 0.25f, 0);
                lbl.outlineWidth = 0.25f;
                lbl.outlineColor = Color.black;
            }
            return root;
        }

        /// <summary>
        /// The wooden sign, centered on the bar in its upper part. It grows with the cells; long words shrink their text
        /// to fit the bar (down to a readable minimum, after which the sign may stick out a little).
        /// </summary>
        void AddSign(Transform root, WordInstance w, FlatGrid grid, Vector2Int anchor, int signOrder)
        {
            float k = TextScale(grid);
            // one-cell words on small cells may overhang their bar a little rather than become unreadable
            float maxW = Mathf.Max(grid.BarSize(w.Size).x - 2f / IsoLayout.PPU, 2.1f);
            float badgeW = 0.6f * k;           // room for the type badge at the left end
            float padding = 0.35f * k;
            var text = World.Text("SignText", root, w.Shown, SignSize * k, SignText, signOrder + 1);
            text.ForceMeshUpdate();
            float width = text.preferredWidth + badgeW + padding;
            if (width > maxW)
            {
                float fit = (maxW - badgeW - padding) / Mathf.Max(0.01f, text.preferredWidth);
                text.fontSize = Mathf.Max(MinSignSize, text.fontSize * fit);
                text.ForceMeshUpdate();
                width = text.preferredWidth + badgeW + padding;
            }
            width = Mathf.Max(width, Mathf.Min(1.0f * k, maxW));
            float height = SignHeight * Mathf.Lerp(1f, k, 0.8f);

            var center = SignCenter(grid, anchor, w.Size);
            var sign = World.Sprite("Sign", root, sprites.sign, signOrder);
            sign.drawMode = SpriteDrawMode.Sliced;
            sign.size = new Vector2(width, height);
            sign.transform.position = center;
            text.transform.position = center + new Vector3(badgeW / 2f - padding * 0.15f, 0.04f * k, 0);

            var badge = World.Sprite("TypeBadge", root, sprites.pixel, signOrder + 1, SpriteSet.TypeColor(w.Type));
            badge.transform.position = center + new Vector3(-width / 2f + badgeW * 0.55f, 0.03f * k, 0);
            badge.transform.localScale = new Vector3(0.8f * k, 0.8f * k, 1f);
        }

        // ------------------------------------------------------------ sentence twines

        class TwineObj
        {
            public Structure Structure;
            public string Geometry;
            public Transform Root;
            public TextMeshPro Label;
            public SpriteRenderer[] Sprites;
        }

        readonly Dictionary<string, TwineObj> twines = new Dictionary<string, TwineObj>();
        const float TwineWidth = 3f / 16f, LaneOffset = 0.16f;

        public IEnumerable<(Structure s, Transform t)> StructureObjects => twines.Values.Select(o => (o.Structure, o.Root));

        void ClearStructures()
        {
            foreach (var o in twines.Values) if (o.Root != null) Destroy(o.Root.gameObject);
            twines.Clear();
        }

        /// <summary>Diffs the field's structures against the drawn twines: new ones pop in, vanished ones fade out.</summary>
        public void UpdateStructures(RunState run, bool animate)
        {
            var structures = run.Analysis.Structures;
            var words = run.Grid.Words.ToDictionary(w => w.Id);

            // lanes: structures that share a word are drawn side by side
            var lanes = new int[structures.Count];
            for (int i = 0; i < structures.Count; i++)
            {
                var taken = new HashSet<int>();
                for (int j = 0; j < i; j++)
                    if (structures[j].Words.Any(w => structures[i].Contains(w.Id))) taken.Add(lanes[j]);
                int lane = 0;
                while (taken.Contains(lane)) lane++;
                lanes[i] = lane;
            }

            var keep = new HashSet<string>();
            for (int i = 0; i < structures.Count; i++)
            {
                var s = structures[i];
                if (!s.Words.All(w => words.ContainsKey(w.Id))) continue;
                string key = s.Key;
                keep.Add(key);
                var points = s.Words.Select(w => SignCenter(FieldGrid, words[w.Id].Anchor, words[w.Id].Size)).ToList();
                string geometry = lanes[i] + ":" + string.Join(";", points.Select(p => p.ToString("F2")));
                if (twines.TryGetValue(key, out var existing))
                {
                    if (existing.Geometry == geometry) { existing.Structure = s; continue; }
                    Destroy(existing.Root.gameObject);
                    twines.Remove(key);
                    twines[key] = BuildTwine(s, points, lanes[i], geometry, false);
                }
                else twines[key] = BuildTwine(s, points, lanes[i], geometry, animate);
            }
            foreach (var key in twines.Keys.Where(k => !keep.Contains(k)).ToList())
            {
                var o = twines[key];
                twines.Remove(key);
                if (o.Root != null) StartCoroutine(FadeOut(o));
            }
        }

        static float LaneShift(int lane) =>
            lane == 0 ? 0 : (lane % 2 == 1 ? 1 : -1) * ((lane + 1) / 2) * LaneOffset * TextScale(FieldGrid);

        TwineObj BuildTwine(Structure s, List<Vector3> points, int lane, string geometry, bool animate)
        {
            var color = TwineColor(s.Category);
            float k = TextScale(FieldGrid);
            float width = TwineWidth * k;
            var dir = (points[points.Count - 1] - points[0]);
            var perp = dir.sqrMagnitude < 1e-6f ? Vector3.up : new Vector3(-dir.y, dir.x, 0).normalized;
            if (perp.y < 0) perp = -perp;
            var shift = perp * LaneShift(lane);
            var pts = points.Select(p => p + shift).ToList();
            var mid = (pts[0] + pts[pts.Count - 1]) / 2f;

            var root = new GameObject($"Twine {s}").transform;
            root.SetParent(twineRoot, false);
            root.position = mid;
            var srs = new List<SpriteRenderer>();
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                var a = pts[i]; var b = pts[i + 1];
                var seg = World.Sprite("Segment", root, sprites.pixel, Sorting.Twine, color);
                seg.drawMode = SpriteDrawMode.Sliced;
                seg.size = new Vector2((b - a).magnitude, width);
                seg.transform.position = (a + b) / 2f;
                seg.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
                srs.Add(seg);
            }
            foreach (var p in pts)
            {
                var knot = World.Sprite("Knot", root, sprites.pixel, Sorting.Twine + 1, color);
                knot.drawMode = SpriteDrawMode.Sliced;
                knot.size = Vector2.one * width * 2.2f;
                knot.transform.position = p;
                knot.transform.rotation = Quaternion.Euler(0, 0, 45);
                srs.Add(knot);
            }
            var label = World.Text("Label", root, UiText.StructureLabel(s.Category, s.Bonus), 3.8f * Mathf.Min(k, 1.3f), color, Sorting.Labels);
            label.fontStyle = FontStyles.Bold;
            label.outlineWidth = 0.35f;
            label.outlineColor = new Color32(40, 25, 10, 255);
            // label sits at the middle of the string, nudged off the signs
            var segMid = PointAlong(pts, 0.5f);
            if (Mathf.Abs(dir.y) > 0.01f)
            {
                // vertical or diagonal string: label to the right of its middle, clear of the signs
                label.alignment = TextAlignmentOptions.Left;
                label.ForceMeshUpdate();
                label.transform.position = segMid + new Vector3(label.rectTransform.sizeDelta.x / 2f + 0.25f * k, 0, 0) + shift * 0.5f;
            }
            // horizontal string: label in the lower part of the row, under the signs
            else label.transform.position = segMid + new Vector3(0, -FieldGrid.CellUnits * 0.5f, 0) + shift * 0.5f;

            var o = new TwineObj { Structure = s, Geometry = geometry, Root = root, Label = label, Sprites = srs.ToArray() };
            if (animate) StartCoroutine(PopIn(o));
            return o;
        }

        static Vector3 PointAlong(List<Vector3> pts, float t)
        {
            if (pts.Count == 1) return pts[0];
            float total = 0;
            for (int i = 0; i + 1 < pts.Count; i++) total += (pts[i + 1] - pts[i]).magnitude;
            float target = total * t;
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                float len = (pts[i + 1] - pts[i]).magnitude;
                if (target <= len || i + 2 == pts.Count) return Vector3.Lerp(pts[i], pts[i + 1], len > 0 ? Mathf.Clamp01(target / len) : 0);
                target -= len;
            }
            return pts[pts.Count - 1];
        }

        float PopSeconds => cfg != null ? Mathf.Max(0.05f, cfg.structurePopSeconds) : 0.4f;

        IEnumerator PopIn(TwineObj o)
        {
            float time = 0, d = PopSeconds;
            while (time < d && o.Root != null)
            {
                time += Time.deltaTime;
                float k = Mathf.Clamp01(time / d);
                // scale bounce: 0.6 → 1.15 → 1
                float s = k < 0.6f ? Mathf.Lerp(0.6f, 1.15f, k / 0.6f) : Mathf.Lerp(1.15f, 1f, (k - 0.6f) / 0.4f);
                o.Root.localScale = Vector3.one * s;
                if (o.Label != null) o.Label.alpha = k;
                yield return null;
            }
            if (o.Root != null) o.Root.localScale = Vector3.one;
            if (o.Label != null) o.Label.alpha = 1;
        }

        IEnumerator FadeOut(TwineObj o)
        {
            float time = 0, d = PopSeconds * 0.75f;
            var baseColors = o.Sprites.Select(s => s != null ? s.color : Color.clear).ToArray();
            while (time < d && o.Root != null)
            {
                time += Time.deltaTime;
                float a = 1f - Mathf.Clamp01(time / d);
                for (int i = 0; i < o.Sprites.Length; i++)
                    if (o.Sprites[i] != null) { var c = baseColors[i]; c.a *= a; o.Sprites[i].color = c; }
                if (o.Label != null) o.Label.alpha = a;
                yield return null;
            }
            if (o.Root != null) Destroy(o.Root.gameObject);
        }

        /// <summary>Harvest: one structure lights up (bounce and brighten).</summary>
        public IEnumerator LightUp(Transform t, float duration)
        {
            if (t == null) yield break;
            var srs = t.GetComponentsInChildren<SpriteRenderer>();
            var baseColors = srs.Select(s => s.color).ToArray();
            float time = 0;
            while (time < duration && t != null)
            {
                time += Time.deltaTime;
                float k = Mathf.Sin(Mathf.Clamp01(time / duration) * Mathf.PI);
                t.localScale = Vector3.one * (1f + 0.18f * k);
                for (int i = 0; i < srs.Length; i++)
                    if (srs[i] != null) srs[i].color = Color.Lerp(baseColors[i], Color.white, 0.6f * k);
                yield return null;
            }
            if (t == null) yield break;
            t.localScale = Vector3.one;
            for (int i = 0; i < srs.Length; i++) if (srs[i] != null) srs[i].color = baseColors[i];
        }

        // ------------------------------------------------------------ held word / glow

        (int id, Vector2Int anchor, FlatGrid grid, float pitch, PreviewState state, int size, string shown) heldKey;
        Transform heldWord;
        Vector3 heldAnchorCenter;

        /// <summary>Shows the held word. <paramref name="grid"/> null = floating at the cursor.</summary>
        public void ShowHeld(WordInstance w, FlatGrid grid, Vector2Int anchor, PreviewState state, Vector2 cursorWorld, WordInstance swapTarget)
        {
            if (w == null) { HideHeld(); return; }
            var g = grid ?? FieldGrid;
            var key = (w.Id, anchor, grid, g.PitchPx, state, w.Size, w.Shown);
            if (heldWord != null && key.Equals(heldKey))
            {
                if (grid == null) heldWord.position = (Vector3)cursorWorld - heldAnchorCenter;
                return;
            }
            heldKey = key;
            Ui.Clear(heldRoot);
            BeginGlow();
            var root = new GameObject("HeldWord").transform;
            heldWord = root;
            root.SetParent(heldRoot, false);
            heldAnchorCenter = g.CellCenter(anchor);
            Color glow = state == PreviewState.Valid ? Pal.PreviewValid : state == PreviewState.Swap ? SwapGlow : Pal.PreviewInvalid;
            if (swapTarget != null && state == PreviewState.Swap)
                foreach (var c in swapTarget.Cells()) Glow(g, g.CellCenter(c), new Color(1f, 1f, 1f, 0.35f));
            if (swapTarget != null && state == PreviewState.Merge)
                foreach (var c in swapTarget.Cells()) Glow(g, g.CellCenter(c), MergeGlow);
            else if (grid != null)
                foreach (var c in WordInstance.CellsAt(anchor, w.Size)) Glow(g, g.CellCenter(c), glow);
            DrawBar(root, w, g, anchor, Sorting.Held, Sorting.HeldSigns, 0.9f);
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
                    foreach (var c in w.Cells()) Glow(FieldGrid, FieldGrid.CellCenter(c), col);
                }
            EndGlow();
        }

        public void ClearGlow() { BeginGlow(); EndGlow(); }

        void BeginGlow() => glowUsed = 0;

        void Glow(FlatGrid grid, Vector3 pos, Color col)
        {
            SpriteRenderer sr;
            if (glowUsed < glowPool.Count) sr = glowPool[glowUsed];
            else { sr = World.Sprite("Glow", glowRoot, sprites.boardHighlight, Sorting.BoardGlow); glowPool.Add(sr); }
            glowUsed++;
            sr.gameObject.SetActive(true);
            sr.transform.position = pos;
            sr.transform.localScale = Vector3.one * grid.SpriteScale;
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
