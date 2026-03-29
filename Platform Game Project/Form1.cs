using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;

namespace Platform_Game_Project
{
    public enum GameScene { Menu, Playing, GameOver, GameClear, Tutorial }

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
        private bool isBossMap = false;
        private bool bossSummoned = false;
        private bool bossCleared = false;
        private List<string> mapPool = new List<string>
        {
            "map6.tmj",
            "map2.tmj",
            "map1.tmj",
            "map3.tmj",
            "map4.tmj",
            "map5.tmj",
            "map7.tmj",
            "map8.tmj",
            "map9.tmj"
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
        private int finalSoul = 0;
        private int finalMapCount = 0;

        // Buff
        private bool showBuffPopup = false;
        private List<BuffEntry> buffChoices = new List<BuffEntry>();
        private const int BUFF_DROP_CHANCE = 50;

        public Form1()
        {
            InitializeComponent();
            this.ClientSize = new Size(30 * 16 * 3, 20 * 16 * 3);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            InitGame();                     
            this.DoubleBuffered = true;
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
            bossSummoned = false;
            bossCleared = false;
            isBossMap = false;

            player = new Player(100, 50, 3);
            LoadRandomMap();
            SpawnEnemiesForMap(lastMap);
            ui = new UIManager(this.ClientSize.Width, this.ClientSize.Height);
            sfx = new SoundManager();
        }


        //  INPUT

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            switch (currentScene)
            {
                case GameScene.Menu:
                    if (e.KeyCode == Keys.Enter) StartGame();
                    if (e.KeyCode == Keys.T) currentScene = GameScene.Tutorial;
                    break;

                case GameScene.Tutorial:
                    if (e.KeyCode == Keys.Escape) GoToMenu();
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
                    if (e.KeyCode == Keys.B && soul >= SOUL_REQUIRED && !bossSummoned) goToBossMap();
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

                case GameScene.GameClear:
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


        //  GAME LOOP

        private void gameTimer_Tick(object sender, EventArgs e)
        {
            switch (currentScene)           
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
                        CheckBossClear();
                    }
                    break;

                case GameScene.GameOver:
                    break;
            }
            this.Invalidate();
        }


        //  UPDATE PLAYER

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

            // ─── CHẾ ĐỘ LEO THANG ───        
            if (player.IsClimbing)
            {
                // Căn giữa player theo thang
                foreach (var ladder in map.Ladders)
                {
                    if (player.hurtBox.IntersectsWith(ladder))
                    {
                        int ladderCenterX = ladder.X + ladder.Width / 2;
                        int playerCenterX = player.hurtBox.X + player.hurtBox.Width / 2;
                        player.Bounds.X += ladderCenterX - playerCenterX; // Kéo player vào giữa
                        break;
                    }
                }

                int climbSpeed = 4;
                if (climbUp) player.Bounds.Y -= climbSpeed;
                if (climbDown) player.Bounds.Y += climbSpeed;

                player.VelocityY = 0;
                player.IsOnPlatform = false;
                player.TransitionTo(PlayerState.Climbing);
                player.Update(0, 0);

                int offX = player.FacingLeft ? 85 : 60;
                int offY = 40;
                var climbB = new Rectangle(         
                    player.Bounds.X + offX,
                    player.Bounds.Y + offY,
                    player.Bounds.Width - 150,
                    player.Bounds.Height - 40);

                var climbVel = player.VelocityY;  
                bool onGround1 = map.ResolveCollision(ref climbB, ref climbVel, ignoreOneWay: false);
                player.VelocityY = climbVel;

                if (onGround1 && climbDown)
                {
                    player.IsClimbing = false;
                    player.IsOnPlatform = true;
                    player.TransitionTo(PlayerState.Idle);
                }

                player.Bounds.X = climbB.X - offX;
                player.Bounds.Y = climbB.Y - offY;
                player.UpdateHurtbox();
                return;
            }

            // ─── CHẾ ĐỘ BÌNH THƯỜNG ───

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
            bool onGround = map.ResolveCollision(ref b, ref vel, ignoreOneWay: dropThroughTimer > 0);
            player.VelocityY = vel;

