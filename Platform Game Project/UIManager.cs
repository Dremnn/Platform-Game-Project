using System;
using System.Collections.Generic;
using System.Drawing;

namespace Platform_Game_Project
{
    public class BuffEntry
    {
        public string Label; // "HP", "DMG", "KB"
        public int Value;
        public Image Icon;
    }

    public class UIManager
    {
        private Font pixelFont;
        private Font pixelFontSmall;
        private int screenWidth;
        private int screenHeight;

        // Màu retro
        private static readonly Color C_BORDER = Color.FromArgb(255, 255, 220, 100);
        private static readonly Color C_BG = Color.FromArgb(180, 20, 20, 40);
        private static readonly Color C_HP_BG = Color.FromArgb(180, 80, 20, 20);
        private static readonly Color C_HP_FILL = Color.FromArgb(220, 200, 40, 40);
        private static readonly Color C_HP_SHINE = Color.FromArgb(80, 255, 180, 180);
        private static readonly Color C_SOUL = Color.FromArgb(255, 160, 100, 255);
        private static readonly Color C_SOUL_FILL = Color.FromArgb(220, 120, 60, 220);
        private static readonly Color C_BUFF_DMG = Color.FromArgb(220, 220, 80, 30);
        private static readonly Color C_BUFF_HP = Color.FromArgb(220, 200, 40, 40);
        private static readonly Color C_BUFF_KB = Color.FromArgb(220, 50, 120, 220);

        public UIManager(int screenWidth, int screenHeight)
        {
            this.screenWidth = screenWidth;
            this.screenHeight = screenHeight;
            pixelFont = new Font("Courier New", 11, FontStyle.Bold);
            pixelFontSmall = new Font("Courier New", 8, FontStyle.Bold);
        }

        public void Draw(Graphics g, Player player, int soul, int soulRequired,
                         int mapCount, List<BuffEntry> buffs, bool isBossMap, bool bossSummoned = false)
        {
            DrawHPBar(g, player);
            DrawSoulBar(g, soul, soulRequired);
            DrawMapCounter(g, mapCount);
            DrawBuffList(g, buffs);

            // Chỉ hiện nút khi đủ soul VÀ chưa vào boss map VÀ chưa từng summon
            if (soul >= soulRequired && !isBossMap && !bossSummoned)
                DrawSummonButton(g);
        }

        // --- HP Bar ---
        private void DrawHPBar(Graphics g, Player player)
        {
            int x = 16, y = 16;
            int barW = 300, barH = 28;

            // Label và bar dùng y cố định, không tăng y
            g.DrawString("VITALITY", new Font("Courier New", 14, FontStyle.Bold),
                new SolidBrush(C_BORDER), x, y);

            int barY = y + 20; // Bar nằm ngay dưới label
            DrawBorder(g, x, barY, barW + 4, barH + 4);
            g.FillRectangle(new SolidBrush(C_HP_BG), x + 2, barY + 2, barW, barH);

            float ratio = Math.Max(0, (float)player.HP / player.MaxHP);
            int fillW = (int)(barW * ratio);
            if (fillW > 0)
            {
                g.FillRectangle(new SolidBrush(C_HP_FILL), x + 2, barY + 2, fillW, barH);
                g.FillRectangle(new SolidBrush(C_HP_SHINE), x + 2, barY + 2, fillW, 4);
            }

            g.DrawString($"{player.HP}/{player.MaxHP}",
                new Font("Courier New", 13, FontStyle.Bold),
                Brushes.White, x + 6, barY + 5);
        }

        private void DrawSoulBar(Graphics g, int soul, int soulRequired)
        {
            int x = 16, y = 80;
            int barW = 150, barH = 14;

            g.DrawString("SOUL", new Font("Courier New", 10, FontStyle.Bold),
                new SolidBrush(C_SOUL), x, y);

            int barY = y + 14; // Bar nằm ngay dưới label
            DrawBorder(g, x, barY, barW + 4, barH + 4);
            g.FillRectangle(new SolidBrush(C_BG), x + 2, barY + 2, barW, barH);

            float ratio = Math.Min(1f, (float)soul / soulRequired);
            int fillW = (int)(barW * ratio);
            if (fillW > 0)
            {
                g.FillRectangle(new SolidBrush(C_SOUL_FILL), x + 2, barY + 2, fillW, barH);
                g.FillRectangle(new SolidBrush(Color.FromArgb(60, 255, 255, 255)),
                    x + 2, barY + 2, fillW, 3);
            }

            g.DrawString($"{soul}/{soulRequired}",
                new Font("Courier New", 8, FontStyle.Bold),
                Brushes.White, x + 4, barY + 2);
        }

