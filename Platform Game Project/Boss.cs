using System;
using System.Drawing;
using System.IO;

namespace Platform_Game_Project
{
    public enum BossPhase { Phase1, Phase2 }

    public class Boss : Enemy
    {
        private BossPhase phase = BossPhase.Phase1;
        public bool IsPhase2 => phase == BossPhase.Phase2;
        private int attackCooldown = 0;

        // Phase 1
        private const int MOVE_SPEED_P1 = 3;
        private const int ATTACK_RANGE_P1 = 120;
        private const int ATTACK_COOLDOWN_P1 = 60;

        // Phase 2
        private const int MOVE_SPEED_P2 = 6;
        private const int ATTACK_RANGE_P2 = 200; // Tầm xa hơn
        private const int FLY_SPEED = 4;   // Tốc độ bay ngang
        private const int ATTACK_COOLDOWN_P2 = 30;

        public Boss(int x, int y, int scale, int hp = 500)
            : base(x, y, 128, 96, hp, scale)
        {
            moveSpeed = MOVE_SPEED_P1;
            LoadAllAnimations();
        }

        protected override void LoadAllAnimations()
        {
            string root = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Assets", "Enemy", "Boss");

            animations["Idle"] = LoadFolder(Path.Combine(root, "Idle"));
            animations["Run"] = LoadFolder(Path.Combine(root, "Walk"));
            animations["Attack"] = LoadFolder(Path.Combine(root, "Attack1"));
            animations["Fly"] = LoadFolder(Path.Combine(root, "Fly"));
            animations["Attack2"] = LoadFolder(Path.Combine(root, "Attack2"));
            animations["Hurt"] = LoadFolder(Path.Combine(root, "Hurt"));
            animations["Dead"] = LoadFolder(Path.Combine(root, "Dead"));
        }

        public override void UpdateAI(Player player)
        {
            if (IsDead) return;
            if (phase == BossPhase.Phase1 && HP <= MaxHP / 2)
                EnterPhase2();

            int dx = player.Bounds.X - hurtBox.X;

            switch (phase)
            {
                case BossPhase.Phase1: UpdatePhase1(player, dx); break;
                case BossPhase.Phase2: UpdatePhase2(player, dx); break;
            }
        }

        private void UpdatePhase1(Player player, int dx)
        {
            if (attackCooldown > 0) attackCooldown--;

            switch (CurrentState)
            {
                case EnemyState.Idle:
                    if (attackCooldown <= 0)
                    {
                        FacingLeft = dx < 0; // Update hướng khi bắt đầu di chuyển
                        TransitionTo(EnemyState.Running, "Run", 4);
                    }
                    break;

                case EnemyState.Running:
                    FacingLeft = dx < 0; // Luôn nhìn về phía player khi chạy
                    if (AttackRange.IntersectsWith(player.hurtBox))
                    {
                        FacingLeft = dx < 0; // Chốt hướng trước khi attack
                        TransitionTo(EnemyState.Attack, "Attack", 5);
                    }
                    else
                        Bounds.X += dx > 0 ? moveSpeed : -moveSpeed;
                    break;

                case EnemyState.Attack:
                    // Không update FacingLeft — giữ nguyên hướng đã chốt
                    if (IsLastFrame())
                    {
                        attackCooldown = ATTACK_COOLDOWN_P1;
                        TransitionTo(EnemyState.Idle, "Idle", 4);
                    }
                    break;

                case EnemyState.Hurt:
                    if (IsLastFrame())
                        TransitionTo(EnemyState.Idle, "Idle", 4);
                    break;
            }
        }

        private void UpdatePhase2(Player player, int dx)
        {
            if (attackCooldown > 0) attackCooldown--;

            switch (CurrentState)
            {
                case EnemyState.Idle:
                    if (attackCooldown <= 0)
                    {
                        FacingLeft = dx < 0;
                        TransitionTo(EnemyState.Running, "Fly", 3);
                    }
                    break;

                case EnemyState.Running:
                    FacingLeft = dx < 0; // Luôn nhìn về phía player khi bay
                    if (AttackRange.IntersectsWith(player.hurtBox))
                    {
                        FacingLeft = dx < 0; // Chốt hướng trước khi attack
                        TransitionTo(EnemyState.Attack, "Attack2", 3);
                    }
                    else
                        Bounds.X += dx > 0 ? FLY_SPEED : -FLY_SPEED;
                    break;

                case EnemyState.Attack:
                    // Không update FacingLeft
                    if (IsLastFrame())
                    {
                        attackCooldown = ATTACK_COOLDOWN_P2;
                        TransitionTo(EnemyState.Idle, "Idle", 4);
                    }
                    break;

                case EnemyState.Hurt:
                    if (IsLastFrame())
                        TransitionTo(EnemyState.Running, "Fly", 3);
                    break;
            }
        }

