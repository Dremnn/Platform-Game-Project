using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Platform_Game_Project
{
    public enum GameScene { Menu, Playing, GameOver }

    public partial class Form1 : Form
    {
        // --- Fields ---
        private Player player;
        private List<Enemy> enemies;   // Dùng List để dễ thêm/xóa nhiều enemy
        private TiledMap map;
        private const int GRAVITY = 2;
        private GameScene currentScene = GameScene.Menu;

        private bool left, right, jump, lightAttack, dash;

        public Form1()
        {
            InitializeComponent();
            this.DoubleBuffered = true;
            InitGame();

            // Resize window khớp với map: 30*16*3 x 20*16*3
            this.ClientSize = new Size(30 * 16 * 3, 20 * 16 * 3); // 1440 x 960
            this.FormBorderStyle = FormBorderStyle.FixedSingle;    // không resize được
            this.MaximizeBox = false;

            gameTimer.Interval = 20;
            gameTimer.Start();
        }

        // --- Khởi tạo riêng, tách khỏi constructor ---
        private void InitGame()
        {
            string mapPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                          "Assets", "Map", "map3.tmj");
            map = new TiledMap(mapPath, scale: 3);

            // Spawn thấp hơn để đứng trên ground đầu tiên
            // Ground đầu tiên trong map2 ở y≈288 * scale3 = 864
            player = new Player(100, 50, 3);
            enemies = new List<Enemy> { 
                new Slime(800, 50, 3),
                new MeleeSkeleton(1000,50,3) 
            };
        }

        // --- Input ---
        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            switch (currentScene)
            {
                case GameScene.Menu:
                    if (e.KeyCode == Keys.Enter) StartGame();
                    break;

                case GameScene.Playing:
                    if (e.KeyCode == Keys.A) left = true;
                    if (e.KeyCode == Keys.D) right = true;
                    if (e.KeyCode == Keys.Space) jump = true;
                    if (e.KeyCode == Keys.J) lightAttack = true;
                    if (e.KeyCode == Keys.ShiftKey) dash = true;
                    break;

                case GameScene.GameOver:
                    if (e.KeyCode == Keys.Enter) StartGame();   // Retry
                    if (e.KeyCode == Keys.Escape) GoToMenu();   // Menu
                    break;
            }
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
            switch (currentScene)
            {
                case GameScene.Menu:
                    // Không cần update gì, chỉ chờ input
                    break;

                case GameScene.Playing:
                    UpdatePlayer();
                    UpdateEnemies();
                    HandleCombat();
                    ResetFrameInput();
                    CheckGameOver();
                    break;

                case GameScene.GameOver:
                    // Không cần update gì, chỉ chờ input
                    break;
            }
            this.Invalidate();
        }

        private void UpdatePlayer()
        {
            int moveDir = 0;
            bool isAttacking = player.CurrentState == PlayerState.LightAttack
                            || player.CurrentState == PlayerState.HeavyAttack
                            || player.CurrentState == PlayerState.DashAttack;
            bool isBeingAttacked = player.CurrentState == PlayerState.Hurt
                                || player.CurrentState == PlayerState.Dead;

            if (!isAttacking && !isBeingAttacked)
            {
                if (left) { moveDir = -1; player.FacingLeft = true; }
                else if (right) { moveDir = 1; player.FacingLeft = false; }
            }

            player.HandleState(left || right, jump, dash, lightAttack, lightAttack);

            // Physics
            if (player.CurrentState == PlayerState.Dashing)
            {
                player.Bounds.X += (player.FacingLeft ? -1 : 1) * 30;
                player.VelocityY = 0;
            }
            else
            {
                player.VelocityY += GRAVITY;
                player.Bounds.Y += player.VelocityY;
                player.Bounds.X += moveDir * 15;
            }

            // Animation + hitbox
            player.Update(GRAVITY, moveDir);

            // Collision với map
            int offsetX = player.FacingLeft ? 85 : 60;
            int offsetY = 40;
            var b = new Rectangle(
                player.Bounds.X + offsetX,
                player.Bounds.Y + offsetY,
                player.Bounds.Width - 150,
                player.Bounds.Height - 40
            );

            var vel = player.VelocityY;
            bool onGround = map.ResolveCollision(ref b, ref vel);
            player.VelocityY = vel;
            player.IsOnPlatform = onGround;

            // QUAN TRỌNG: đồng bộ Bounds từ hurtBox đã resolve
            player.Bounds.X = b.X - offsetX;
            player.Bounds.Y = b.Y - offsetY;
        }

        private void UpdateEnemies()
        {
            foreach (var enemy in enemies)
            {
                if (enemy.CurrentState != EnemyState.Dead)
                    enemy.UpdateAI(player);

                enemy.Update(GRAVITY);

                if (enemy.CurrentState != EnemyState.Dead)
                {
                    int offsetX = enemy.hurtBox.X - enemy.Bounds.X;
                    int offsetY = enemy.hurtBox.Y - enemy.Bounds.Y;

                    var b = new Rectangle(
                        enemy.Bounds.X + offsetX,
                        enemy.Bounds.Y + offsetY,
                        enemy.hurtBox.Width,
                        enemy.hurtBox.Height
                    );

                    var ev = enemy.VelocityY;
                    bool eg = map.ResolveCollision(ref b, ref ev);
                    enemy.VelocityY = ev;
                    enemy.IsOnPlatform = eg;

                    enemy.Bounds.X = b.X - offsetX;
                    enemy.Bounds.Y = b.Y - offsetY;
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
            switch (currentScene)
            {
                case GameScene.Menu: DrawMenu(e.Graphics); break;
                case GameScene.Playing: DrawGame(e.Graphics); break;
                case GameScene.GameOver: DrawGameOver(e.Graphics); break;
            }
        }

        private void DrawMenu(Graphics g)
        {
            g.Clear(Color.Black);
            g.DrawString("PRESS ENTER TO START",
                new Font("Arial", 20), Brushes.White, 300, 200);
        }

        private void DrawGame(Graphics g)
        {
            g.Clear(Color.Black);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;

            map.DrawMap(g);
            map.DrawDebug(g); // bỏ comment để debug collision
            foreach (var enemy in enemies) enemy.Draw(g);
            player.Draw(g);

            g.DrawString($"State: {player.CurrentState} | Frame: {player.currentFrame}",
                new Font("Arial", 10), Brushes.White, 10, 10);
        }

        private void DrawGameOver(Graphics g)
        {
            g.Clear(Color.Black);
            g.DrawString("GAME OVER",
                new Font("Arial", 30), Brushes.Red, 300, 150);
            g.DrawString("ENTER - Retry    ESC - Menu",
                new Font("Arial", 16), Brushes.White, 250, 250);
        }

        private void StartGame()
        {
            InitGame(); // Reset toàn bộ
            currentScene = GameScene.Playing;
        }

        private void GoToMenu()
        {
            currentScene = GameScene.Menu;
        }

        // Gọi khi player chết
        private void CheckGameOver()
        {
            if (player.CurrentState == PlayerState.Dead && player.IsDeadAnimationDone)
                currentScene = GameScene.GameOver;
        }
    }
}