        // --- Map Counter ---
        private void DrawMapCounter(Graphics g, int mapCount)
        {
            int x = 16, y = 125;
            DrawBorder(g, x, y, 150, 22);
            g.FillRectangle(new SolidBrush(C_BG), x + 2, y + 2, 146, 18);
            g.DrawString($"MAP : {mapCount}",
                new Font("Courier New", 10, FontStyle.Bold),
                new SolidBrush(C_BORDER), x + 6, y + 3);
        }

        // --- Buff Popup ---
        public void DrawBuffPopup(Graphics g, List<BuffEntry> choices)
        {
            if (choices == null || choices.Count == 0) return;

            int screenCenterX = screenWidth / 2;
            int popupW = 520, popupH = 180;
            int popupX = screenCenterX - popupW / 2;
            int popupY = 380;

            // Overlay tối phía sau
            g.FillRectangle(new SolidBrush(Color.FromArgb(160, 0, 0, 0)),
                0, 0, screenWidth, 960);

            // Background popup
            g.FillRectangle(new SolidBrush(Color.FromArgb(220, 20, 10, 40)),
                popupX, popupY, popupW, popupH);
            DrawBorder(g, popupX, popupY, popupW, popupH);

            // Title
            g.DrawString("— ESSENCE OBTAINED —",
                new Font("Courier New", 13, FontStyle.Bold),
                new SolidBrush(C_BORDER),
                popupX + 100, popupY + 12);

            // 3 lựa chọn buff
            int cardW = 140, cardH = 90;
            int startX = popupX + 20;
            int cardY = popupY + 45;
            int gap = 20;

            for (int i = 0; i < choices.Count; i++)
            {
                var buff = choices[i];
                int cardX = startX + i * (cardW + gap);

                Color buffColor = buff.Label switch
                {
                    "DMG" => C_BUFF_DMG,
                    "HP" => C_BUFF_HP,
                    "KB" => C_BUFF_KB,
                    _ => Color.Gray
                };

                // Card background
                g.FillRectangle(new SolidBrush(Color.FromArgb(180, 30, 15, 60)),
                    cardX, cardY, cardW, cardH);
                DrawBorder(g, cardX, cardY, cardW, cardH);

                // Số phím bấm [1] [2] [3]
                g.FillRectangle(new SolidBrush(buffColor), cardX + 6, cardY + 6, 22, 22);
                g.DrawString($"{i + 1}",
                    new Font("Courier New", 11, FontStyle.Bold),
                    Brushes.White, cardX + 10, cardY + 7);

                // Tên buff
                g.DrawString(buff.Label,
                    new Font("Courier New", 14, FontStyle.Bold),
                    new SolidBrush(buffColor),
                    cardX + 36, cardY + 8);

                // Giá trị
                g.DrawString($"+ {buff.Value}",
                    new Font("Courier New", 18, FontStyle.Bold),
                    Brushes.White,
                    cardX + cardW / 2 - 25, cardY + 38);

                // Mô tả
                string desc = buff.Label switch
                {
                    "DMG" => "Attack Power",
                    "HP" => "Max Vitality",
                    "KB" => "Knockback",
                    _ => ""
                };
                g.DrawString(desc,
                    new Font("Courier New", 8, FontStyle.Bold),
                    new SolidBrush(Color.FromArgb(180, 200, 200, 200)),
                    cardX + 8, cardY + 70);
            }

            // Hint
            g.DrawString("Press  1 / 2 / 3  to choose",
                new Font("Courier New", 9, FontStyle.Bold),
                new SolidBrush(Color.FromArgb(180, 255, 255, 255)),
                popupX + 140, popupY + 155);
        }

