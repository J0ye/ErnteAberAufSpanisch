using System;
using System.IO;
using Cosecha.View;
using UnityEditor;
using UnityEngine;

namespace Cosecha.EditorTools
{
    /// <summary>
    /// Generates the placeholder pixel art (isometric, shaded, light from the upper left).
    /// Replace the PNGs or the SpriteSet entries with hand-drawn art later.
    /// </summary>
    public static class PixelArtGenerator
    {
        public const string Folder = "Assets/Art/Generated";
        const int ScreenW = 480, ScreenH = 270;

        // ------------------------------------------------------------ pixel buffer

        class Px
        {
            public readonly int W, H;
            public readonly Color32[] P;
            public Px(int w, int h) { W = w; H = h; P = new Color32[w * h]; }
            public bool In(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;
            public void Set(int x, int y, Color32 c) { if (In(x, y)) P[y * W + x] = c; }
            public Color32 Get(int x, int y) => In(x, y) ? P[y * W + x] : Clear;
            public void Blend(int x, int y, Color32 c)
            {
                if (!In(x, y)) return;
                var d = P[y * W + x];
                float a = c.a / 255f;
                if (d.a == 0) { P[y * W + x] = c; return; }
                P[y * W + x] = new Color32((byte)(d.r + (c.r - d.r) * a), (byte)(d.g + (c.g - d.g) * a), (byte)(d.b + (c.b - d.b) * a), (byte)Math.Max(d.a, c.a));
            }
            public void Rect(int x, int y, int w, int h, Color32 c) { for (int j = y; j < y + h; j++) for (int i = x; i < x + w; i++) Set(i, j, c); }
            public void Disc(float cx, float cy, float rx, float ry, Color32 c)
            {
                for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1f) Set(x, y, c);
                }
            }
            public void Poly(Vector2[] pts, Func<int, int, Color32> color)
            {
                float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
                foreach (var q in pts) { minX = Mathf.Min(minX, q.x); maxX = Mathf.Max(maxX, q.x); minY = Mathf.Min(minY, q.y); maxY = Mathf.Max(maxY, q.y); }
                for (int y = Mathf.FloorToInt(minY); y <= Mathf.CeilToInt(maxY); y++)
                    for (int x = Mathf.FloorToInt(minX); x <= Mathf.CeilToInt(maxX); x++)
                        if (InPoly(pts, x + 0.5f, y + 0.5f)) Set(x, y, color(x, y));
            }
            public void Poly(Vector2[] pts, Color32 c) => Poly(pts, (x, y) => c);
            public void Line(Vector2 a, Vector2 b, Color32 c)
            {
                int n = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y))) + 1;
                for (int i = 0; i <= n; i++) { var q = Vector2.Lerp(a, b, i / (float)n); Set(Mathf.FloorToInt(q.x), Mathf.FloorToInt(q.y), c); }
            }
        }

        static bool InPoly(Vector2[] pts, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
                if ((pts[i].y > y) != (pts[j].y > y) && x < (pts[j].x - pts[i].x) * (y - pts[i].y) / (pts[j].y - pts[i].y) + pts[i].x)
                    inside = !inside;
            return inside;
        }

        static Color32 C(int r, int g, int b, int a = 255) => new Color32((byte)r, (byte)g, (byte)b, (byte)a);
        static readonly Color32 Clear = C(0, 0, 0, 0);
        static Color32 Mul(Color32 c, float f) => C(Mathf.Clamp((int)(c.r * f), 0, 255), Mathf.Clamp((int)(c.g * f), 0, 255), Mathf.Clamp((int)(c.b * f), 0, 255), c.a);
        static Color32 Mix(Color32 a, Color32 b, float t) => Color32.Lerp(a, b, Mathf.Clamp01(t));
        static Vector2 V(float x, float y) => new Vector2(x, y);

        static int Hash(int x, int y) { unchecked { int h = x * 374761393 + y * 668265263; h = (h ^ (h >> 13)) * 1274126177; return h ^ (h >> 16); } }
        static float Noise(int x, int y) => (Hash(x, y) & 0xffff) / 65535f;
        static float Smooth(float x, float y, float scale)
        {
            x /= scale; y /= scale;
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float a = Noise(ix, iy), b = Noise(ix + 1, iy), c = Noise(ix, iy + 1), d = Noise(ix + 1, iy + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        // iso unit vectors in px

        // ------------------------------------------------------------ entry point

        public static void GenerateAll(BalanceConfig cfg)
        {
            Directory.CreateDirectory(Folder);
            Save("pixel", Solid(4, 4, Color.white), V(0.5f, 0.5f));
            Save("panel", Panel(), V(0.5f, 0.5f), new Vector4(5, 5, 5, 5));
            Save("button", ButtonSprite(), V(0.5f, 0.5f), new Vector4(4, 4, 4, 4));
            Save("sky", Sky(), V(0.5f, 0.5f));
            Save("mesa", Mesa(cfg), V(0.5f, 0.5f));
            var house = House(out var housePivot);
            Save("house", house, housePivot);
            Save("cloud_small", Cloud(28, 14, 1), V(0.5f, 0.5f));
            Save("cloud_medium", Cloud(40, 18, 2), V(0.5f, 0.5f));
            Save("cloud_large", Cloud(58, 24, 3), V(0.5f, 0.5f));
            Save("tile_soil", SoilTile(), V(0.5f, (2f + HH) / (TH + 2f)));
            Save("crop_wheat", Wheat(), V(0.5f, HH / 13f));
            Save("crop_lavender", Lavender(), V(0.5f, HH / 13f));
            Save("board_cell", BoardCell(), V(0.5f, 0.5f));
            Save("board_highlight", BoardHighlight(), V(0.5f, 0.5f));
            Save("icon_wheat", IconWheat(), V(0.5f, 0.5f));
            Save("icon_lavender", IconLavender(), V(0.5f, 0.5f));
            Save("shed_roof", ShedRoof(cfg), V(0.5f, 0f));
            Save("sign", Sign(), V(0.5f, 0.5f), new Vector4(3, 3, 3, 3));
            Save("icon_article", ArticleIcon(), V(0.5f, 0.5f));
            Save("icon_star", Star(), V(0.5f, 0.5f));
            Save("icon_magnifier", Magnifier(), V(0.5f, 0.5f));
            Save("compost", Compost(), V(0.5f, 0.5f));
            Save("scarecrow", Scarecrow(), V(0.5f, 0f));
            DeleteObsolete("field", "soil_cell", "cell_highlight", "crop_noun_wheat", "crop_verb_corn", "crop_adjective_sunflower", "crop_other_cabbage",
                "wheelbarrow", "shed", "tile_shed_floor", "tile_highlight");
            AssetDatabase.Refresh();
        }

        static void DeleteObsolete(params string[] names)
        {
            foreach (var n in names)
            {
                string path = $"{Folder}/{n}.png";
                if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            }
        }

        static void Save(string name, Px px, Vector2 pivot, Vector4 border = default)
        {
            var tex = new Texture2D(px.W, px.H, TextureFormat.RGBA32, false);
            tex.SetPixels32(px.P);
            tex.Apply();
            string path = $"{Folder}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.spritePixelsPerUnit = IsoLayout.PPU;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.maxTextureSize = 2048;
            var s = new TextureImporterSettings();
            imp.ReadTextureSettings(s);
            s.spriteAlignment = (int)SpriteAlignment.Custom;
            s.spritePivot = pivot;
            s.spriteBorder = border;
            s.spriteMeshType = SpriteMeshType.FullRect;
            imp.SetTextureSettings(s);
            imp.SaveAndReimport();
        }

        public static Sprite Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/{name}.png");

        // ------------------------------------------------------------ UI

        static Px Solid(int w, int h, Color32 c) { var p = new Px(w, h); p.Rect(0, 0, w, h, c); return p; }

        static Px Panel()
        {
            var p = Solid(16, 16, C(255, 255, 255));
            for (int i = 0; i < 16; i++)
            {
                p.Set(i, 0, C(120, 104, 90)); p.Set(i, 15, C(120, 104, 90));
                p.Set(0, i, C(120, 104, 90)); p.Set(15, i, C(120, 104, 90));
                p.Set(i, 1, C(214, 206, 196)); p.Set(14, i, C(214, 206, 196));   // inner shadow bottom/right
                p.Set(i, 14, C(255, 255, 255)); p.Set(1, i, C(255, 255, 255));   // inner light top/left
            }
            p.Set(1, 1, C(214, 206, 196)); p.Set(14, 14, C(214, 206, 196));
            foreach (var (x, y) in new[] { (0, 0), (15, 0), (0, 15), (15, 15) }) p.Set(x, y, Clear);
            return p;
        }

        static Px ButtonSprite()
        {
            var p = Solid(12, 12, C(255, 255, 255));
            p.Rect(0, 0, 12, 3, C(176, 176, 176));     // bottom bevel
            p.Rect(1, 9, 10, 2, C(255, 255, 255));
            for (int i = 0; i < 12; i++) { p.Set(i, 0, C(110, 110, 110)); p.Set(i, 11, C(150, 150, 150)); p.Set(0, i, C(130, 130, 130)); p.Set(11, i, C(130, 130, 130)); }
            foreach (var (x, y) in new[] { (0, 0), (11, 0), (0, 11), (11, 11) }) p.Set(x, y, Clear);
            return p;
        }

        // ------------------------------------------------------------ sky and clouds

        static Px Sky()
        {
            var p = new Px(ScreenW, ScreenH);
            Color top = C(58, 122, 214), mid = C(118, 178, 238), horizon = C(214, 236, 252);
            const int bands = 18;
            for (int y = 0; y < p.H; y++)
            {
                float t = y / (float)(p.H - 1);
                float bandPos = t * bands;
                int band = Mathf.FloorToInt(bandPos);
                float frac = bandPos - band;
                for (int x = 0; x < p.W; x++)
                {
                    float threshold = ((x & 1) * 2 + (y & 1)) / 4f + 0.125f;
                    float b = Mathf.Clamp01((frac > threshold ? band + 1 : band) / (float)bands);
                    Color c = b < 0.45f ? Color.Lerp(horizon, mid, b / 0.45f) : Color.Lerp(mid, top, (b - 0.45f) / 0.55f);
                    p.Set(x, y, c);
                }
            }
            // distant hazy islands near the bottom (atmospheric depth below the cliff)
            for (int x = 0; x < p.W; x++)
            {
                float h1 = 26 + 10 * Mathf.Sin(x * 0.021f) + 6 * Mathf.Sin(x * 0.057f + 1.3f);
                float h2 = 14 + 7 * Mathf.Sin(x * 0.034f + 2.1f) + 4 * Mathf.Sin(x * 0.09f);
                for (int y = 0; y < h1; y++) p.Set(x, y, Mix(C(178, 206, 234), C(200, 222, 244), y / h1));
                for (int y = 0; y < h2; y++) p.Set(x, y, Mix(C(150, 186, 222), C(170, 200, 230), y / h2));
            }
            return p;
        }

        static Px Cloud(int w, int h, int lobes)
        {
            var p = new Px(w, h);
            Color32 light = C(255, 255, 255), mid = C(234, 242, 252), shade = C(196, 214, 236);
            var mask = new bool[w, h];
            void Blob(float cx, float cy, float r)
            {
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                {
                    float dx = x + 0.5f - cx, dy = (y + 0.5f - cy) * 1.15f;
                    if (dx * dx + dy * dy <= r * r) mask[x, y] = true;
                }
            }
            Blob(w * 0.2f, h * 0.36f, h * 0.32f);
            Blob(w * 0.8f, h * 0.36f, h * 0.32f);
            for (int i = 0; i < lobes; i++)
                Blob(w * (0.33f + 0.34f * (lobes == 1 ? 0.5f : i / (float)(lobes - 1))), h * 0.56f, h * 0.42f);
            for (int y = 1; y < h * 0.4f; y++) for (int x = (int)(w * 0.18f); x < w * 0.82f; x++) mask[x, y] = true;
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                if (!mask[x, y]) continue;
                bool edgeBelow = y == 0 || !mask[x, y - 1];
                bool edgeAbove = y == h - 1 || !mask[x, y + 1];
                bool edgeLeft = x == 0 || !mask[x - 1, y];
                Color32 c = y < h * 0.3f ? shade : y < h * 0.5f ? mid : light;
                if (edgeBelow || y < 2) c = shade;
                if (edgeAbove || edgeLeft) c = light;
                p.Set(x, y, c);
            }
            return p;
        }

        // ------------------------------------------------------------ mesa (cliff top with the field)

        static Px Mesa(BalanceConfig cfg)
        {
            var p = new Px(ScreenW, ScreenH);
            var O = IsoLayout.FieldOriginPx;
            float u0 = IsoLayout.MesaU.x, u1 = IsoLayout.MesaU.y, v0 = IsoLayout.MesaV.x, v1 = IsoLayout.MesaV.y;
            Vector2 left = IsoLayout.PointPx(O, u0, v0), bottom = IsoLayout.PointPx(O, u1, v0), right = IsoLayout.PointPx(O, u1, v1);

            Color32 grass = C(104, 168, 72), grassLight = C(132, 192, 86), grassDark = C(78, 136, 58), lip = C(160, 206, 98);
            Color32 soil = C(154, 108, 66), soilDark = C(124, 84, 50), soilLight = C(176, 128, 82);
            Color32 rockLit = C(186, 154, 118), rockLitDark = C(156, 124, 94), rockShade = C(126, 100, 80), rockShadeDark = C(102, 80, 64);
            Color32 haze = C(190, 214, 238);

            for (int y = 0; y < p.H; y++)
                for (int x = 0; x < p.W; x++)
                {
                    float sx = x - ScreenW / 2f + 0.5f, sy = y - ScreenH / 2f + 0.5f;
                    var uv = IsoLayout.ToUV(O, V(sx, sy));
                    float u = uv.x, v = uv.y;
                    if (u >= u0 && u <= u1 && v >= v0 && v <= v1)
                    {
                        // ---- top surface
                        float edgeDist = Mathf.Min(Mathf.Min(u - u0, u1 - u), Mathf.Min(v - v0, v1 - v));
                        bool frontEdge = (u1 - u) < 0.12f || (v - v0) < 0.12f;
                        Color32 c;
                        float n = Smooth(x, y, 9f);
                        c = n > 0.62f ? grassLight : n < 0.35f ? grassDark : grass;
                        if (Noise(x, y) > 0.985f) c = C(236, 226, 120);                 // tiny flowers
                        else if (Noise(x + 7, y) > 0.985f) c = C(240, 240, 240);

                        bool inField = u >= -0.35f && u <= cfg.maxGridWidth + 0.35f && v >= -0.35f && v <= cfg.maxGridHeight + 0.35f;
                        if (inField)
                        {
                            bool border = u < -0.2f || u > cfg.maxGridWidth + 0.2f || v < -0.2f || v > cfg.maxGridHeight + 0.2f;
                            float f = v * 2f - Mathf.Floor(v * 2f);
                            c = border ? soilDark : f < 0.25f ? soilDark : f > 0.75f ? soilLight : soil;
                            if (Noise(x, y) > 0.93f) c = soilDark;
                        }
                        if (frontEdge) c = lip;
                        else if (edgeDist < 0.1f) c = grassLight;
                        p.Set(x, y, c);
                        continue;
                    }

                    // ---- cliff faces below the two front edges
                    float depth;
                    bool lit;
                    if (sx >= left.x && sx <= bottom.x)
                    {
                        float t = (sx - left.x) / (bottom.x - left.x);
                        float ey = Mathf.Lerp(left.y, bottom.y, t);
                        if (sy >= ey) continue;
                        depth = ey - sy; lit = true;
                    }
                    else if (sx > bottom.x && sx <= right.x)
                    {
                        float t = (sx - bottom.x) / (right.x - bottom.x);
                        float ey = Mathf.Lerp(bottom.y, right.y, t);
                        if (sy >= ey) continue;
                        depth = ey - sy; lit = false;
                    }
                    else continue;

                    Color32 baseC = lit ? rockLit : rockShade, darkC = lit ? rockLitDark : rockShadeDark;
                    // strata bands that follow the edge, jittered by noise; vertical cracks
                    float band = (depth + Smooth(x, y, 6f) * 4f) / 7f;
                    Color32 rc = (Mathf.FloorToInt(band) % 2 == 0) ? baseC : Mix(baseC, darkC, 0.55f);
                    if (band - Mathf.Floor(band) < 0.12f) rc = darkC;
                    if (Noise(x / 3, y / 9) > 0.86f && Noise(x, y / 2) > 0.4f) rc = darkC;
                    if (depth < 2.5f) rc = C(92, 120, 52);                 // grass overhang
                    else if (depth < 5f) rc = C(110, 82, 56);              // dark soil
                    rc = Mix(rc, haze, Mathf.Clamp01((depth - 40f) / 160f));   // atmospheric fade
                    p.Set(x, y, rc);
                }
            return p;
        }

        // ------------------------------------------------------------ ground tiles (isometric)

        const int TW = (int)IsoLayout.TileW, TH = (int)IsoLayout.TileH, HW = TW / 2, HH = TH / 2;

        /// <summary>Is (x,y) inside the TW×TH diamond whose bottom row is at <paramref name="baseY"/>?</summary>
        static bool InDiamond(int x, int y, int baseY, out float dx, out float dy)
        {
            dx = x + 0.5f - HW; dy = y - baseY + 0.5f - HH;
            return Mathf.Abs(dx) / HW + Mathf.Abs(dy) / HH <= 1.001f;
        }

        static Px SoilTile()
        {
            var p = new Px(TW, TH + 2);
            for (int y = 0; y < p.H; y++)
                for (int x = 0; x < TW; x++)
                {
                    if (InDiamond(x, y, 2, out float dx, out float dy))
                    {
                        float s = dx / HW, d = dy / HH;
                        float v = (s + d) * 0.5f + 0.5f;
                        float rim = Mathf.Abs(dx) / HW + Mathf.Abs(dy) / HH;
                        float f = v * 3f - Mathf.Floor(v * 3f);
                        Color32 c = f < 0.34f ? C(86, 54, 32) : f > 0.7f ? C(128, 84, 50) : C(106, 68, 40);
                        if (rim > 0.75f) c = dy > 0 ? C(138, 96, 60) : C(72, 44, 26);
                        p.Set(x, y, c);
                    }
                    else if (InDiamond(x, y + 1, 2, out _, out _) || InDiamond(x, y + 2, 2, out _, out _))
                        p.Set(x, y, x < HW ? C(84, 52, 30) : C(62, 38, 22));
                }
            return p;
        }

        /// <summary>Isometric wheat tuft on the ground (tile center at (HW, HH)).</summary>
        static Px Wheat()
        {
            var p = new Px(TW, 13);
            for (int y = 0; y < TH + 1; y++) for (int x = 0; x < TW; x++)
                if (InDiamond(x, y, 0, out float dx, out float dy) && Mathf.Abs(dx) / (HW - 1) + Mathf.Abs(dy) / (HH - 0.5f) <= 1f)
                    p.Set(x, y, C(70, 50, 18, 90));
            Color32 stalk = C(184, 140, 44), stalkShade = C(146, 104, 30);
            Color32 headLight = C(252, 220, 104), head = C(234, 186, 62), headDark = C(192, 136, 36);
            var stalks = new (int x, int y)[] { (3, 4), (8, 4), (6, 5), (2, 2), (9, 2), (5, 3), (7, 1), (4, 1) };
            Array.Sort(stalks, (a, b) => b.y.CompareTo(a.y));
            foreach (var (bx, by) in stalks)
            {
                int top = Mathf.Min(by + 7 + (Hash(bx, by) & 1), 12);
                bool right = bx >= HW;
                for (int y = by; y < top - 2; y++) p.Set(bx, y, right ? stalkShade : stalk);
                for (int y = top - 3; y <= top; y++)
                {
                    p.Set(bx, y, right ? head : headLight);
                    p.Set(bx + 1, y, (y % 2 == 0) ? headDark : head);
                }
            }
            return p;
        }

        /// <summary>Isometric lavender bush on the ground (phrases).</summary>
        static Px Lavender()
        {
            var p = new Px(TW, 13);
            for (int y = 0; y < TH + 1; y++) for (int x = 0; x < TW; x++)
                if (InDiamond(x, y, 0, out float dx, out float dy) && Mathf.Abs(dx) / (HW - 1) + Mathf.Abs(dy) / (HH - 0.5f) <= 1f)
                    p.Set(x, y, C(60, 40, 70, 90));
            Color32 leaf = C(124, 156, 112), leafLight = C(152, 184, 136), leafDark = C(92, 120, 86);
            for (int y = 0; y < 13; y++) for (int x = 0; x < TW; x++)
            {
                float ex = (x + 0.5f - HW) / 5f, ey = (y + 0.5f - 4.5f) / 2.8f;
                if (ex * ex + ey * ey > 1f) continue;
                float light = -ex * 0.6f + ey * 0.7f + (Noise(x, y) - 0.5f) * 0.5f;
                p.Set(x, y, light > 0.3f ? leafLight : light < -0.35f ? leafDark : leaf);
            }
            Color32 stem = C(104, 132, 92), flower = C(150, 106, 204), flowerLight = C(186, 150, 232), flowerDark = C(112, 74, 168);
            var spikes = new (int x, int y)[] { (3, 5), (6, 6), (9, 5), (4, 3), (8, 3), (6, 2) };
            Array.Sort(spikes, (a, b) => b.y.CompareTo(a.y));
            foreach (var (bx, by) in spikes)
            {
                int top = Mathf.Min(by + 5 + (Hash(bx, by) & 1), 12);
                for (int y = by; y < top - 2; y++) p.Set(bx, y, stem);
                for (int y = top - 2; y <= top; y++) p.Set(bx, y, y == top ? flowerLight : (bx >= HW ? flowerDark : flower));
            }
            return p;
        }

        // ------------------------------------------------------------ sky boards (flat, top-down)

        const int BC = (int)IsoLayout.BoardCellPx;   // board cell size in px
        const int IC = 18;                            // crop icon size in px

        static Px BoardCell()
        {
            var p = Solid(BC, BC, C(255, 255, 255, 170));
            for (int i = 0; i < BC; i++)
            {
                p.Set(i, BC - 1, C(255, 255, 255, 235)); p.Set(0, i, C(255, 255, 255, 235));          // light top/left edge
                p.Set(i, 0, C(196, 196, 196, 205)); p.Set(BC - 1, i, C(196, 196, 196, 205));          // darker bottom/right edge
            }
            foreach (var (x, y) in new[] { (0, 0), (BC - 1, 0), (0, BC - 1), (BC - 1, BC - 1) }) p.Set(x, y, Clear);
            return p;
        }

        static Px BoardHighlight()
        {
            var p = Solid(BC, BC, C(255, 255, 255, 110));
            for (int i = 0; i < BC; i++)
                for (int k = 0; k < 2; k++)
                { p.Set(i, k, Color.white); p.Set(i, BC - 1 - k, Color.white); p.Set(k, i, Color.white); p.Set(BC - 1 - k, i, Color.white); }
            return p;
        }

        /// <summary>Flat 2D wheat icon: a sheaf of five ears with a red tie.</summary>
        static Px IconWheat()
        {
            var p = new Px(IC, IC);
            Color32 stalk = C(196, 150, 50), stalkDark = C(150, 108, 34);
            Color32 head = C(244, 200, 76), headLight = C(255, 234, 146), headDark = C(204, 148, 40), tie = C(176, 60, 40), tieLight = C(214, 92, 64);
            int[] tops = { 14, 16, 17, 16, 14 };
            int[] xs = { 3, 6, 9, 12, 15 };
            for (int i = 0; i < 5; i++)
            {
                int x = xs[i];
                // stalk converges towards the tie at (9, 5)
                for (int y = 0; y < 9; y++)
                {
                    float t = Mathf.Clamp01((y - 1) / 7f);
                    int sx = Mathf.RoundToInt(Mathf.Lerp(9 + (x - 9) * 0.35f, x, t));
                    p.Set(sx, y, i < 2 ? stalk : stalkDark);
                }
                // ear: two columns of grains, lit from the left
                for (int y = 8; y <= tops[i]; y++)
                {
                    p.Set(x - 1, y, (y % 2 == 0) ? headLight : head);
                    p.Set(x, y, (y % 2 == 0) ? head : headDark);
                }
                p.Set(x, tops[i] + 1 > IC - 1 ? IC - 1 : tops[i] + 1, headLight);   // awn tip
            }
            p.Rect(6, 4, 7, 2, tie);
            p.Rect(6, 5, 7, 1, tieLight);
            return p;
        }

        /// <summary>Flat 2D lavender icon: purple flower spikes on green stems.</summary>
        static Px IconLavender()
        {
            var p = new Px(IC, IC);
            Color32 stem = C(92, 140, 80), stemDark = C(70, 110, 62), leaf = C(124, 170, 104);
            Color32 flower = C(150, 106, 204), flowerLight = C(198, 164, 242), flowerDark = C(108, 70, 164);
            int[] xs = { 3, 7, 10, 14 };
            int[] tops = { 14, 17, 16, 13 };
            for (int i = 0; i < xs.Length; i++)
            {
                int x = xs[i];
                int top = tops[i];
                for (int y = 0; y < top - 6; y++) p.Set(x, y, i < 2 ? stem : stemDark);
                for (int y = top - 6; y <= top; y++)
                {
                    p.Set(x, y, (y % 2 == 0) ? flowerLight : flower);
                    p.Set(x + 1, y, (y % 2 == 0) ? flower : flowerDark);
                    if (y % 3 == 0) p.Set(x - 1, y, flower);
                }
            }
            p.Set(4, 3, leaf); p.Set(5, 4, leaf); p.Set(6, 4, leaf);
            p.Set(12, 3, leaf); p.Set(13, 4, stemDark); p.Set(11, 4, leaf);
            return p;
        }

        /// <summary>Flat front view of a shed roof, drawn above the shed board (pivot bottom-center).</summary>
        static Px ShedRoof(BalanceConfig cfg)
        {
            int w = cfg.shedWidth * (int)IsoLayout.BoardCellPx + 16, h = 22;
            var p = new Px(w, h);
            Color32 beam = C(130, 88, 48), beamDark = C(96, 62, 32);
            Color32 roof = C(184, 72, 52), roofLight = C(218, 108, 78), roofDark = C(140, 50, 38);
            p.Rect(4, 0, w - 8, 3, beam);
            p.Rect(4, 0, w - 8, 1, beamDark);
            for (int y = 3; y < h; y++)
            {
                int inset = (y - 3) * 5 / 3;
                for (int x = inset; x < w - inset; x++)
                {
                    Color32 c = (y - 3) % 3 == 0 ? roofDark : roof;
                    if (x - inset < 2) c = roofLight;
                    p.Set(x, y, c);
                }
            }
            return p;
        }

        // ------------------------------------------------------------ signs and icons

        static Px Sign()
        {
            var p = new Px(16, 9);
            Color32 wood = C(200, 150, 92), light = C(226, 182, 122), dark = C(150, 104, 58), outline = C(96, 62, 32);
            p.Rect(0, 0, 16, 9, outline);
            p.Rect(1, 1, 14, 7, wood);
            p.Rect(1, 6, 14, 1, light);
            p.Rect(1, 7, 14, 1, C(236, 196, 140));
            p.Rect(1, 1, 14, 1, dark);
            p.Set(2, 4, dark); p.Set(13, 4, dark);   // nails
            foreach (var (x, y) in new[] { (0, 0), (15, 0), (0, 8), (15, 8) }) p.Set(x, y, Clear);
            return p;
        }

        static Px ArticleIcon()
        {
            var p = new Px(10, 8);
            Color32 paper = C(252, 244, 214), shade = C(220, 206, 168), red = C(176, 56, 52);
            p.Poly(new[] { V(2, 0), V(10, 0), V(10, 8), V(2, 8), V(0, 4) }, red);
            p.Poly(new[] { V(3, 1), V(9, 1), V(9, 7), V(3, 7), V(1.2f, 4) }, paper);
            p.Rect(3, 1, 6, 1, shade);
            p.Set(2, 4, red);
            p.Rect(4, 3, 1, 3, red); p.Rect(6, 3, 2, 1, red); p.Rect(6, 5, 2, 1, red);   // "la"-ish marks
            return p;
        }

        /// <summary>Generates only the magnifier icon (does not touch the other, possibly hand-edited art).</summary>
        public static void GenerateMagnifier()
        {
            Directory.CreateDirectory(Folder);
            Save("icon_magnifier", Magnifier(), V(0.5f, 0.5f));
        }

        /// <summary>Magnifying glass: brass ring with a light lens at the upper left, wooden handle to the lower right.</summary>
        static Px Magnifier()
        {
            var p = new Px(16, 16);
            Color32 ring = C(196, 148, 52), ringDark = C(132, 92, 30), glass = C(196, 228, 246), glare = C(250, 254, 255),
                    wood = C(122, 76, 40), woodLit = C(166, 108, 60);
            // handle first, so the ring covers its end
            for (int i = 0; i < 6; i++)
            {
                p.Rect(9 + i, 5 - i, 2, 2, wood);
                p.Set(9 + i, 6 - i, woodLit);
            }
            p.Disc(6.5f, 9.5f, 6.2f, 6.2f, ringDark);
            p.Disc(6.3f, 9.7f, 5.6f, 5.6f, ring);
            p.Disc(6.5f, 9.5f, 4.3f, 4.3f, glass);
            p.Rect(4, 11, 2, 1, glare); p.Rect(4, 12, 1, 1, glare); p.Set(6, 12, glare);
            return p;
        }

        static Px Star()
        {
            var p = new Px(9, 9);
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI / 2 + i * Mathf.PI / 5;
                float r = i % 2 == 0 ? 4.4f : 1.9f;
                pts[i] = V(4.5f + Mathf.Cos(a) * r, 4.3f + Mathf.Sin(a) * r);
            }
            p.Poly(pts, (x, y) => x + (8 - y) < 8 ? C(255, 232, 92) : C(232, 168, 28));
            return p;
        }

        // ------------------------------------------------------------ props

        static Px Compost()
        {
            var p = new Px(36, 22);
            Color32 lit = C(122, 86, 52), mid = C(98, 68, 40), dark = C(70, 48, 28);
            for (int y = 0; y < 22; y++) for (int x = 0; x < 36; x++)
            {
                float dx = (x + 0.5f - 18f) / 17f, dy = (y + 0.5f - 4f) / 17f;
                if (dy < -0.22f || dx * dx + dy * dy > 1f) continue;
                float light = -dx * 0.6f + dy * 0.8f + (Smooth(x, y, 3f) - 0.5f) * 0.6f;
                p.Set(x, y, light > 0.35f ? lit : light > -0.1f ? mid : dark);
            }
            Color32[] bits = { C(96, 160, 64), C(230, 138, 44), C(214, 186, 96), C(160, 50, 40) };
            for (int i = 0; i < 46; i++)
            {
                int x = 3 + (Hash(i, 1) & 0x7fff) % 30, y = (Hash(i, 2) & 0x7fff) % 18;
                if (p.Get(x, y).a == 0) continue;
                p.Set(x, y, bits[i % bits.Length]);
            }
            return p;
        }

        static Px Scarecrow()
        {
            var p = new Px(18, 32);
            Color32 wood = C(142, 98, 54), woodDark = C(104, 70, 38);
            Color32 shirt = C(74, 118, 192), shirtShade = C(50, 84, 146), straw = C(234, 200, 96), strawDark = C(190, 150, 60);
            Color32 face = C(238, 216, 164), faceShade = C(206, 180, 124), hat = C(160, 116, 62), hatDark = C(118, 82, 40);
            p.Disc(9, 1.5f, 5, 1.6f, C(40, 60, 20, 90));       // shadow
            p.Rect(8, 0, 1, 24, wood); p.Rect(9, 0, 1, 24, woodDark);
            p.Rect(1, 17, 16, 1, wood); p.Rect(1, 16, 16, 1, woodDark);
            p.Rect(5, 9, 8, 10, shirt); p.Rect(10, 9, 3, 10, shirtShade);
            p.Rect(1, 15, 4, 3, shirt); p.Rect(13, 15, 4, 3, shirtShade);
            p.Rect(7, 12, 1, 4, C(220, 70, 60)); p.Rect(6, 13, 3, 1, C(220, 70, 60));   // patch
            p.Set(0, 15, straw); p.Set(0, 16, strawDark); p.Set(17, 15, straw); p.Set(17, 16, strawDark);
            p.Set(6, 8, straw); p.Set(11, 8, strawDark); p.Set(8, 8, straw);
            p.Disc(9, 22.5f, 3.4f, 3.4f, face);
            for (int y = 19; y < 26; y++) for (int x = 10; x < 13; x++) if (p.Get(x, y).Equals(face)) p.Set(x, y, faceShade);
            p.Set(8, 23, C(40, 30, 20)); p.Set(10, 23, C(40, 30, 20)); p.Rect(8, 21, 3, 1, C(170, 80, 60));
            p.Rect(3, 25, 13, 2, hat); p.Rect(10, 25, 6, 2, hatDark);
            p.Rect(6, 27, 7, 4, hat); p.Rect(10, 27, 3, 4, hatDark);
            p.Rect(6, 27, 7, 1, C(200, 70, 60));
            return p;
        }

        // ------------------------------------------------------------ house (Canary-Islands style, isometric)

        static Px House(out Vector2 pivot)
        {
            const int a = 12, b = 4;
            Vector2 Iu = HU, Iv = HV;            // footprint in tiles along u (front-left face) and v (front-right face)
            var Lloc = V(12, 70);               // roof left corner inside the sprite = IsoLayout roof position
            Vector2 L = Lloc, B = L + Iu * a, R = B + Iv * b, T = L + Iv * b;
            int w = Mathf.CeilToInt(R.x + 6), h = Mathf.CeilToInt(T.y + 6);
            var p = new Px(w, h);

            Color32 wallLit = C(248, 246, 238), wallLitShade = C(232, 230, 220), wallShade = C(206, 206, 198), wallShadeDark = C(188, 188, 180);
            Color32 roofC = C(226, 222, 212), parapet = C(252, 250, 244), parapetShade = C(214, 212, 204);
            Color32 rockLit = C(176, 146, 112), rockShade = C(122, 98, 78);

            // walls down to the sprite bottom; rock base at the bottom
            var down = V(0, -200);
            p.Poly(new[] { L, B, B + down, L + down }, (x, y) => y < 10 + (x - L.x) * 0.0f ? rockLit : (x - L.x) < 3 ? wallLitShade : wallLit);
            p.Poly(new[] { B, R, R + down, B + down }, (x, y) => y < 10 ? rockShade : (x - B.x) < 2 ? wallShadeDark : wallShade);
            for (int x = 0; x < w; x++) for (int y = 0; y < 12; y++)
            {
                var c = p.Get(x, y);
                if (c.a == 0) continue;
                bool lit = x < B.x;
                float band = (y + Smooth(x, y, 5) * 3) / 5f;
                p.Set(x, y, Mathf.FloorToInt(band) % 2 == 0 ? (lit ? rockLit : rockShade) : Mul(lit ? rockLit : rockShade, 0.86f));
            }
            // roof with parapet
            p.Poly(new[] { L, B, R, T }, roofC);
            p.Poly(new[] { L + V(3, 0), B + V(0, 1.5f), R + V(-3, 0), T + V(0, -1.5f) }, C(214, 210, 198));
            p.Line(L, B, parapet); p.Line(L + V(0, 1), B + V(0, 1), parapet);
            p.Line(B, R, parapetShade); p.Line(B + V(0, 1), R + V(0, 1), parapetShade);
            p.Line(L, T, parapetShade); p.Line(T, R, parapetShade);
            // water tank on the roof
            var tank = L + Iu * 2.5f + Iv * 2f;
            p.Disc(tank.x, tank.y + 3, 4, 2, C(90, 90, 96));
            p.Rect((int)tank.x - 4, (int)tank.y + 3, 8, 4, C(70, 70, 78));
            p.Disc(tank.x, tank.y + 7, 4, 2, C(120, 120, 128));

            // helpers to place things on the front-left face: position along the face (tiles) and height above roof line
            Vector2 OnLeft(float along, float below) => L + Iu * along + V(0, -below);
            Vector2 OnRight(float along, float below) => B + Iv * along + V(0, -below);

            // door (wooden, green frame) on the left face
            DrawOpeningLeft(p, OnLeft(2.2f, 30), 2.2f, 22, C(126, 80, 40), C(92, 56, 28), C(60, 130, 90));
            // windows with green shutters
            DrawWindowLeft(p, OnLeft(5.5f, 14), C(52, 140, 96), C(36, 104, 70));
            DrawWindowLeft(p, OnLeft(8.6f, 14), C(52, 140, 96), C(36, 104, 70));
            DrawWindowLeft(p, OnLeft(5.5f, 30), C(52, 140, 96), C(36, 104, 70));
            // blue shutters on the shaded right face
            var rw = OnRight(1.6f, 16);
            p.Poly(new[] { rw, rw + Iv * 1.2f, rw + Iv * 1.2f + V(0, -8), rw + V(0, -8) }, C(56, 84, 116));
            p.Poly(new[] { rw - Iv * 0.6f, rw, rw + V(0, -8), rw - Iv * 0.6f + V(0, -8) }, C(48, 96, 160));
            p.Poly(new[] { rw + Iv * 1.2f, rw + Iv * 1.8f, rw + Iv * 1.8f + V(0, -8), rw + Iv * 1.2f + V(0, -8) }, C(40, 80, 140));
            // bougainvillea by the door
            for (int i = 0; i < 40; i++)
            {
                var q = OnLeft(0.8f, 28) + V((Hash(i, 3) & 7) - 2, (Hash(i, 4) & 15) - 2);
                p.Set((int)q.x, (int)q.y, i % 3 == 0 ? C(70, 140, 60) : C(214, 60, 140));
            }

            pivot = V(0, 0);
            return p;
        }

        static readonly Vector2 HU = new Vector2(8, -4), HV = new Vector2(8, 4);   // house iso basis (independent of the ground tiles)

        static void DrawOpeningLeft(Px p, Vector2 bl, float widthTiles, float height, Color32 fill, Color32 dark, Color32 frame)
        {
            var br = bl + HU * widthTiles;
            var up = V(0, height);
            p.Poly(new[] { bl + V(-1, 0), br + V(1, 0), br + up + V(1, 1), bl + up + V(-1, 1) }, frame);
            p.Poly(new[] { bl, br, br + up, bl + up }, (x, y) => ((int)(x - bl.x) % 4 == 0) ? dark : fill);
            p.Set((int)(br.x - 3), (int)(br.y + height * 0.45f), C(236, 200, 90));
        }

        static void DrawWindowLeft(Px p, Vector2 bl, Color32 shutter, Color32 shutterDark)
        {
            var w = HU * 1.4f; var up = V(0, 10);
            var glass = C(70, 104, 140);
            p.Poly(new[] { bl, bl + w, bl + w + up, bl + up }, glass);
            p.Line(bl + up * 0.5f, bl + w + up * 0.5f, C(236, 236, 230));
            var sL = -HU * 0.8f;
            p.Poly(new[] { bl + sL, bl, bl + up, bl + sL + up }, (x, y) => y % 2 == 0 ? shutter : shutterDark);
            p.Poly(new[] { bl + w, bl + w - sL, bl + w - sL + up, bl + w + up }, (x, y) => y % 2 == 0 ? shutter : shutterDark);
            p.Line(bl + V(0, -1), bl + w + V(0, -1), C(214, 210, 200));   // sill
        }
    }
}
