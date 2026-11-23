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
        public const float BoardCellPx = 20f;
        /// <summary>Top-left corner of the field board (just below the HUD strip). The board grows downwards/right;
        /// at the maximum 15×11 it covers about half of the screen.</summary>
        public static readonly Vector2 BoardTopLeftPx = new Vector2(-226, 92);
        /// <summary>Bottom-left corner of the shed board, right of the field board.</summary>
        public static readonly Vector2 ShedBoardOriginPx = new Vector2(88, 6);
        public static readonly Vector2 CompostPx = new Vector2(138, -20);
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

    /// <summary>A flat, top-down grid in the sky (field board or shed board).</summary>
    public class FlatGrid
    {
        readonly Vector2 anchorPx;
        readonly bool anchoredTop;
        /// <summary>Number of rows; only used when the grid is anchored at its top edge.</summary>
        public int Rows = 1;

        /// <summary>Bottom-left corner of cell (0,0).</summary>
        public Vector2 OriginPx => anchoredTop ? anchorPx - new Vector2(0, Rows * IsoLayout.BoardCellPx) : anchorPx;

        public FlatGrid(Vector2 anchorPx, bool anchoredTop = false) { this.anchorPx = anchorPx; this.anchoredTop = anchoredTop; }

        public Vector3 CellCenter(Vector2Int c) =>
            IsoLayout.World(OriginPx + new Vector2(c.x + 0.5f, c.y + 0.5f) * IsoLayout.BoardCellPx);

        public Vector2Int WorldToCell(Vector2 world)
        {
            var p = (IsoLayout.Px(world) - OriginPx) / IsoLayout.BoardCellPx;
            return new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));
        }

        public Vector3 Corner(float x, float y) => IsoLayout.World(OriginPx + new Vector2(x, y) * IsoLayout.BoardCellPx);
    }

    /// <summary>The isometric ground grid (decoration).</summary>
    public class IsoGrid
    {
        public readonly Vector2 OriginPx;
        public IsoGrid(Vector2 originPx) { OriginPx = originPx; }
        public Vector3 CellCenter(Vector2Int c) => IsoLayout.World(IsoLayout.PointPx(OriginPx, c.x + 0.5f, c.y + 0.5f));
    }
}