        // --- Buff List (góc trên trái) --- Hiện buff đã chọn
        public void DrawBuffList(Graphics g, List<BuffEntry> buffs)
        {
            if (buffs == null || buffs.Count == 0) return;

            int x = 16, y = 158;

            g.DrawString("ESSENCE",
                new Font("Courier New", 10, FontStyle.Bold),
                new SolidBrush(C_BORDER), x, y);
            y += 16;

            // Gộp buff cùng loại lại — hiện tổng thay vì từng cái riêng
            var grouped = new Dictionary<string, int>();
            foreach (var buff in buffs)
            {
                if (!grouped.ContainsKey(buff.Label)) grouped[buff.Label] = 0;
                grouped[buff.Label] += buff.Value;
            }

            foreach (var kv in grouped)
            {
                int iconSize = 18;
                int itemW = 120;

                Color buffColor = kv.Key switch
                {
                    "DMG" => C_BUFF_DMG,
                    "HP" => C_BUFF_HP,
                    "KB" => C_BUFF_KB,
                    _ => Color.Gray
                };

                DrawBorder(g, x, y, itemW, iconSize + 4);
                g.FillRectangle(new SolidBrush(C_BG), x + 2, y + 2, itemW - 4, iconSize);

                // Icon màu
                g.FillRectangle(new SolidBrush(buffColor), x + 2, y + 2, iconSize, iconSize);
                g.DrawString(kv.Key[0].ToString(),
                    new Font("Courier New", 8, FontStyle.Bold),
                    Brushes.White, x + 4, y + 3);

                // Label + tổng value
                g.DrawString($"{kv.Key}  +{kv.Value}",
                    new Font("Courier New", 9, FontStyle.Bold),
                    new SolidBrush(buffColor), x + iconSize + 6, y + 3);

                y += iconSize + 5;
            }
        }

        // --- Summon Boss Button ---
        private void DrawSummonButton(Graphics g)
        {
            string text = "[ SUMMON BOSS ]";
            int btnW = 300, btnH = 45;
            int x = screenWidth / 2 - btnW / 2;
            int y = 900;

            bool blink = (DateTime.Now.Millisecond / 400) % 2 == 0;
            Color btnColor = blink
                ? Color.FromArgb(200, 160, 50, 200)
                : Color.FromArgb(200, 100, 20, 150);

            g.FillRectangle(new SolidBrush(btnColor), x, y, btnW, btnH);
            DrawBorder(g, x, y, btnW, btnH);
            g.DrawString(text, new Font("Courier New", 16, FontStyle.Bold),
                Brushes.White, x + 18, y + 8);
        }

        // Viền pixel retro
        private void DrawBorder(Graphics g, int x, int y, int w, int h)
        {
            using var pen = new Pen(C_BORDER, 1);
            g.DrawRectangle(pen, x, y, w, h);
        }

