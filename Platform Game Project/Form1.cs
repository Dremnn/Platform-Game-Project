using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Platform_Game_Project
{
    public partial class Form1 : Form
    {
        // --- Fields ---
        private Player player;
        private List<Enemy> enemies;   // Dùng List để dễ thêm/xóa nhiều enemy
        private Rectangle platform;
        private const int GRAVITY = 2;

        private bool left, right, jump, lightAttack, dash;

        public Form1()
        {
            InitializeComponent();
            this.DoubleBuffered = true;
            InitGame();
            gameTimer.Interval = 20;
            gameTimer.Start();
        }

        // --- Khởi tạo riêng, tách khỏi constructor ---
        private void InitGame()
        {
            player = new Player(100, 50, 3);
            platform = new Rectangle(0, 450, 800, 50);
            enemies = new List<Enemy>
            {
                new MeleeSkeleton(500, 400, 2),   // Thêm enemy qua List
                new Slime(550, 400, 3),
            };
        }

        // --- Input ---
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
        }

        // --- Game Loop ---
        private void gameTimer_Tick(object sender, EventArgs e)
        {
            UpdatePlayer();
            UpdateEnemies();
            HandleCombat();
            ResetFrameInput();
            this.Invalidate();
        }

        private void UpdatePlayer()
        {
            // Chỉ cho phép di chuyển khi không đang trong animation tấn công
            int moveDir = 0;
            bool isAttacking = player.CurrentState == PlayerState.LightAttack
                            || player.CurrentState == PlayerState.HeavyAttack
                            || player.CurrentState == PlayerState.DashAttack;

            if (!isAttacking)
            {
                if (left) { moveDir = -1; player.FacingLeft = true; }
                else if (right) { moveDir = 1; player.FacingLeft = false; }
            }

            player.HandleState(left || right, jump, dash, lightAttack, lightAttack);
            player.Update(GRAVITY, moveDir);

            // Va chạm với platform
            if (player.hurtBox.IntersectsWith(platform))
            {
                player.Bounds.Y = platform.Y - player.Bounds.Height;
                player.VelocityY = 0;
                player.IsOnPlatform = true;
            }
            else
            {
                player.IsOnPlatform = false;
            }
        }

        private void UpdateEnemies()
        {
            foreach (var enemy in enemies)
            {
                // Bỏ if (enemy.IsDead) continue
                // Dead state vẫn cần Update() để chạy AnimateOnce()

                if (enemy.CurrentState != EnemyState.Dead)
                    enemy.UpdateAI(player);

                enemy.Update(GRAVITY); // Luôn gọi kể cả khi Dead

                if (enemy.CurrentState != EnemyState.Dead)
                {
                    if (enemy.hurtBox.IntersectsWith(platform))
                    {
                        enemy.Bounds.Y = platform.Y - enemy.Bounds.Height;
                        enemy.VelocityY = 0;
                        enemy.IsOnPlatform = true;
                    }
                    else enemy.IsOnPlatform = false;
                }
            }

            enemies.RemoveAll(e => e.IsDeadAnimationDone);
        }

        private void HandleCombat()
        {
            // --- Player đánh Enemy ---
            if (player.IsHitboxActive)
            {
                foreach (var enemy in enemies)
                {
                    if (enemy.IsDead) continue;
                    if (!player.ActiveHitbox.IntersectsWith(enemy.hurtBox)) continue;
                    if (player.HitEnemiesThisSwing.Contains(enemy)) continue;

                    enemy.TakeDamage(player.CurrentAttackDamage, player.CurrentAttackKnockback, player.FacingLeft);
                    player.HitEnemiesThisSwing.Add(enemy);
                }
            }

            bool isAttacking = player.CurrentState == PlayerState.LightAttack
                            || player.CurrentState == PlayerState.HeavyAttack
                            || player.CurrentState == PlayerState.DashAttack;
            if (!isAttacking) player.HitEnemiesThisSwing.Clear();

            // --- Enemy đánh Player ---
            foreach (var enemy in enemies)
            {
                if (enemy.IsDead || !enemy.IsHitboxActive) continue;
                if (enemy.HasHitPlayer) continue; // Đã đánh rồi thì bỏ qua
                if (!enemy.ActiveHitbox.IntersectsWith(player.hurtBox)) continue;

                player.TakeDamage(10, 15, enemy.FacingLeft);
                enemy.HasHitPlayer = true;
            }

            // Reset khi enemy kết thúc đòn attack
            foreach (var enemy in enemies)
            {
                if (enemy.CurrentState != EnemyState.Attack)
                    enemy.HasHitPlayer = false;
            }
        }

        // Input dạng "vừa nhấn" chỉ sống 1 tick
        private void ResetFrameInput()
        {
            dash = false;
            lightAttack = false;
        }

        // --- Render ---
        private void Form1_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;

            g.FillRectangle(Brushes.Black, platform);

            foreach (var enemy in enemies)
                enemy.Draw(g);

            player.Draw(g);
        }
    }
}