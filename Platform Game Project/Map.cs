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

    public class TiledMap
    {
        public List<MapCollider> Colliders = new();
        public List<Rectangle> Ladders = new();
        public Rectangle? ItemSpawn;
        public Rectangle? Door;

        private int _scale;
        private Image _tileset;
        private int _tileW, _tileH;
        private int _tilesetCols;
        private List<(int tileId, bool flipH, bool flipV, bool flipD, int col, int row)> _tiles = new(); public TiledMap(string tmjPath, int scale = 3)
        {
            _scale = scale;
            var json = File.ReadAllText(tmjPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            _tileW = root.GetProperty("tilewidth").GetInt32();   // 16
            _tileH = root.GetProperty("tileheight").GetInt32();  // 16
            int mapCols = root.GetProperty("width").GetInt32();  // 30

            // Load tileset — đặt Tileset.png cùng thư mục với file .tmj
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

                // --- Tile layer: lấy dữ liệu tile để render ---
                if (type == "tilelayer" && layer.TryGetProperty("data", out var dataEl))
                {
                    int col = 0, row = 0;
                    foreach (var tile in dataEl.EnumerateArray())
                    {
                        uint rawId = tile.GetUInt32();
                        bool flipH = (rawId & 0x80000000) != 0;
                        bool flipV = (rawId & 0x40000000) != 0;
                        bool flipD = (rawId & 0x20000000) != 0; // diagonal / rotation
                        int id = (int)(rawId & 0x1FFFFFFF);
                        if (id > 0)
                            _tiles.Add((id - 1, flipH, flipV, flipD, col, row));

                        col++;
                        if (col >= mapCols) { col = 0; row++; }
                    }
                    continue;
                }

                // --- Object layer: collision, ladder, door... ---
                if (type != "objectgroup") continue;

                foreach (var obj in layer.GetProperty("objects").EnumerateArray())
                {
                    float ox = obj.GetProperty("x").GetSingle();
                    float oy = obj.GetProperty("y").GetSingle();

                    Rectangle rect;

                    if (obj.TryGetProperty("polygon", out var poly))
                    {
                        // Tính bounding box của polygon
                        float minX = float.MaxValue, minY = float.MaxValue;
                        float maxX = float.MinValue, maxY = float.MinValue;

                        foreach (var pt in poly.EnumerateArray())
                        {
                            float px = pt.GetProperty("x").GetSingle();
                            float py = pt.GetProperty("y").GetSingle();
                            if (px < minX) minX = px;
                            if (py < minY) minY = py;
                            if (px > maxX) maxX = px;
                            if (py > maxY) maxY = py;
                        }

                        // ox, oy là vị trí gốc của object trong Tiled
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
        {
            return new Rectangle(
                (int)(x * _scale),
                (int)(y * _scale),
                Math.Max(1, (int)(w * _scale)),
                Math.Max(1, (int)(h * _scale))
            );
        }

        public void DrawMap(Graphics g)
        {
            if (_tileset == null) return;

            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;

            foreach (var (tileId, flipH, flipV, flipD, col, row) in _tiles)
            {
                int srcX = (tileId % _tilesetCols) * _tileW;
                int srcY = (tileId / _tilesetCols) * _tileH;

                var destRect = new Rectangle(
                    col * _tileW * _scale,
                    row * _tileH * _scale,
                    _tileW * _scale,
                    _tileH * _scale);

                using var bmp = new Bitmap(_tileW, _tileH);
                using (var bg = Graphics.FromImage(bmp))
                {
                    bg.DrawImage(_tileset,
                        new Rectangle(0, 0, _tileW, _tileH),
                        new Rectangle(srcX, srcY, _tileW, _tileH),
                        GraphicsUnit.Pixel);
                }

                // Tiled rotation encoding:
                // 90°  CW  = flipD + flipH
                // 90°  CCW = flipD + flipV
                // 180°     = flipH + flipV
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

        public bool ResolveCollision(ref Rectangle bounds, ref int velocityY)
        {
            bool onGround = false;

            foreach (var col in Colliders)
            {
                if (!bounds.IntersectsWith(col.Bounds)) continue;

                var inter = Rectangle.Intersect(bounds, col.Bounds);

                if (col.IsOneWay)
                {
                    if (velocityY >= 0 && bounds.Bottom - inter.Height <= col.Bounds.Top + 4)
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
                        if (bounds.X < col.Bounds.X) bounds.X -= inter.Width;
                        else bounds.X += inter.Width;
                    }
                    else
                    {
                        if (bounds.Y < col.Bounds.Y)
                        {
                            bounds.Y -= inter.Height;
                            velocityY = 0;
                            onGround = true;
                        }
                        else
                        {
                            bounds.Y += inter.Height;
                            if (velocityY < 0) velocityY = 0;
                        }
                    }
                }
            }

            return onGround;
        }

        public void DrawDebug(Graphics g)
        {
            foreach (var col in Colliders)
                g.DrawRectangle(col.IsOneWay ? Pens.Cyan : Pens.Lime, col.Bounds);
            foreach (var ladder in Ladders)
                g.DrawRectangle(Pens.Brown, ladder);
            if (Door.HasValue)
                g.DrawRectangle(Pens.Gold, Door.Value);
        }
    }
}