        public void DrawMenu(Graphics g)
        {
            g.Clear(Color.FromArgb(10, 10, 18));
            int W = screenWidth, H = screenHeight;
            var gold = Color.FromArgb(200, 168, 75);
            var goldDim = Color.FromArgb(122, 106, 58);
            var goldFaint = Color.FromArgb(58, 48, 48);
            var pen = new Pen(gold, 2);
            int m = 18;
            g.DrawLines(pen, new[] { new Point(m + 40, m), new Point(m, m), new Point(m, m + 40) });
            g.DrawLines(pen, new[] { new Point(W - m - 40, m), new Point(W - m, m), new Point(W - m, m + 40) });
            g.DrawLines(pen, new[] { new Point(m + 40, H - m), new Point(m, H - m), new Point(m, H - m - 40) });
            g.DrawLines(pen, new[] { new Point(W - m - 40, H - m), new Point(W - m, H - m), new Point(W - m, H - m - 40) });
            pen.Dispose();

            string title = "SOULFORGE";
            var titleFont = new Font("Courier New", 52, FontStyle.Bold);
            var sz = g.MeasureString(title, titleFont);
            g.DrawString(title, titleFont, new SolidBrush(gold), (W - sz.Width) / 2, H / 2 - 130);

            // Changed text and pushed down by adjusting the Y offset
            string sub = "Group 9 project";
            var subFont = new Font("Courier New", 11, FontStyle.Bold);
            var subSz = g.MeasureString(sub, subFont);
            g.DrawString(sub, subFont, new SolidBrush(goldDim), (W - subSz.Width) / 2, H / 2 - 30);

            // Pushed the decorative line down
            g.FillRectangle(new SolidBrush(Color.FromArgb(128, 200, 168, 75)),
                (W - 180) / 2, H / 2 - 6, 180, 1);

            string btn = "[ ENTER ]  NEW GAME";
            var btnFont = new Font("Courier New", 14, FontStyle.Bold);
            var btnSz = g.MeasureString(btn, btnFont);

            // Pushed the button down 
            int bx = (int)((W - btnSz.Width) / 2) - 20, by = H / 2 + 20;
            g.DrawRectangle(new Pen(gold, 1), bx - 10, by - 4, btnSz.Width + 40, btnSz.Height + 8);
            g.FillRectangle(new SolidBrush(Color.FromArgb(17, 200, 168, 75)),
                bx - 10, by - 4, btnSz.Width + 40, btnSz.Height + 8);
            g.DrawString(btn, btnFont, new SolidBrush(Color.FromArgb(232, 208, 112)), bx, by);

            // Nút Tutorial [T]
            string btnT = "[ T ]  TUTORIAL";
            var btnTFont = new Font("Courier New", 14, FontStyle.Bold);
            var btnTSz = g.MeasureString(btnT, btnTFont);
            int txBtn = (int)((W - btnTSz.Width) / 2) - 20;
            int tyBtn = H / 2 + 80; // Dưới nút ENTER

            g.DrawRectangle(new Pen(Color.FromArgb(80, 200, 168, 75), 1),
                txBtn - 10, tyBtn - 4, btnTSz.Width + 40, btnTSz.Height + 8);
            g.FillRectangle(new SolidBrush(Color.FromArgb(12, 200, 168, 75)),
                txBtn - 10, tyBtn - 4, btnTSz.Width + 40, btnTSz.Height + 8);
            g.DrawString(btnT, btnTFont,
                new SolidBrush(Color.FromArgb(160, 200, 168, 75)), txBtn, tyBtn);

            // Removed "v0.1" from the string
            string ver = "PRESS ENTER TO BEGIN";
            var verSz = g.MeasureString(ver, new Font("Courier New", 10, FontStyle.Regular));
            g.DrawString(ver, new Font("Courier New", 10, FontStyle.Regular),
                new SolidBrush(Color.FromArgb(60, 60, 74)), (W - verSz.Width) / 2, H - 40);
        }

        public void DrawTutorial(Graphics g)
        {
            g.Clear(Color.FromArgb(10, 10, 18));
            int W = screenWidth, H = screenHeight;
            var gold = Color.FromArgb(200, 168, 75);
            var white = Color.FromArgb(200, 200, 200);

            // Title
            string title = "TUTORIAL";
            var titleFont = new Font("Courier New", 36, FontStyle.Bold);
            var titleSz = g.MeasureString(title, titleFont);
            g.DrawString(title, titleFont, new SolidBrush(gold), (W - titleSz.Width) / 2, 60);

            // Controls
            var font = new Font("Courier New", 13, FontStyle.Bold);
            var entries = new[]
            {
        ("A / D",       "Move left / right"),
        ("SPACE",       "Jump"),
        ("SHIFT",       "Dash"),
        ("J",           "Light Attack"),
        ("J + J",       "Heavy Attack (combo)"),
        ("SHIFT + J",   "Dash Attack"),
        ("W / S",       "Climb ladder"),
        ("S",           "Drop through platform"),
        ("B",           "Summon Boss (need 100 Soul)"),
    };

            int startY = 150;
            foreach (var (key, desc) in entries)
            {
                var keySz = g.MeasureString(key, font);
                g.DrawString(key, font, new SolidBrush(gold), W / 2 - 300, startY);
                g.DrawString("—", font, new SolidBrush(white), W / 2 - 80, startY);
                g.DrawString(desc, font, new SolidBrush(white), W / 2 - 50, startY);
                startY += 40;
            }

            // Back
            string back = "[ ESC ]  BACK TO MENU";
            var backFont = new Font("Courier New", 12, FontStyle.Bold);
            var backSz = g.MeasureString(back, backFont);
            g.DrawString(back, backFont,
                new SolidBrush(Color.FromArgb(120, 120, 120)),
                (W - backSz.Width) / 2, H - 60);
        }

