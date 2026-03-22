using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;

namespace Platform_Game_Project
{
    public enum GameScene { Menu, Playing, GameOver, GameClear }

    public partial class Form1 : Form
    {
        // ── Fields ──
        private Player player;
        private List<Enemy> enemies;
        private TiledMap map;
        private const int GRAVITY = 2;
        private GameScene currentScene = GameScene.Menu;

        // Coyote time
        private int coyoteTimer = 0;

        // Input giữ liên tục
        private bool left, right, jump, climbUp, climbDown;
        // Input "vừa nhấn" (reset mỗi tick)
        private bool lightAttack, dash, interactE, dropDown;

        // ── Spike: trừ 1/4 MaxHP + bất tử 1 giây mỗi lần chạm ──
        private bool spikeHitThisContact = false;
        private const int SPIKE_INVINCIBLE_TICKS = 50; // 50 × 20ms = 1 giây

        // ── One-Way drop-through ──
        private int dropThroughTimer = 0;
        private const int DROP_THROUGH_TICKS = 15;

        // Map
        private List<string> mapPool = new List<string>
        {
            "map10.tmj",
            "map2.tmj",
            "map3.tmj",
            "map4.tmj",
            "map5.tmj",
            "map6.tmj",
            "map7.tmj",
            "map8.tmj",
            "map9.tmj",
        };

        // SFX
        private SoundManager sfx;

        private Random rng = new Random();
        private string lastMap = "";

        // UI
        private UIManager ui;
        private int soul = 0;
        private const int SOUL_REQUIRED = 100;
        private int mapCount = 0;
        private List<BuffEntry> activeBuffs = new List<BuffEntry>();

        // Buff
        private bool showBuffPopup = false;
        private List<BuffEntry> buffChoices = new List<BuffEntry>();
        private const int BUFF_DROP_CHANCE = 100;

        public Form1()
        {
            InitializeComponent();
            this.DoubleBuffered = true;
            InitGame();

            this.ClientSize = new Size(30 * 16 * 3, 20 * 16 * 3);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            gameTimer.Interval = 20;
            gameTimer.Start();
        }

