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

        public UIManager(int screenWidth)
        {
            this.screenWidth = screenWidth;
            pixelFont = new Font("Courier New", 11, FontStyle.Bold);
            pixelFontSmall = new Font("Courier New", 8, FontStyle.Bold);
        }

        public void Draw(Graphics g, Player player, int soul, int soulRequired,
                         int mapCount, List<BuffEntry> buffs)
        {
            DrawHPBar(g, player);
            DrawSoulBar(g, soul, soulRequired);
            DrawMapCounter(g, mapCount);
            DrawBuffList(g, buffs);

            if (soul >= soulRequired)
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
    }
}