            if (onGround)
            {
                player.IsOnPlatform = true;
                coyoteTimer = 5;
                if (player.VelocityY > 0) player.VelocityY = 0;
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

            player.HandleState(left || right, jump, dash, lightAttack, lightAttack);

            HandleStairStep();
            if (player.Bounds.Y > this.ClientSize.Height + 300
                && player.CurrentState != PlayerState.Dead)
            {
                player.HP = 0;
                player.TransitionTo(PlayerState.Dead);
            }
        }


        //  STAIR

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
        private void HandleEnemyStairStep(Enemy enemy)
        {
            var hb = enemy.hurtBox;
            // Query ở giữa hurtbox theo hướng di chuyển
            int moveDir = enemy.Bounds.X < (hb.X + hb.Width / 2) ? 0 : 0; // default center
            float queryX = hb.X + hb.Width * 0.5f;

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
                    int offsetY = enemy.hurtBox.Y - enemy.Bounds.Y;
                    enemy.Bounds.Y -= (int)MathF.Ceiling(distToSnap);
                    enemy.VelocityY = 0;
                    enemy.IsOnPlatform = true;
                    break;
                }
            }
        }


        //  LADDER

        private void HandleLadder()
        {
            bool insideLadder = false;
            foreach (var ladder in map.Ladders)
                if (player.hurtBox.IntersectsWith(ladder)) { insideLadder = true; break; }

            player.IsOnLadder = insideLadder;

            // Vào thang khi nhấn W hoặc S trong vùng thang
            if (insideLadder && (climbUp || climbDown))
                player.IsClimbing = true;

            // Ra khỏi vùng thang → về Idle
            if (!insideLadder && player.IsClimbing)
            {
                player.IsClimbing = false;
                player.TransitionTo(PlayerState.Idle);
            }

            // Nhảy khi đang leo → thoát thang về Jumping
            if (player.IsClimbing && jump)
            {
                player.IsClimbing = false;
                player.VelocityY = -22;
                player.TransitionTo(PlayerState.Jumping);
            }
        }


        //  ONE-WAY DROP

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


        //  SPIKE — trừ 1/4 MaxHP + bất tử 1 giây

        private void HandleSpikeDamage()       
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


        //  UPDATE ENEMIES

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
                    HandleEnemyStairStep(enemy);

                    if (enemy.Bounds.Y > this.ClientSize.Height + 300)
                        enemy.TakeDamage(9999, 0, false);
                }
            }

            enemies.RemoveAll(e => e.IsDeadAnimationDone);
        }


        //  COMBAT

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

                int enemyDmg = (enemy is Boss boss && boss.IsPhase2) ? 20 : 10;
                int enemyKb = (enemy is Boss b2 && b2.IsPhase2) ? 25 : 15;
                player.TakeDamage(enemyDmg, enemyKb, enemy.FacingLeft);
                enemy.HasHitPlayer = true;
                sfx.Play("player_hurt");
            }

        }


        //  BUFF

        private void TryDropBuff()
        {
            if (isBossMap) return;
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


        //  MAP

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
                    SetPlayerSpawn(19, 175);
                    SetSlimeSpawn(130, 175); SetSkeletonSpawn(183, 370); SetSkeletonSpawn(178, 50); SetSlimeSpawn(400, 250);
                    break;
                case "map2.tmj":
                    SetPlayerSpawn(17, 256);
                    SetSlimeSpawn(300, 200); SetSkeletonSpawn(157, 360); SetSkeletonSpawn(450, 65);
                    break;
                case "map3.tmj":
                    SetPlayerSpawn(10, 255);
                    SetSkeletonSpawn(190, 160);
                    SetSkeletonSpawn(120, 30);
                    SetSkeletonSpawn(200, 30);
                    SetSkeletonSpawn(280, 30);
                    SetSkeletonSpawn(360, 30);
                    SetSkeletonSpawn(440, 30);
                    break;
                case "map4.tmj":
                    SetPlayerSpawn(18, 160);
                    SetSlimeSpawn(190, 120);
                    SetSkeletonSpawn(630, 160);
                    break;
                case "map5.tmj":
                    SetPlayerSpawn(29, 203);
                    SetSlimeSpawn(39, 98);
                    SetSkeletonSpawn(256, 239);
                    SetSkeletonSpawn(320, 112);
                    break;
                case "map6.tmj":
                    SetPlayerSpawn(10   , 190);
                    SetSkeletonSpawn(600, 270);
                    SetSlimeSpawn(200, 150);
                    SetSlimeSpawn(80, 200);
                    break;
                case "map7.tmj":
                    SetPlayerSpawn(32, 270);
                    SetSkeletonSpawn(200, 200);
                    SetSkeletonSpawn(400, 200);
                    SetSlimeSpawn(350, 150);
                    SetSlimeSpawn(400, 50);
                    SetSkeletonSpawn(400, 50);
                    SetSkeletonSpawn(300, 50);
                    break;
                case "map8.tmj":
                    SetPlayerSpawn(25, 220);
                    SetSlimeSpawn(180, 170);
                    SetSkeletonSpawn(480, 360);
                    SetSkeletonSpawn(520, 90);
                    break;
                case "map9.tmj":
                    SetPlayerSpawn(20, 170);
                    SetSlimeSpawn(200, 145);
                    SetSkeletonSpawn(270, 315);
                    SetSkeletonSpawn(350, 315);
                    SetSkeletonSpawn(530, 315);
                    break;
                default:
                    break;
            }
            player.VelocityY = 0;
        }
        private void SetPlayerSpawn(int x, int y, int scale = 3)
        {
            player.Bounds = new Rectangle(x * scale, y * scale, player.Bounds.Width, player.Bounds.Height);
        }
        private void SetSlimeSpawn(int x, int y, int scale = 3)
        {
            enemies.Add(new Slime(x * scale, y * scale, 3));
        }
        private void SetSkeletonSpawn(int x, int y, int scale = 2)
        {
            enemies.Add(new MeleeSkeleton(x * scale, y * scale, 2));
        }

        private void GoToNextMap()
        {
            isBossMap = false;

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
        private void CheckBossClear()
        {
            if (!isBossMap || bossCleared) return;
            if (enemies.Count == 0)
            {
                bossCleared = true;
                finalSoul = soul;
                finalMapCount = mapCount;
                currentScene = GameScene.GameClear;
            }
        }

        private void goToBossMap()
        {
            spikeHitThisContact = false;
            isBossMap = true;
            bossSummoned = true;
            soul = 0; 

            string mapPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Assets", "Map", "Bossmap.tmj");
            map = new TiledMap(mapPath, scale: 3);

            player.Bounds = new Rectangle(150, 50, player.Bounds.Width, player.Bounds.Height);
            enemies = new List<Enemy>(); 
            enemies.Add(new Boss(800, 50, 4));
        }


        //  RESET FRAME INPUT

        private void ResetFrameInput()
        {
            dash = false;
            lightAttack = false;
            interactE = false;
            dropDown = false;
        }


        //  RENDER

        private void Form1_Paint(object sender, PaintEventArgs e)
        {
            switch (currentScene)
            {
                case GameScene.Menu: DrawMenu(e.Graphics); break;
                case GameScene.Tutorial: DrawTutorial(e.Graphics); break;
                case GameScene.Playing: DrawGame(e.Graphics); break;
                case GameScene.GameOver: DrawGameOver(e.Graphics); break;
                case GameScene.GameClear: DrawGameClear(e.Graphics); break;
            }
        }

        private void DrawMenu(Graphics g) => ui.DrawMenu(g);

        private void DrawTutorial(Graphics g) => ui.DrawTutorial(g);
        private void DrawGameOver(Graphics g) => ui.DrawGameOver(g, finalSoul, finalMapCount);
        private void DrawGameClear(Graphics g) => ui.DrawGameClear(g, finalSoul, finalMapCount, activeBuffs.Count);

        private void DrawGame(Graphics g)
        {
            g.Clear(Color.Black);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;

            map.DrawMap(g);
            foreach (var enemy in enemies) enemy.Draw(g);
            player.Draw(g);

            ui.Draw(g, player, soul, SOUL_REQUIRED, mapCount, activeBuffs, isBossMap, bossSummoned);
            if (showBuffPopup)
                ui.DrawBuffPopup(g, buffChoices);
        }


        //  SCENE MANAGEMENT
        private void StartGame() { InitGame(); currentScene = GameScene.Playing; }
        private void GoToMenu() { currentScene = GameScene.Menu; }

        private void CheckGameOver()
        {
            if (player.CurrentState == PlayerState.Dead && player.IsDeadAnimationDone)
            {
                finalSoul = soul;
                finalMapCount = mapCount;
                currentScene = GameScene.GameOver;
            }
        }
    }
}