        private void InitGame()
        {
            soul = 0;
            mapCount = 0;
            activeBuffs = new List<BuffEntry>();
            spikeHitThisContact = false;
            dropThroughTimer = 0;
            coyoteTimer = 0;

            player = new Player(100, 50, 3);
            LoadRandomMap();
            SpawnEnemiesForMap(lastMap);
            ui = new UIManager(this.ClientSize.Width);
            sfx = new SoundManager();
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
                    if (e.KeyCode == Keys.G) enemies.Add(new Slime(100, 50, 3));
                    if (showBuffPopup)
                    {
                        if (e.KeyCode == Keys.D1 && buffChoices.Count > 0) ApplyBuff(buffChoices[0]);
                        if (e.KeyCode == Keys.D2 && buffChoices.Count > 1) ApplyBuff(buffChoices[1]);
                        if (e.KeyCode == Keys.D3 && buffChoices.Count > 2) ApplyBuff(buffChoices[2]);
                        return;
                    }
                    if (e.KeyCode == Keys.T) soul += 10;
                    if (e.KeyCode == Keys.B && soul >= SOUL_REQUIRED) enemies.Add(new Boss(100, 50, 3));
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
            switch (currentScene)           // ← FIX: switch thay vì if
            {
                case GameScene.Menu:
                    break;

                case GameScene.Playing:
                    if (!showBuffPopup)
                    {
                        UpdatePlayer();
                        UpdateEnemies();
                        HandleCombat();
                        HandleSpikeDamage();
                        ResetFrameInput();
                        CheckGameOver();
                        CheckDoor();
                    }
                    break;

                case GameScene.GameOver:
                    break;
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

            // ─── CHẾ ĐỘ LEO THANG ───          // ← FIX: thiếu hoàn toàn trong bản cũ
            if (player.IsClimbing)
            {
                int climbSpeed = 4;
                player.Bounds.X += moveDir * 8;
                if (climbUp) player.Bounds.Y -= climbSpeed;
                if (climbDown) player.Bounds.Y += climbSpeed;

                player.VelocityY = 0;
                player.IsOnPlatform = false;

                if (jump)
                {
                    player.IsClimbing = false;
                    player.VelocityY = -22;
                }

                player.ForceState(PlayerState.Climbing);
                player.Update(0, moveDir);

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

            // ─── CHẾ ĐỘ BÌNH THƯỜNG ───
            player.HandleState(left || right, jump, dash, lightAttack, lightAttack); // ← FIX: gọi HandleState trước physics

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

            // Coyote time
            if (onGround)
            {
                player.IsOnPlatform = true;
                coyoteTimer = 5;
            }
            else if (coyoteTimer > 0)
            {
                coyoteTimer--;
                player.IsOnPlatform = true;
            }
            else
            {
                player.IsOnPlatform = false;
            }

            player.Bounds.X = b.X - offsetX;
            player.Bounds.Y = b.Y - offsetY;

            HandleStairStep();
        }

        // ════════════════════════════════════════
        //  STAIR
        // ════════════════════════════════════════
        private void HandleStairStep()
        {
            if (player.CurrentState == PlayerState.Climbing) return;

            int moveDir = left ? -1 : right ? 1 : 0;
            if (moveDir == 0 && player.IsOnPlatform) return;

            int offX = player.FacingLeft ? 85 : 60;
            int offY = 40;
            var hb = new Rectangle(
                player.Bounds.X + offX,
                player.Bounds.Y + offY,
                player.Bounds.Width - 150,
                player.Bounds.Height - 40);

            float queryX = moveDir >= 0
                ? hb.X + hb.Width * 0.75f
                : hb.X + hb.Width * 0.25f;
            if (moveDir == 0) queryX = hb.X + hb.Width * 0.5f;

            int snapThreshold = hb.Height;

            foreach (var stair in map.Stairs)
            {
                var expandedBounds = new Rectangle(
                    stair.Bounds.X - 16 * 3, stair.Bounds.Y - 16 * 3,
                    stair.Bounds.Width + 32 * 3, stair.Bounds.Height + 32 * 3);

                if (!hb.IntersectsWith(expandedBounds)) continue;

                float surfaceY = stair.GetSurfaceYAt(queryX);
                if (surfaceY == float.MaxValue) continue;

                float distToSnap = hb.Bottom - surfaceY;
                if (distToSnap > 0 && distToSnap <= snapThreshold)
                {
                    player.Bounds.Y -= (int)MathF.Ceiling(distToSnap);
                    player.VelocityY = 0;
                    player.IsOnPlatform = true;
                    break;
                }
            }
        }

        // ════════════════════════════════════════
        //  LADDER
        // ════════════════════════════════════════
        private void HandleLadder()
        {
            bool insideLadder = false;
            foreach (var ladder in map.Ladders)
                if (player.hurtBox.IntersectsWith(ladder)) { insideLadder = true; break; }

            player.IsOnLadder = insideLadder;
            if (!insideLadder) player.IsClimbing = false;
            if (interactE && insideLadder) player.IsClimbing = !player.IsClimbing;
        }

        // ════════════════════════════════════════
        //  ONE-WAY DROP
        // ════════════════════════════════════════
        private void HandleOneWayDropLogic()
        {
            if (dropThroughTimer > 0) { dropThroughTimer--; return; }
            if (!dropDown || !player.IsOnPlatform) return;

            int offX = player.FacingLeft ? 85 : 60;
            int offY = 40;
            var hb = new Rectangle(
                player.Bounds.X + offX, player.Bounds.Y + offY,
                player.Bounds.Width - 150, player.Bounds.Height - 40);

            foreach (var col in map.Colliders)
            {
                if (!col.IsOneWay) continue;
                if (Math.Abs(hb.Bottom - col.Bounds.Top) <= 6 &&
                    hb.Right > col.Bounds.Left && hb.Left < col.Bounds.Right)
                {
                    dropThroughTimer = DROP_THROUGH_TICKS;
                    player.IsOnPlatform = false;
                    player.VelocityY = 3;
                    break;
                }
            }
        }

        // ════════════════════════════════════════
        //  SPIKE — trừ 1/4 MaxHP + bất tử 1 giây
        // ════════════════════════════════════════
        private void HandleSpikeDamage()        // ← FIX: cơ chế cũ → mới
        {
            bool inSpike = false;
            foreach (var spike in map.Spikes)
                if (player.hurtBox.IntersectsWith(spike)) { inSpike = true; break; }

            if (inSpike)
            {
                if (!spikeHitThisContact && !player.IsInvincible)
                {
                    int damage = player.MaxHP / 4;
                    player.TakeDamage(damage, knockback: 0, enemyFacingLeft: false);
                    player.SetInvincible(SPIKE_INVINCIBLE_TICKS);
                    spikeHitThisContact = true;
                }
            }
            else
            {
                spikeHitThisContact = false;
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
                    sfx.Play("player_hit");

                    if (enemy.IsDead)
                    {
                        soul += 10;
                        TryDropBuff();
                    }
                }
            }

            bool isAttacking = player.CurrentState == PlayerState.LightAttack
                            || player.CurrentState == PlayerState.HeavyAttack
                            || player.CurrentState == PlayerState.DashAttack;
            if (!isAttacking) player.HitEnemiesThisSwing.Clear();

            foreach (var enemy in enemies)
            {
                if (enemy.IsDead || !enemy.IsHitboxActive || enemy.HasHitPlayer) continue;
                if (!enemy.ActiveHitbox.IntersectsWith(player.hurtBox)) continue;

                player.TakeDamage(10, 15, enemy.FacingLeft);
                enemy.HasHitPlayer = true;
                sfx.Play("player_hurt");
            }

            // ← FIX: reset HasHitPlayer khi enemy kết thúc đòn
            foreach (var enemy in enemies)
                if (enemy.CurrentState != EnemyState.Attack) enemy.HasHitPlayer = false;
        }

        // ════════════════════════════════════════
        //  BUFF
        // ════════════════════════════════════════
        private void TryDropBuff()
        {
            if (rng.Next(100) >= BUFF_DROP_CHANCE) return;

            var pool = new List<BuffEntry>
            {
                new BuffEntry { Label = "DMG", Value = rng.Next(3,  8)  },
                new BuffEntry { Label = "HP",  Value = rng.Next(15, 30) },
                new BuffEntry { Label = "KB",  Value = rng.Next(2,  6)  },
            };

            buffChoices = pool.OrderBy(_ => rng.Next()).Take(3).ToList();
            showBuffPopup = true;
        }

        private void ApplyBuff(BuffEntry buff)
        {
            switch (buff.Label)
            {
                case "DMG": player.BonusDamage += buff.Value; break;
                case "HP":
                    player.MaxHP += buff.Value;
                    player.HP += buff.Value; break;
                case "KB": player.BonusKnockback += buff.Value; break;
            }
            activeBuffs.Add(buff);
            showBuffPopup = false;
        }

        // ════════════════════════════════════════
        //  MAP
        // ════════════════════════════════════════
        private void LoadRandomMap()
        {
            var available = mapPool.Where(m => m != lastMap).ToList();
            lastMap = available[rng.Next(available.Count)];

            string mapPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Assets", "Map", lastMap);
            map = new TiledMap(mapPath, scale: 3);
        }

        private void SpawnEnemiesForMap(string mapName)
        {
            enemies = new List<Enemy>();
            switch (mapName)
            {
                case "map1.tmj":
                    player.Bounds = new Rectangle(100, 50, player.Bounds.Width, player.Bounds.Height);
                    enemies.Add(new Slime(800, 50, 3));
                    break;
                case "map2.tmj":
                    player.Bounds = new Rectangle(150, 50, player.Bounds.Width, player.Bounds.Height);
                    enemies.Add(new MeleeSkeleton(800, 50, 2));
                    break;
                case "map3.tmj":
                    player.Bounds = new Rectangle(100, 50, player.Bounds.Width, player.Bounds.Height);
                    enemies.Add(new MeleeSkeleton(700, 50, 2));
                    enemies.Add(new Slime(1200, 50, 3));
                    break;
            }
            player.VelocityY = 0;
        }

        private void GoToNextMap()
        {
            mapCount++;
            spikeHitThisContact = false;
            LoadRandomMap();
            SpawnEnemiesForMap(lastMap);
        }

        private void CheckDoor()
        {
            if (map.Door.HasValue && player.hurtBox.IntersectsWith(map.Door.Value))
                GoToNextMap();
        }

        // ════════════════════════════════════════
        //  RESET FRAME INPUT
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

            ui.Draw(g, player, soul, SOUL_REQUIRED, mapCount, activeBuffs);
            if (showBuffPopup)
                ui.DrawBuffPopup(g, buffChoices);
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