using System;
using System.Drawing;
using System.IO;

namespace Platform_Game_Project
{
    public enum BossPhase { Phase1, Phase2 }

    public class Boss : Enemy
    {
        private BossPhase phase = BossPhase.Phase1;

        // Phase 1
        private const int MOVE_SPEED_P1 = 3;
        private const int ATTACK_RANGE_P1 = 120;

        // Phase 2
        private const int MOVE_SPEED_P2 = 6;
        private const int ATTACK_RANGE_P2 = 200; // Tầm xa hơn
        private const int FLY_SPEED = 4;   // Tốc độ bay ngang

        public Boss(int x, int y, int scale, int hp = 500)
            : base(x, y, 128, 96, hp, scale)
        {
            moveSpeed = MOVE_SPEED_P1;
            detectRangeSize = 9999; // Boss luôn aggro
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

            // Check chuyển phase
            if (phase == BossPhase.Phase1 && HP <= MaxHP / 2)
                EnterPhase2();

            int dx = player.Bounds.X - Bounds.X;
            FacingLeft = dx < 0;

            switch (phase)
            {
                case BossPhase.Phase1: UpdatePhase1(player, dx); break;
                case BossPhase.Phase2: UpdatePhase2(player, dx); break;
            }
        }

        private void UpdatePhase1(Player player, int dx)
        {
            switch (CurrentState)
            {
                case EnemyState.Idle:
                    TransitionTo(EnemyState.Running, "Run", 4);
                    break;

                case EnemyState.Running:
                    if (AttackRange.IntersectsWith(player.hurtBox))
                        TransitionTo(EnemyState.Attack, "Attack", 5);
                    else
                        Bounds.X += dx > 0 ? moveSpeed : -moveSpeed;
                    break;

                case EnemyState.Attack:
                    if (IsLastFrame())
                    {
                        if (AttackRange.IntersectsWith(player.hurtBox))
                            TransitionTo(EnemyState.Attack, "Attack", 5);
                        else
                            TransitionTo(EnemyState.Running, "Run", 4);
                    }
                    break;

                case EnemyState.Hurt:
                    if (IsLastFrame())
                        TransitionTo(EnemyState.Running, "Run", 4);
                    break;
            }
        }

        private void UpdatePhase2(Player player, int dx)
        {
            switch (CurrentState)
            {
                case EnemyState.Running: // Dùng Running cho trạng thái Fly
                    if (AttackRange.IntersectsWith(player.hurtBox))
                        TransitionTo(EnemyState.Attack, "Attack2", 3);
                    else
                        Bounds.X += dx > 0 ? FLY_SPEED : -FLY_SPEED; // Bay ngang
                    break;

                case EnemyState.Attack:
                    if (IsLastFrame())
                    {
                        if (AttackRange.IntersectsWith(player.hurtBox))
                            TransitionTo(EnemyState.Attack, "Attack2", 3);
                        else
                            TransitionTo(EnemyState.Running, "Fly", 3);
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

            // Chuyển sang Fly animation
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
                if (currentFrame >= 4 && currentFrame <= 7)
                {
                    IsHitboxActive = true;
                    ActiveHitbox = new Rectangle(
                        Bounds.X + (FacingLeft ? 0 : Bounds.Width - 150),
                        Bounds.Y + 40, 150, Bounds.Height - 60
                    );
                }
            }
            else
            {
                // Attack2 — tầm xa hơn phase 2
                if (currentFrame >= 3 && currentFrame <= 6)
                {
                    IsHitboxActive = true;
                    ActiveHitbox = new Rectangle(
                        Bounds.X + (FacingLeft ? -50 : Bounds.Width - 100),
                        Bounds.Y + 20, 200, Bounds.Height - 40
                    );
                }
            }
        }

        public override void UpdateHurtbox()
        {
            hurtBox = new Rectangle(
                Bounds.X + 30, Bounds.Y + 20,
                Bounds.Width - 60, Bounds.Height - 40
            );
            AttackRange = new Rectangle(
                Bounds.X - 60,
                Bounds.Y - 20,
                Bounds.Width + 60 * 2,
                Bounds.Height + 20
            );
            DetectRange = new Rectangle(
                Bounds.X - 9999, Bounds.Y - 9999,
                Bounds.Width + 9999 * 2, Bounds.Height + 9999 * 2
            );
        }

        public override void Draw(Graphics g)
        {
            // Phase 2 — tint màu đỏ để báo hiệu
            if (phase == BossPhase.Phase2 &&
                animations.ContainsKey(currentAnimKey) &&
                animations[currentAnimKey].Count > 0)
            {
                // Vẽ bình thường, sau này có thể thêm shader effect
                DrawImage(g, animations[currentAnimKey][currentFrame]);
            }
            else
            {
                base.Draw(g);
                return;
            }

            g.DrawRectangle(Pens.Magenta, hurtBox);
            g.DrawRectangle(Pens.Orange, AttackRange);
            if (IsHitboxActive) g.DrawRectangle(Pens.Red, ActiveHitbox);

            // HP bar to hơn enemy thường
            int barW = Bounds.Width;
            g.FillRectangle(Brushes.DarkRed, Bounds.X, Bounds.Y - 14, barW, 8);
            g.FillRectangle(phase == BossPhase.Phase1 ? Brushes.LimeGreen : Brushes.OrangeRed,
                Bounds.X, Bounds.Y - 14,
                (int)((float)HP / MaxHP * barW), 8);

            // Label phase
            g.DrawString(phase == BossPhase.Phase1 ? "BOSS - Phase 1" : "BOSS - Phase 2 !!",
                new Font("Courier New", 9, FontStyle.Bold),
                phase == BossPhase.Phase1 ? Brushes.White : Brushes.OrangeRed,
                Bounds.X, Bounds.Y - 28);
        }
    }
}