using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;

namespace Platform_Game_Project
{
    public class MapCollider
    {
        public Rectangle Bounds;
        public bool IsOneWay;
    }

    // ── Stair polygon: lưu vertices thật để tính mặt bậc chính xác ──
    public class StairPolygon
    {
        public List<PointF> Vertices = new();
        public Rectangle Bounds; // AABB để check nhanh

        /// <summary>
        /// Tìm Y mặt bậc (cạnh ngang cao nhất) tại worldX.
        /// Trả về float.MaxValue nếu X nằm ngoài phạm vi polygon.
        /// </summary>
        public float GetSurfaceYAt(float worldX)
        {
            float bestY = float.MaxValue;

            for (int i = 0; i < Vertices.Count; i++)
            {
                var a = Vertices[i];
                var b = Vertices[(i + 1) % Vertices.Count];

                // Bỏ qua cạnh dọc (riser của bậc thang)
                if (MathF.Abs(b.X - a.X) < 0.5f) continue;

                float minX = MathF.Min(a.X, b.X);
                float maxX = MathF.Max(a.X, b.X);
                if (worldX < minX || worldX > maxX) continue;

                // Nội suy Y tại worldX trên cạnh này
                float t = (worldX - a.X) / (b.X - a.X);
                float y = a.Y + t * (b.Y - a.Y);

                // Lấy cạnh có Y nhỏ nhất = cao nhất màn hình = mặt bậc
                if (y < bestY) bestY = y;
            }

            return bestY;
        }
    }

    public class TiledMap
    {
        public List<MapCollider> Colliders = new();
        public List<Rectangle> Ladders = new();
        public List<StairPolygon> Stairs = new();   // polygon thật
        public List<Rectangle> Spikes = new();
        public Rectangle? ItemSpawn;
        public Rectangle? Door;

        private int _scale;
        private Image _tileset;
        private int _tileW, _tileH;
        private int _tilesetCols;
        private List<(int tileId, bool flipH, bool flipV, bool flipD, int col, int row)> _tiles = new();

