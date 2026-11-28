using UnityEngine;

namespace Cosecha.View
{
    /// <summary>
    /// World layout in reference pixels (480×270, origin = screen center, 16 px = 1 unit). Shared with the art generator.
    /// The interactive boards (field + shed) are flat, top-down grids in the sky.
    /// The ground field is a small isometric mirror of the field board (decoration only).
    /// </summary>
    public static class IsoLayout
    {
        public const float PPU = 16f;

        // ---------------------------------------------------------------- sky boards (flat, interactive)
        /// <summary>Native size of the board sprites (cell, highlight) and the shop preview cell. Boards scale them.</summary>
        public const float BoardCellPx = 20f;
        /// <summary>Top-left corner of the field board (just below the HUD strip). The board grows downwards/right
        /// (new rows at the bottom, new columns at the right); the maximum is 12×7.</summary>
        public static readonly Vector2 BoardTopLeftPx = new Vector2(-232, 92);
        /// <summary>Free area for the field board: right up to the shed, down to just above the toast/hint strip.</summary>
        public static readonly Vector2 FieldAreaPx = new Vector2(310, 184);
        /// <summary>Largest cell pitch (cell + gap) of the field board. The board keeps it while the field fits
        /// into <see cref="FieldAreaPx"/> and only shrinks once it would grow into the shed or the bottom strip.</summary>
        public const float FieldMaxPitchPx = 44f;
        /// <summary>Gap between cells as a share of the pitch.</summary>
        public const float CellGapShare = 0.14f;
        /// <summary>Top-left corner of the shed board, right of the field board (5×3 cells, bottom edge at y = −6,
        /// right edge clear of the closed shop handle).</summary>
        public static readonly Vector2 ShedBoardTopLeftPx = new Vector2(86, 72);
        public const float ShedPitchPx = 26f;
        public static readonly Vector2 CompostPx = new Vector2(138, -24);
        public const float CompostScale = 1.5f;
        public static readonly Vector2 CompostHalfSizePx = new Vector2(27, 17);

        /// <summary>uGUI reference resolution (480×270 world × 1.6). Smaller = bigger UI.</summary>
        public static readonly Vector2 CanvasRef = new Vector2(768, 432);
        /// <summary>One board cell in canvas units (shop previews use the same size as the grid).</summary>
        public static float BoardCellCanvas => BoardCellPx * CanvasRef.x / 480f;

        // ---------------------------------------------------------------- ground (isometric, decoration)
        public const float TileW = 12f;   // iso diamond width in px
        public const float TileH = 6f;    // iso diamond height in px
        /// <summary>Left corner of ground cell (0,0). grid x (u) runs down-right, grid y (v) up-right.</summary>
        public static readonly Vector2 FieldOriginPx = new Vector2(-220, -94);
        public static readonly Vector2 MesaU = new Vector2(-1f, 17.5f);
        public static readonly Vector2 MesaV = new Vector2(-1f, 12f);
        public static readonly Vector2 ScarecrowPx = PointPx(FieldOriginPx, 16.8f, 9f);
        /// <summary>Bottom-left of the house sprite.</summary>
        public static readonly Vector2 HouseBottomLeftPx = new Vector2(66, -136);

        /// <summary>Cell pitch of the field board for a field of the given size (whole pixels).</summary>
        public static float FieldPitch(int columns, int rows) =>
            Mathf.Floor(Mathf.Min(FieldMaxPitchPx, FieldAreaPx.x / Mathf.Max(1, columns), FieldAreaPx.y / Mathf.Max(1, rows)));

        public static Vector2 PointPx(Vector2 origin, float u, float v) =>
            origin + new Vector2((u + v) * TileW * 0.5f, (v - u) * TileH * 0.5f);

        public static Vector2 ToUV(Vector2 origin, Vector2 px)
        {
            float s = (px.x - origin.x) / (TileW * 0.5f);
            float d = (px.y - origin.y) / (TileH * 0.5f);
            return new Vector2((s - d) * 0.5f, (s + d) * 0.5f);
        }

        public static Vector3 World(Vector2 px) => new Vector3(px.x / PPU, px.y / PPU, 0);
        public static Vector2 Px(Vector2 world) => world * PPU;
    }

    /// <summary>
    /// A flat, top-down grid in the sky (field board or shed board), anchored at its top-left corner.
    /// Cell (0,0) is the top-left cell; the row index grows downwards, so new rows appear at the bottom.
    /// Cells are <see cref="PitchPx"/> apart and separated by a gap of <see cref="GapPx"/>.
    /// </summary>
    public class FlatGrid
    {
        /// <summary>Top-left corner of cell (0,0) in reference pixels.</summary>
        public readonly Vector2 TopLeftPx;
        public float PitchPx { get; private set; }
        public float GapPx { get; private set; }
        /// <summary>Visible size of one cell in reference pixels.</summary>
        public float CellPx => PitchPx - GapPx;
        /// <summary>Visible size of one cell in world units.</summary>
        public float CellUnits => CellPx / IsoLayout.PPU;
        /// <summary>Scale for sprites drawn at their native 20 px cell size.</summary>
        public float SpriteScale => CellPx / IsoLayout.BoardCellPx;

        public FlatGrid(Vector2 topLeftPx, float pitchPx) { TopLeftPx = topLeftPx; SetPitch(pitchPx); }

        public void SetPitch(float pitchPx)
        {
            PitchPx = pitchPx;
            GapPx = Mathf.Max(2f, Mathf.Round(pitchPx * IsoLayout.CellGapShare));
        }

        public Vector3 CellCenter(Vector2Int c) => Corner(c.x + 0.5f, c.y + 0.5f);

        /// <summary>Center of a word bar of <paramref name="size"/> cells starting at <paramref name="anchor"/>.</summary>
        public Vector3 BarCenter(Vector2Int anchor, int size) => Corner(anchor.x + size / 2f, anchor.y + 0.5f);

        /// <summary>Visible size of a word bar in world units (the gaps inside the bar are filled).</summary>
        public Vector2 BarSize(int size) => new Vector2(size * PitchPx - GapPx, CellPx) / IsoLayout.PPU;

        public Vector2Int WorldToCell(Vector2 world)
        {
            var p = IsoLayout.Px(world) - TopLeftPx;
            return new Vector2Int(Mathf.FloorToInt(p.x / PitchPx), Mathf.FloorToInt(-p.y / PitchPx));
        }

        /// <summary>Point at (x columns right, y rows down) from the top-left corner, in pitch units.</summary>
        public Vector3 Corner(float x, float y) => IsoLayout.World(TopLeftPx + new Vector2(x, -y) * PitchPx);
    }

    /// <summary>The isometric ground grid (decoration).</summary>
    public class IsoGrid
    {
        public readonly Vector2 OriginPx;
        public IsoGrid(Vector2 originPx) { OriginPx = originPx; }
        public Vector3 CellCenter(Vector2Int c) => IsoLayout.World(IsoLayout.PointPx(OriginPx, c.x + 0.5f, c.y + 0.5f));
    }
}