        private void EnterPhase2()
        {
            phase = BossPhase.Phase2;
            moveSpeed = MOVE_SPEED_P2;
            attackCooldown = 0; // Reset cooldown khi vào phase 2
            TransitionTo(EnemyState.Running, "Fly", 3);
        }

        public override void Update(int gravity)
        {
            if (CurrentState == EnemyState.Dead)
            {
                AnimateOnce();
                return;
            }

            // Phase 2 bay ngang — không chịu gravity
            if (phase == BossPhase.Phase2)
            {
                Bounds.X += KnockbackX;
                KnockbackX = (int)(KnockbackX * 0.75f);
                if (Math.Abs(KnockbackX) < 1) KnockbackX = 0;
            }
            else
            {
                // Phase 1 bình thường — chịu gravity
                VelocityY += gravity;
                Bounds.Y += VelocityY;
                Bounds.X += KnockbackX;
                KnockbackX = (int)(KnockbackX * 0.75f);
                if (Math.Abs(KnockbackX) < 1) KnockbackX = 0;
            }

            Animate();
            UpdateHurtbox();
            UpdateHitbox();
            if (!IsHitboxActive) HasHitPlayer = false;
        }

        protected override void UpdateHitbox()
        {
            IsHitboxActive = false;
            if (CurrentState != EnemyState.Attack) return;

            if (phase == BossPhase.Phase1)
            {
                // Attack1 — hitbox phase 1
                if (currentFrame >= 3 && currentFrame <= 5) 
                {
                    IsHitboxActive = true;
                    ActiveHitbox = new Rectangle(
                        Bounds.X + (FacingLeft ? 20 : 200),
                        Bounds.Y + 170, 300, 80
                    );
                }
            }
            else
            {
                // Attack2 — tầm xa hơn phase 2
                if (currentFrame >= 3 && currentFrame <= 7)
                {
                    IsHitboxActive = true;
                    ActiveHitbox = new Rectangle(
                        Bounds.X + (FacingLeft ? 10 : 220),
                        Bounds.Y + 160, 280, 75
                    );
                }
            }
        }

        public override void UpdateHurtbox()
        {
            hurtBox = new Rectangle(
                Bounds.X + (FacingLeft ? 170 : 165), Bounds.Y + 110,
                170, 220
            );
            AttackRange = new Rectangle(
                Bounds.X + 70,
                Bounds.Y - 20,
                Bounds.Width - 150,
                Bounds.Height + 20  
            );
            DetectRange = new Rectangle(
                Bounds.X - 9999, Bounds.Y - 9999,
                Bounds.Width + 9999 * 2, Bounds.Height + 9999 * 2
            );
        }

        public override void Draw(Graphics g)
        {
            if (animations.ContainsKey(currentAnimKey) && animations[currentAnimKey].Count > 0)
                DrawImage(g, animations[currentAnimKey][currentFrame]);

            //g.DrawRectangle(Pens.Magenta, hurtBox);
            //g.DrawRectangle(Pens.Cyan, Bounds);
            //g.DrawRectangle(Pens.Yellow, DetectRange);  
            //g.DrawRectangle(Pens.Orange, AttackRange);  
            //if (IsHitboxActive) g.DrawRectangle(Pens.Red, ActiveHitbox);

            int barW = 600, barH = 20;
            int barX = 1440 / 2 - barW / 2; // Căn giữa màn hình 1440px
            int barY = 250;

            // Background
            g.FillRectangle(Brushes.DarkRed, barX, barY, barW, barH);

            // Fill HP
            g.FillRectangle(
                phase == BossPhase.Phase1 ? Brushes.LimeGreen : Brushes.OrangeRed,
                barX, barY,
                (int)((float)HP / MaxHP * barW), barH);

            // Border
            g.DrawRectangle(Pens.Gold, barX, barY, barW, barH);

            // Label
            string label = phase == BossPhase.Phase1 ? "☠ BOSS ☠" : "☠ BOSS - PHASE 2 ☠";
            var font = new Font("Courier New", 10, FontStyle.Bold);
            var sz = g.MeasureString(label, font);
            g.DrawString(label,
                font,
                phase == BossPhase.Phase1 ? Brushes.White : Brushes.OrangeRed,
                barX + (barW - sz.Width) / 2, barY - 18);
        }
    }
}