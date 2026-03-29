using System;
using System.Collections.Generic;
using System.Text;

namespace Platform_Game_Project
{
    public class Slime : Enemy
    {
        public Slime(int x, int y, int scale) : base(x, y, 64, 48, hp: 50, scale)
        {
            moveSpeed = 2; 
            LoadAllAnimations();
        }

        protected override void LoadAllAnimations()
        {
            string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Enemy", "Melee", "Slime");
            animations["Idle"] = LoadSpritesheet(Path.Combine(root, "Slime_Idle.png"), 4, 64, 48);
            animations["Run"] = LoadSpritesheet(Path.Combine(root, "Slime_Run.png"), 4, 64, 48);
            animations["Attack"] = LoadSpritesheet(Path.Combine(root, "Slime_Attack.png"), 4, 64, 48);
            animations["Hurt"] = LoadSpritesheet(Path.Combine(root, "Slime_Hurt.png"), 4, 64, 48);
            animations["Dead"] = LoadSpritesheet(Path.Combine(root, "Slime_Dead.png"), 6, 64, 48);
        }

        public override void UpdateAI(Player player)
        {
            if (IsDead) return;
            int dx = player.Bounds.X - hurtBox.X;
            FacingLeft = dx < 0;

            switch (CurrentState)
            {
                case EnemyState.Idle:
                    if (DetectRange.IntersectsWith(player.hurtBox))
                        TransitionTo(EnemyState.Running, "Run", 5);
                    break;

                case EnemyState.Running:
                    if (AttackRange.IntersectsWith(player.hurtBox))
                        TransitionTo(EnemyState.Attack, "Attack", 5);
                    else if (!DetectRange.IntersectsWith(player.hurtBox))
                        TransitionTo(EnemyState.Idle, "Idle", 5);
                    else
                        Bounds.X += dx > 0 ? moveSpeed : -moveSpeed;
                    break;

                case EnemyState.Attack:
                    if (IsLastFrame())
                    {
                        if (AttackRange.IntersectsWith(player.hurtBox))
                            TransitionTo(EnemyState.Attack, "Attack", 5);
                        else
                            TransitionTo(EnemyState.Running, "Run", 5);
                    }
                    break;

                case EnemyState.Hurt:
                    if (IsLastFrame())
                        TransitionTo(EnemyState.Idle, "Idle", 5);
                    break;

                case EnemyState.Dead:
                    return;
            }
        }

        protected override void UpdateHitbox()
        {
            IsHitboxActive = false;
            if (CurrentState != EnemyState.Attack) return;
            if (currentFrame >= 2 && currentFrame <= 3)
            {
                IsHitboxActive = true;
                ActiveHitbox = new Rectangle(
                    Bounds.X + 60, Bounds.Y + 70,
                    Bounds.Width - 120, Bounds.Height - 70
                );
            }
        }

        public override void UpdateHurtbox()
        {
            hurtBox = new Rectangle(
                Bounds.X + 60, Bounds.Y + 70,
                Bounds.Width - 120, Bounds.Height - 70
            );
            AttackRange = new Rectangle(
                Bounds.X + 60, Bounds.Y + 60,
                Bounds.Width - 120, Bounds.Height - 60
            );
            DetectRange = new Rectangle(
                Bounds.X - 400,
                Bounds.Y,
                Bounds.Width + 400 * 2,
                Bounds.Height
            );
        }
    }
}
