using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Platform_Game_Project
{
    public enum GameScene { Menu, Playing, GameOver }

    public partial class Form1 : Form
    {
        // ── Fields ──
        private Player player;
        private List<Enemy> enemies;
        private TiledMap map;
        private const int GRAVITY = 2;
        private GameScene currentScene = GameScene.Menu;

        // Input giữ liên tục
        private bool left, right, jump, climbUp, climbDown;
        // Input "vừa nhấn" (reset mỗi tick)
        private bool lightAttack, dash, interactE, dropDown;

        // ── Spike: tích lũy damage theo thời gian ──
        private float spikeDamageAccum = 0f;
        private const float SPIKE_DPS = 40f;    // 40 HP/giây
        private const float TICK_SECONDS = 0.04f;  // 40ms / tick

        // ── One-Way drop-through: số tick bỏ qua one-way collision ──
        private int dropThroughTimer = 0;
        private const int DROP_THROUGH_TICKS = 15;

        public Form1()
        {
            InitializeComponent();
            this.DoubleBuffered = true;
            InitGame();

            this.ClientSize = new Size(30 * 16 * 3, 20 * 16 * 3); // 1440×960
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            gameTimer.Interval = 20;
            gameTimer.Start();
        }

        private void InitGame()
        {
            string mapPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                          "Assets", "Map", "map8.tmj");
            map = new TiledMap(mapPath, scale: 3);
            player = new Player(100, 50, 3);
            enemies = new List<Enemy>
            {
                new Slime(800, 50, 3),
                new MeleeSkeleton(1000, 50, 3)
            };

            spikeDamageAccum = 0f;
            dropThroughTimer = 0;
        }

        // ════════════════════════════════════════
        //  INPUT
        // ════════════════════════════════════════
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
                    if (e.KeyCode == Keys.W) climbUp = true;
                    if (e.KeyCode == Keys.S) { climbDown = true; dropDown = true; }
                    if (e.KeyCode == Keys.Space) jump = true;
                    if (e.KeyCode == Keys.J) lightAttack = true;
                    if (e.KeyCode == Keys.ShiftKey) dash = true;
                    if (e.KeyCode == Keys.E) interactE = true;
                    break;

                case GameScene.GameOver:
                    if (e.KeyCode == Keys.Enter) StartGame();
                    if (e.KeyCode == Keys.Escape) GoToMenu();
                    break;
            }
        }

        private void Form1_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.A) left = false;
            if (e.KeyCode == Keys.D) right = false;
            if (e.KeyCode == Keys.W) climbUp = false;
            if (e.KeyCode == Keys.S) climbDown = false;
            if (e.KeyCode == Keys.Space) jump = false;
        }

        // ════════════════════════════════════════
        //  GAME LOOP
        // ════════════════════════════════════════
        private void gameTimer_Tick(object sender, EventArgs e)
        {
            if (currentScene == GameScene.Playing)
            {
                UpdatePlayer();
                UpdateEnemies();
                HandleCombat();
                HandleSpikeDamage();
                ResetFrameInput();
                CheckGameOver();
            }
            this.Invalidate();
        }

        // ════════════════════════════════════════
        //  UPDATE PLAYER
        // ════════════════════════════════════════
        private void UpdatePlayer()
        {
            HandleLadder();
            HandleOneWayDropLogic();

            bool isAttacking = player.CurrentState == PlayerState.LightAttack
                                || player.CurrentState == PlayerState.HeavyAttack
                                || player.CurrentState == PlayerState.DashAttack;
            bool isBeingHurt = player.CurrentState == PlayerState.Hurt
                                || player.CurrentState == PlayerState.Dead;

            int moveDir = 0;
            if (!isAttacking && !isBeingHurt)
            {
                if (left) { moveDir = -1; player.FacingLeft = true; }
                else if (right) { moveDir = 1; player.FacingLeft = false; }
            }

            // ────────────────────────────────────
            //  CHẾ ĐỘ LEO THANG (Ladder)
            // ────────────────────────────────────
            if (player.IsClimbing)
            {
                int climbSpeed = 4;

                player.Bounds.X += moveDir * 8;
                if (climbUp) player.Bounds.Y -= climbSpeed;
                if (climbDown) player.Bounds.Y += climbSpeed;

                player.VelocityY = 0;
                player.IsOnPlatform = false;

                // Nhảy thoát khỏi thang
                if (jump)
                {
                    player.IsClimbing = false;
                    player.VelocityY = -22;
                }

                player.ForceState(PlayerState.Climbing);
                player.Update(0, moveDir);

                // Chỉ resolve ngang, không block vertical khi leo
                int offX = player.FacingLeft ? 85 : 60;
                int offY = 40;
                var hbClimb = new Rectangle(
                    player.Bounds.X + offX, player.Bounds.Y + offY,
                    player.Bounds.Width - 150, player.Bounds.Height - 40);

                map.ResolveHorizontalOnly(ref hbClimb);
                player.Bounds.X = hbClimb.X - offX;
                player.Bounds.Y = hbClimb.Y - offY;
                player.UpdateHurtbox();
                return;
            }

            // ────────────────────────────────────
            //  CHẾ ĐỘ BÌNH THƯỜNG
            // ────────────────────────────────────
            player.HandleState(left || right, jump, dash, lightAttack, lightAttack);

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

            player.Update(GRAVITY, moveDir);

            // Hurtbox để resolve collision
            int offsetX = player.FacingLeft ? 85 : 60;
            int offsetY = 40;
            var b = new Rectangle(
                player.Bounds.X + offsetX,
                player.Bounds.Y + offsetY,
                player.Bounds.Width - 150,
                player.Bounds.Height - 40);

            var vel = player.VelocityY;
            bool onGround = map.ResolveCollision(ref b, ref vel,
                                                  ignoreOneWay: dropThroughTimer > 0);
            player.VelocityY = vel;
            player.IsOnPlatform = onGround;

            player.Bounds.X = b.X - offsetX;
            player.Bounds.Y = b.Y - offsetY;

            // Stair: snap lên bậc thang sau khi resolve ground
            HandleStairStep();
        }

        // ════════════════════════════════════════
        //  STAIR — snap lên bậc thang
        // ════════════════════════════════════════
        //
        //  Cầu thang bậc thang (step pattern):
        //
        //    ┌──┐
        //    │  ├──┐          ← mỗi bậc là 1 tile cao
        //    │  │  ├──┐
        //    │  │  │  │
        //
        //  Polygon trong Tiled có các cạnh ngang (tread) xen kẽ cạnh dọc (riser).
        //  GetSurfaceYAt() bỏ qua cạnh dọc, chỉ lấy Y của cạnh ngang tại X của player.
        //  Nếu mặt bậc nằm trong nửa dưới cơ thể → snap player lên đúng vị trí bậc đó.
        //
        private void HandleStairStep()
        {
            if (player.CurrentState == PlayerState.Climbing) return;

            int moveDir = left ? -1 : right ? 1 : 0;
            if (moveDir == 0 && player.IsOnPlatform) return; // đứng yên trên ground thường

            int offX = player.FacingLeft ? 85 : 60;
            int offY = 40;
            var hb = new Rectangle(
                player.Bounds.X + offX,
                player.Bounds.Y + offY,
                player.Bounds.Width - 150,
                player.Bounds.Height - 40);

            // Dùng mép trước của player (phía đang đi) để query bậc chính xác hơn
            float queryX = moveDir >= 0
                ? hb.X + hb.Width * 0.75f   // đi phải → lấy mép phải
                : hb.X + hb.Width * 0.25f;  // đi trái  → lấy mép trái

            // Nếu đứng yên (moveDir == 0 nhưng chưa on platform) dùng giữa
            if (moveDir == 0) queryX = hb.X + hb.Width * 0.5f;

            int snapThreshold = hb.Height; // snap tối đa bằng chiều cao hurtbox (1 body)

            foreach (var stair in map.Stairs)
            {
                // Check AABB nhanh — mở rộng thêm 1 tile để bắt trường hợp đang tiếp cận
                var expandedBounds = new Rectangle(
                    stair.Bounds.X - 16 * 3,
                    stair.Bounds.Y - 16 * 3,
                    stair.Bounds.Width + 32 * 3,
                    stair.Bounds.Height + 32 * 3);

                if (!hb.IntersectsWith(expandedBounds)) continue;

                float surfaceY = stair.GetSurfaceYAt(queryX);
                if (surfaceY == float.MaxValue) continue;

                // Khoảng cách từ đáy hurtbox đến mặt bậc
                // (+) = bậc nằm trên đáy player (player cần được nâng lên)
                // (-) = bậc nằm dưới đáy player (player đang lơ lửng trên không)
                float distToSnap = hb.Bottom - surfaceY;

                if (distToSnap > 0 && distToSnap <= snapThreshold)
                {
                    // Snap player lên mặt bậc mượt mà
                    player.Bounds.Y -= (int)MathF.Ceiling(distToSnap);
                    player.VelocityY = 0;
                    player.IsOnPlatform = true;
                    break;
                }
            }
        }

        // ════════════════════════════════════════
        //  LADDER — nhấn E để bắt đầu/dừng leo
        // ════════════════════════════════════════
        private void HandleLadder()
        {
            bool insideLadder = false;
            foreach (var ladder in map.Ladders)
            {
                if (player.hurtBox.IntersectsWith(ladder)) { insideLadder = true; break; }
            }

            player.IsOnLadder = insideLadder;

            // Rời khỏi vùng thang → dừng leo tự động
            if (!insideLadder)
                player.IsClimbing = false;

            // Nhấn E khi đang trong vùng thang → toggle leo
            if (interactE && insideLadder)
                player.IsClimbing = !player.IsClimbing;
        }

        // ════════════════════════════════════════
        //  ONE-WAY
        //  S  → drop xuống xuyên qua one-way
        //  Space → nhảy lên xuyên qua (tự động
        //          vì ResolveCollision chỉ block từ trên xuống)
        // ════════════════════════════════════════
        private void HandleOneWayDropLogic()
        {
            if (dropThroughTimer > 0) { dropThroughTimer--; return; }

            // Nhấn S + đang đứng trên one-way → drop through
            if (!dropDown || !player.IsOnPlatform) return;

            int offX = player.FacingLeft ? 85 : 60;
            int offY = 40;
            var hb = new Rectangle(
                player.Bounds.X + offX,
                player.Bounds.Y + offY,
                player.Bounds.Width - 150,
                player.Bounds.Height - 40);

            foreach (var col in map.Colliders)
            {
                if (!col.IsOneWay) continue;
                // Đáy player nằm sát đỉnh platform (±6px)
                if (Math.Abs(hb.Bottom - col.Bounds.Top) <= 6 &&
                    hb.Right > col.Bounds.Left && hb.Left < col.Bounds.Right)
                {
                    dropThroughTimer = DROP_THROUGH_TICKS;
                    player.IsOnPlatform = false;
                    player.VelocityY = 3; // nudge nhỏ vượt qua threshold
                    break;
                }
            }
        }

        // ════════════════════════════════════════
        //  SPIKE — trừ 10 HP/giây
        // ════════════════════════════════════════
        private void HandleSpikeDamage()
        {
            bool inSpike = false;
            foreach (var spike in map.Spikes)
            {
                if (player.hurtBox.IntersectsWith(spike)) { inSpike = true; break; }
            }

            if (inSpike)
            {
                spikeDamageAccum += SPIKE_DPS * TICK_SECONDS;
                if (spikeDamageAccum >= 1f)
                {
                    int dmg = (int)spikeDamageAccum;
                    spikeDamageAccum -= dmg;
                    player.TakeDamage(dmg, knockback: 0, enemyFacingLeft: false);
                }
            }
            else
            {
                spikeDamageAccum = 0f;
            }
        }

        // ════════════════════════════════════════
        //  UPDATE ENEMIES
        // ════════════════════════════════════════
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
                        enemy.Bounds.X + offsetX, enemy.Bounds.Y + offsetY,
                        enemy.hurtBox.Width, enemy.hurtBox.Height);

                    var ev = enemy.VelocityY;
                    bool eg = map.ResolveCollision(ref b, ref ev, ignoreOneWay: false);
                    enemy.VelocityY = ev;
                    enemy.IsOnPlatform = eg;

                    enemy.Bounds.X = b.X - offsetX;
                    enemy.Bounds.Y = b.Y - offsetY;
                }
            }

            enemies.RemoveAll(e => e.IsDeadAnimationDone);
        }

        // ════════════════════════════════════════
        //  COMBAT
        // ════════════════════════════════════════
        private void HandleCombat()
        {
            // Player đánh enemy
            if (player.IsHitboxActive)
            {
                foreach (var enemy in enemies)
                {
                    if (enemy.IsDead) continue;
                    if (!player.ActiveHitbox.IntersectsWith(enemy.hurtBox)) continue;
                    if (player.HitEnemiesThisSwing.Contains(enemy)) continue;

                    enemy.TakeDamage(player.CurrentAttackDamage,
                                     player.CurrentAttackKnockback,
                                     player.FacingLeft);
                    player.HitEnemiesThisSwing.Add(enemy);
                }
            }

            bool isAttacking = player.CurrentState == PlayerState.LightAttack
                            || player.CurrentState == PlayerState.HeavyAttack
                            || player.CurrentState == PlayerState.DashAttack;
            if (!isAttacking) player.HitEnemiesThisSwing.Clear();

            // Enemy đánh player
            foreach (var enemy in enemies)
            {
                if (enemy.IsDead || !enemy.IsHitboxActive || enemy.HasHitPlayer) continue;
                if (!enemy.ActiveHitbox.IntersectsWith(player.hurtBox)) continue;

                player.TakeDamage(10, 15, enemy.FacingLeft);
                enemy.HasHitPlayer = true;
            }

            foreach (var enemy in enemies)
                if (enemy.CurrentState != EnemyState.Attack) enemy.HasHitPlayer = false;
        }

        // ════════════════════════════════════════
        //  RESET FRAME INPUT (chỉ sống 1 tick)
        // ════════════════════════════════════════
        private void ResetFrameInput()
        {
            dash = false;
            lightAttack = false;
            interactE = false;
            dropDown = false;
        }

        // ════════════════════════════════════════
        //  RENDER
        // ════════════════════════════════════════
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
            map.DrawDebug(g);
            foreach (var enemy in enemies) enemy.Draw(g);
            player.Draw(g);

            // ── HUD debug ──
            var font = new Font("Arial", 10);
            g.DrawString(
                $"State: {player.CurrentState}  Frame: {player.currentFrame}  " +
                $"Climbing: {player.IsClimbing}  DropTimer: {dropThroughTimer}",
                font, Brushes.White, 10, 10);

            if (spikeDamageAccum > 0f)
                g.DrawString("⚠ SPIKE!", new Font("Arial", 12, FontStyle.Bold),
                             Brushes.OrangeRed, 10, 30);
        }

        private void DrawGameOver(Graphics g)
        {
            g.Clear(Color.Black);
            g.DrawString("GAME OVER",
                new Font("Arial", 30), Brushes.Red, 300, 150);
            g.DrawString("ENTER - Retry    ESC - Menu",
                new Font("Arial", 16), Brushes.White, 250, 250);
        }

        // ════════════════════════════════════════
        //  SCENE MANAGEMENT
        // ════════════════════════════════════════
        private void StartGame() { InitGame(); currentScene = GameScene.Playing; }
        private void GoToMenu() { currentScene = GameScene.Menu; }

        private void CheckGameOver()
        {
            if (player.CurrentState == PlayerState.Dead && player.IsDeadAnimationDone)
                currentScene = GameScene.GameOver;
        }
    }
}