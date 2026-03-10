using System;
using System.Numerics;

namespace Platform_Game_Project
{
    public partial class Form1 : Form
    {
        Player player;
        Enemy enemy;
        Rectangle platform;
        int gravity = 2;

        // Biến điều khiển
        bool left, right, jump, lightAttack, dash;

        public Form1()
        {
            InitializeComponent();
            this.DoubleBuffered = true;

            // Khởi tạo
            player = new Player(100, 50, 5);
            enemy = new Enemy(50, 400, 50, 50);
            platform = new Rectangle(0, 450, 800, 50);

            // Timer
            gameTimer.Interval = 20;
            gameTimer.Start();

        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.A) left = true;
            if (e.KeyCode == Keys.D) right = true;
            if (e.KeyCode == Keys.Space) jump = true;
            if (e.KeyCode == Keys.J) lightAttack = true;
            if (e.KeyCode == Keys.ShiftKey) dash = true;
        }

        private void Form1_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.A) left = false;
            if (e.KeyCode == Keys.D) right = false;
            if (e.KeyCode == Keys.Space) jump = false;
            //if (e.KeyCode == Keys.J) lightAttack = false;
        }

        private void gameTimer_Tick(object sender, EventArgs e)
        {
            // 1. Quyết định trạng thái
            //player.HandleState(left || right, false);

            // 2. Cập nhật vị trí
            int moveDir = 0;
            if (player.CurrentState != PlayerState.LightAttack && player.CurrentState != PlayerState.HeavyAttack && player.CurrentState != PlayerState.DashAttack)
            {
                if (left) { moveDir = -1; player.FacingLeft = true; }
                else if (right) { moveDir = 1; player.FacingLeft = false; }
            }

            // Truyền (left || right) chính là biến isMoving bạn đang tìm
            player.HandleState(left || right, jump, dash, lightAttack, lightAttack);

            dash = false;
            lightAttack = false; // Reset sau khi đã xử lý

            player.Update(gravity, moveDir);

            // 3. Va chạm
            if (player.hurtBox.IntersectsWith(platform))
            {
                player.Bounds.Y = platform.Y - player.Bounds.Height;
                player.VelocityY = 0;
                player.IsOnPlatform = true;
            }
            else player.IsOnPlatform = false;

            // 4. Xử lý va chạm sát thương
            if (player.IsHitboxActive && !player.HasHitEnemy)
            {
                if (player.ActiveHitbox.IntersectsWith(enemy.Hurtbox) && !enemy.IsDead)
                {
                    int damage = (player.CurrentState == PlayerState.HeavyAttack) ? 30 : 10;
                    enemy.HP -= damage;
                    player.HasHitEnemy = true; // Khóa lại, không cho gây thêm damage trong lần vung này

                    // Thêm hiệu ứng rung màn hình hoặc đẩy lùi (Knockback) ở đây
                    enemy.Hurtbox.X += player.FacingLeft ? -20 : 20;
                }
            }

            // Reset hasHitEnemy khi player kết thúc đòn đánh hoặc chuyển sang đòn mới
            if (player.CurrentState != PlayerState.LightAttack && player.CurrentState != PlayerState.HeavyAttack)
            {
                player.HasHitEnemy = false;
            }

            this.Invalidate();
        }

        private void Form1_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            // Vẽ sàn tạm bằng màu đen để test trước khi dùng ảnh
            e.Graphics.FillRectangle(Brushes.Black, platform);
            enemy.Draw(e.Graphics);
            player.Draw(e.Graphics);
        }
    }
}