        public void DrawGameOver(Graphics g, int soul, int mapCount) 
        {
            g.Clear(Color.FromArgb(10, 10, 18));
            int W = screenWidth, H = screenHeight;
            var red = Color.FromArgb(139, 26, 26);
            var gold = Color.FromArgb(200, 168, 75);
            g.FillRectangle(new SolidBrush(Color.FromArgb(178, 139, 26, 26)), 0, 0, W, 6);

            var pen = new Pen(red, 2);
            int m = 18;
            g.DrawLines(pen, new[] { new Point(m + 40, m), new Point(m, m), new Point(m, m + 40) });
            g.DrawLines(pen, new[] { new Point(W - m - 40, m), new Point(W - m, m), new Point(W - m, m + 40) });
            g.DrawLines(pen, new[] { new Point(m + 40, H - m), new Point(m, H - m), new Point(m, H - m - 40) });
            g.DrawLines(pen, new[] { new Point(W - m - 40, H - m), new Point(W - m, H - m), new Point(W - m, H - m - 40) });
            pen.Dispose();

            string fallen = "— YOU HAVE FALLEN —";
            var fallenFont = new Font("Courier New", 11, FontStyle.Bold);
            var fallenSz = g.MeasureString(fallen, fallenFont);
            g.DrawString(fallen, fallenFont, new SolidBrush(Color.FromArgb(90, 42, 42)),
                (W - fallenSz.Width) / 2, H / 2 - 130);

            string title = "GAME OVER";
            var titleFont = new Font("Courier New", 54, FontStyle.Bold);
            var titleSz = g.MeasureString(title, titleFont);
            g.DrawString(title, titleFont, new SolidBrush(red), (W - titleSz.Width) / 2, H / 2 - 100);

            g.FillRectangle(new SolidBrush(Color.FromArgb(153, 139, 26, 26)), W / 2 - 1, H / 2 + 10, 2, 50);

            var statFont = new Font("Courier New", 13, FontStyle.Bold);
            string s1 = $"SOUL COLLECTED   {soul}";
            string s2 = $"MAPS CLEARED   {mapCount}";
            var s1Sz = g.MeasureString(s1, statFont);
            var s2Sz = g.MeasureString(s2, statFont);
            // vẽ phần label trắng mờ, phần số màu vàng
            g.DrawString("SOUL COLLECTED   ", statFont,
                new SolidBrush(Color.FromArgb(122, 90, 90)), (W - s1Sz.Width) / 2, H / 2 + 70);
            var labelW = g.MeasureString("SOUL COLLECTED   ", statFont).Width;
            g.DrawString(soul.ToString(), statFont,
                new SolidBrush(gold), (W - s1Sz.Width) / 2 + labelW, H / 2 + 70);

            g.DrawString("MAPS CLEARED   ", statFont,
                new SolidBrush(Color.FromArgb(122, 90, 90)), (W - s2Sz.Width) / 2, H / 2 + 96);
            var label2W = g.MeasureString("MAPS CLEARED   ", statFont).Width;
            g.DrawString(mapCount.ToString(), statFont,
                new SolidBrush(gold), (W - s2Sz.Width) / 2 + label2W, H / 2 + 96);

            g.FillRectangle(new SolidBrush(Color.FromArgb(128, 139, 26, 26)),
                (W - 180) / 2, H / 2 + 128, 180, 1);

            var btnFont = new Font("Courier New", 13, FontStyle.Bold);
            string b1 = "[ ENTER ]  RETRY";
            string b2 = "[ ESC ]  MENU";
            var b1Sz = g.MeasureString(b1, btnFont);
            var b2Sz = g.MeasureString(b2, btnFont);
            int totalW = (int)(b1Sz.Width + b2Sz.Width) + 60;
            int startX = (W - totalW) / 2;
            int btnY = H / 2 + 142;
            g.DrawRectangle(new Pen(Color.FromArgb(136, 139, 26, 26), 1),
                startX - 10, btnY - 4, b1Sz.Width + 20, b1Sz.Height + 8);
            g.DrawString(b1, btnFont, new SolidBrush(Color.FromArgb(200, 112, 112)),
                startX, btnY);
            int b2X = startX + (int)b1Sz.Width + 60;
            g.DrawRectangle(new Pen(Color.FromArgb(68, 68, 68, 68), 1),
                b2X - 10, btnY - 4, b2Sz.Width + 20, b2Sz.Height + 8);
            g.DrawString(b2, btnFont, new SolidBrush(Color.FromArgb(102, 102, 102)), b2X, btnY);
        }
        public void DrawGameClear(Graphics g, int soul, int mapCount, int buffCount) 
        {
            g.Clear(Color.FromArgb(10, 10, 18));
            int W = screenWidth, H = screenHeight;
            var gold = Color.FromArgb(200, 168, 75);
            var goldDim = Color.FromArgb(122, 106, 58);
            var pen = new Pen(gold, 2);
            int m = 18;
            g.DrawLines(pen, new[] { new Point(m + 40, m), new Point(m, m), new Point(m, m + 40) });
            g.DrawLines(pen, new[] { new Point(W - m - 40, m), new Point(W - m, m), new Point(W - m, m + 40) });
            g.DrawLines(pen, new[] { new Point(m + 40, H - m), new Point(m, H - m), new Point(m, H - m - 40) });
            g.DrawLines(pen, new[] { new Point(W - m - 40, H - m), new Point(W - m, H - m), new Point(W - m, H - m - 40) });
            pen.Dispose();

            string stars = "✦       ✦       ✦";
            var starFont = new Font("Courier New", 11, FontStyle.Regular);
            var starSz = g.MeasureString(stars, starFont);
            g.DrawString(stars, starFont, new SolidBrush(Color.FromArgb(68, 200, 168, 75)),
                (W - starSz.Width) / 2, H / 2 - 148);

            string title = "VICTORY";
            var titleFont = new Font("Courier New", 58, FontStyle.Bold);
            var titleSz = g.MeasureString(title, titleFont);
            g.DrawString(title, titleFont, new SolidBrush(gold), (W - titleSz.Width) / 2, H / 2 - 120);

            string sub = "— THE DARKNESS HAS BEEN VANQUISHED —";
            var subFont = new Font("Courier New", 11, FontStyle.Bold);
            var subSz = g.MeasureString(sub, subFont);
            g.DrawString(sub, subFont, new SolidBrush(goldDim), (W - subSz.Width) / 2, H / 2 - 28);

            g.FillRectangle(new SolidBrush(Color.FromArgb(128, 200, 168, 75)),
                (W - 180) / 2, H / 2 - 8, 180, 1);

            // Stat box 3 cột
            int boxW = 360, boxH = 64, boxX = (W - boxW) / 2, boxY = H / 2 + 4;
            g.DrawRectangle(new Pen(Color.FromArgb(51, 200, 168, 75), 1), boxX, boxY, boxW, boxH);
            int cellW = boxW / 3;
            string[] labels = { "SOUL", "MAPS", "BUFFS" };
            string[] vals = { soul.ToString(), mapCount.ToString(), buffCount.ToString() };
            var lbFont = new Font("Courier New", 10, FontStyle.Bold);
            var valFont = new Font("Courier New", 20, FontStyle.Bold);
            for (int i = 0; i < 3; i++)
            {
                int cx = boxX + i * cellW;
                if (i > 0) g.DrawLine(new Pen(Color.FromArgb(34, 200, 168, 75), 1),
                    cx, boxY, cx, boxY + boxH);
                var lbSz = g.MeasureString(labels[i], lbFont);
                g.DrawString(labels[i], lbFont, new SolidBrush(Color.FromArgb(80, 80, 48)),
                    cx + (cellW - lbSz.Width) / 2, boxY + 8);
                var vSz = g.MeasureString(vals[i], valFont);
                g.DrawString(vals[i], valFont, new SolidBrush(gold),
                    cx + (cellW - vSz.Width) / 2, boxY + 28);
            }

            var btnFont = new Font("Courier New", 13, FontStyle.Bold);
            string b1 = "[ ENTER ]  PLAY AGAIN";
            string b2 = "[ ESC ]  MENU";
            var b1Sz = g.MeasureString(b1, btnFont);
            var b2Sz = g.MeasureString(b2, btnFont);
            int totalW = (int)(b1Sz.Width + b2Sz.Width) + 60;
            int startX = (W - totalW) / 2;
            int btnY = H / 2 + 84;
            g.DrawRectangle(new Pen(Color.FromArgb(136, 200, 168, 75), 1),
                startX - 10, btnY - 4, b1Sz.Width + 20, b1Sz.Height + 8);
            g.DrawString(b1, btnFont, new SolidBrush(gold), startX, btnY);
            int b2X = startX + (int)b1Sz.Width + 60;
            g.DrawRectangle(new Pen(Color.FromArgb(68, 68, 68, 68), 1),
                b2X - 10, btnY - 4, b2Sz.Width + 20, b2Sz.Height + 8);
            g.DrawString(b2, btnFont, new SolidBrush(Color.FromArgb(102, 102, 102)), b2X, btnY);
        }
    }
}