        public TiledMap(string tmjPath, int scale = 3)
        {
            _scale = scale;
            var json = File.ReadAllText(tmjPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            _tileW = root.GetProperty("tilewidth").GetInt32();
            _tileH = root.GetProperty("tileheight").GetInt32();
            int mapCols = root.GetProperty("width").GetInt32();

            string mapDir = Path.GetDirectoryName(tmjPath)!;
            string tilesetPath = Path.Combine(mapDir, "Tileset.png");
            if (File.Exists(tilesetPath))
            {
                _tileset = Image.FromFile(tilesetPath);
                _tilesetCols = _tileset.Width / _tileW;
            }

            foreach (var layer in root.GetProperty("layers").EnumerateArray())
            {
                string name = layer.GetProperty("name").GetString() ?? "";
                string type = layer.GetProperty("type").GetString() ?? "";

                // ── Tile layer ──
                if (type == "tilelayer" && layer.TryGetProperty("data", out var dataEl))
                {
                    int col = 0, row = 0;
                    foreach (var tile in dataEl.EnumerateArray())
                    {
                        uint rawId = tile.GetUInt32();
                        bool flipH = (rawId & 0x80000000) != 0;
                        bool flipV = (rawId & 0x40000000) != 0;
                        bool flipD = (rawId & 0x20000000) != 0;
                        int id = (int)(rawId & 0x1FFFFFFF);
                        if (id > 0) _tiles.Add((id - 1, flipH, flipV, flipD, col, row));
                        col++;
                        if (col >= mapCols) { col = 0; row++; }
                    }
                    continue;
                }

                if (type != "objectgroup") continue;

                // ── Object layer ──
                foreach (var obj in layer.GetProperty("objects").EnumerateArray())
                {
                    float ox = obj.GetProperty("x").GetSingle();
                    float oy = obj.GetProperty("y").GetSingle();

                    // ── Stair: parse polygon giữ nguyên vertices ──
                    if (name == "Stair" && obj.TryGetProperty("polygon", out var polyStair))
                    {
                        var stair = new StairPolygon();
                        float minX = float.MaxValue, minY = float.MaxValue;
                        float maxX = float.MinValue, maxY = float.MinValue;

                        foreach (var pt in polyStair.EnumerateArray())
                        {
                            float px = (ox + pt.GetProperty("x").GetSingle()) * _scale;
                            float py = (oy + pt.GetProperty("y").GetSingle()) * _scale;
                            stair.Vertices.Add(new PointF(px, py));
                            if (px < minX) minX = px; if (py < minY) minY = py;
                            if (px > maxX) maxX = px; if (py > maxY) maxY = py;
                        }

                        stair.Bounds = new Rectangle(
                            (int)minX, (int)minY,
                            Math.Max(1, (int)(maxX - minX)),
                            Math.Max(1, (int)(maxY - minY)));

                        Stairs.Add(stair);
                        continue; // không parse thêm bên dưới
                    }

                    // ── Các object còn lại: lấy bounding box ──
                    Rectangle rect;
                    if (obj.TryGetProperty("polygon", out var poly))
                    {
                        float minX = float.MaxValue, minY = float.MaxValue;
                        float maxX = float.MinValue, maxY = float.MinValue;
                        foreach (var pt in poly.EnumerateArray())
                        {
                            float px = pt.GetProperty("x").GetSingle();
                            float py = pt.GetProperty("y").GetSingle();
                            if (px < minX) minX = px; if (py < minY) minY = py;
                            if (px > maxX) maxX = px; if (py > maxY) maxY = py;
                        }
                        rect = ScaleRect(ox + minX, oy + minY, maxX - minX, maxY - minY);
                    }
                    else
                    {
                        float ow = obj.GetProperty("width").GetSingle();
                        float oh = obj.GetProperty("height").GetSingle();
                        rect = ScaleRect(ox, oy, ow, oh);
                    }

                    switch (name)
                    {
                        case "Ground":
                            Colliders.Add(new MapCollider { Bounds = rect, IsOneWay = false });
                            break;
                        case "One-Way":
                            Colliders.Add(new MapCollider { Bounds = rect, IsOneWay = true });
                            break;
                        case "Spike":
                            Spikes.Add(rect);
                            break;
                        case "Ladder":
                            Ladders.Add(rect);
                            break;
                        case "Items":
                            ItemSpawn = rect;
                            break;
                        case "Door":
                            Door = rect;
                            break;
                    }
                }
            }
        }

        private Rectangle ScaleRect(float x, float y, float w, float h)
            => new Rectangle((int)(x * _scale), (int)(y * _scale),
                             Math.Max(1, (int)(w * _scale)), Math.Max(1, (int)(h * _scale)));

        // ────────────────────────────────────────────────────
        //  DRAW
        // ────────────────────────────────────────────────────
        public void DrawMap(Graphics g)
        {
            if (_tileset == null) return;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;

            foreach (var (tileId, flipH, flipV, flipD, col, row) in _tiles)
            {
                int srcX = (tileId % _tilesetCols) * _tileW;
                int srcY = (tileId / _tilesetCols) * _tileH;
                var destRect = new Rectangle(col * _tileW * _scale, row * _tileH * _scale,
                                             _tileW * _scale, _tileH * _scale);

                using var bmp = new Bitmap(_tileW, _tileH);
                using (var bg = Graphics.FromImage(bmp))
                    bg.DrawImage(_tileset,
                        new Rectangle(0, 0, _tileW, _tileH),
                        new Rectangle(srcX, srcY, _tileW, _tileH),
                        GraphicsUnit.Pixel);

                var flipType = (flipD, flipH, flipV) switch
                {
                    (false, false, false) => RotateFlipType.RotateNoneFlipNone,
                    (false, true, false) => RotateFlipType.RotateNoneFlipX,
                    (false, false, true) => RotateFlipType.RotateNoneFlipY,
                    (false, true, true) => RotateFlipType.RotateNoneFlipXY,
                    (true, true, false) => RotateFlipType.Rotate90FlipNone,
                    (true, false, true) => RotateFlipType.Rotate270FlipNone,
                    (true, false, false) => RotateFlipType.Rotate90FlipY,
                    (true, true, true) => RotateFlipType.Rotate270FlipY,
                };
                bmp.RotateFlip(flipType);
                g.DrawImage(bmp, destRect);
            }

            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Default;
        }

        public void DrawDebug(Graphics g)
        {
            foreach (var col in Colliders)
                g.DrawRectangle(col.IsOneWay ? Pens.Cyan : Pens.Lime, col.Bounds);

            // Vẽ polygon stair thật (màu cam)
            foreach (var stair in Stairs)
            {
                if (stair.Vertices.Count < 2) continue;
                for (int i = 0; i < stair.Vertices.Count; i++)
                {
                    var a = stair.Vertices[i];
                    var b = stair.Vertices[(i + 1) % stair.Vertices.Count];
                    g.DrawLine(Pens.Orange, a, b);
                }
                g.DrawRectangle(Pens.DarkOrange, stair.Bounds); // AABB tham chiếu
            }

            foreach (var spike in Spikes)
                g.DrawRectangle(Pens.Red, spike);
            foreach (var ladder in Ladders)
                g.DrawRectangle(Pens.Brown, ladder);
            if (Door.HasValue)
                g.DrawRectangle(Pens.Gold, Door.Value);
        }

        // ────────────────────────────────────────────────────
        //  COLLISION RESOLUTION
        // ────────────────────────────────────────────────────

        /// <summary>
        /// Resolve collision với Ground và One-Way.
        /// ignoreOneWay = true khi player đang drop-through.
        /// </summary>
        public bool ResolveCollision(ref Rectangle bounds, ref int velocityY,
                                     bool ignoreOneWay = false)
        {
            bool onGround = false;

            foreach (var col in Colliders)
            {
                if (!bounds.IntersectsWith(col.Bounds)) continue;
                var inter = Rectangle.Intersect(bounds, col.Bounds);

                if (col.IsOneWay)
                {
                    if (ignoreOneWay) continue;
                    if (velocityY < 0) continue; // Đang bay lên → bỏ qua

                    // Snap khi đáy player chạm hoặc vừa vượt qua đỉnh platform
                    if (bounds.Bottom >= col.Bounds.Top &&
                        bounds.Bottom <= col.Bounds.Top + velocityY + 4)
                    {
                        bounds.Y = col.Bounds.Top - bounds.Height;
                        velocityY = 0;
                        onGround = true;
                    }
                }
                else
                {
                    if (inter.Width < inter.Height)
                    {
                        // Chỉ bỏ qua corner collision ngang — không ảnh hưởng collision dọc
                        if (inter.Width <= 2) continue;
                        if (bounds.X < col.Bounds.X) bounds.X -= inter.Width;
                        else bounds.X += inter.Width;
                    }
                    else
                    {
                        if (bounds.Y < col.Bounds.Y && velocityY >= 0)
                        {
                            bounds.Y -= inter.Height;
                            velocityY = 0;
                            onGround = true;
                        }
                        else if (bounds.Y >= col.Bounds.Y)
                        {
                            bounds.Y += inter.Height;
                            if (velocityY < 0) velocityY = 0;
                        }
                        else
                        {
                            if (inter.Width <= 2) continue; // Corner check chỉ khi đẩy ngang
                            if (bounds.X < col.Bounds.X) bounds.X -= inter.Width;
                            else bounds.X += inter.Width;
                        }
                    }
                }
            }

            return onGround;
        }

        /// <summary>
        /// Chỉ resolve va chạm ngang (dùng khi leo ladder).
        /// </summary>
        public void ResolveHorizontalOnly(ref Rectangle bounds)
        {
            foreach (var col in Colliders)
            {
                if (col.IsOneWay) continue;
                if (!bounds.IntersectsWith(col.Bounds)) continue;
                var inter = Rectangle.Intersect(bounds, col.Bounds);
                if (inter.Width < inter.Height)
                {
                    if (bounds.X < col.Bounds.X) bounds.X -= inter.Width;
                    else bounds.X += inter.Width;
                }
            }
        }
    }
}