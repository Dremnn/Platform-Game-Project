using System;
using System.Collections.Generic;
using System.Text;

namespace Platform_Game_Project
{
    public class Enemy
    {
        public Rectangle Hurtbox; // Vùng nhận sát thương
        public int HP = 100;
        public bool IsDead => HP <= 0;

        public Enemy(int x, int y, int width, int height)
        {
            Hurtbox = new Rectangle(x, y, width, height);
        }

        public void Draw(Graphics g)
        {
            if (IsDead) return;
            // Vẽ tạm hình chữ nhật màu đỏ để test
            g.FillRectangle(Brushes.Red, Hurtbox);
            g.DrawString($"HP: {HP}", new Font("Arial", 12), Brushes.White, Hurtbox.X, Hurtbox.Y - 20);
        }
    }
}
