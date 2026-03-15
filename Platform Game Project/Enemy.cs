using System.Drawing;

namespace Platform_Game_Project
{
    public enum EnemyState
    {
        Idle, Running, Attack, Hurt, Dead
    }

    public abstract class Enemy : Entity
    {
        public EnemyState CurrentState = EnemyState.Idle;
        public int KnockbackX = 0;
        public Rectangle AttackRange;
        public Rectangle DetectRange;
        public Rectangle ActiveHitbox;
        public bool IsHitboxActive = false;
        public bool HasHitPlayer = false;

        protected int attackRangeSize = 10;
        protected int detectRangeSize = 300;

        protected Enemy(int x, int y, int width, int height, int hp, int scale)
            : base(x, y, width, height, hp, scale) { }

        public abstract void UpdateAI(Player player);
        protected abstract void UpdateHitbox();


        protected void TransitionTo(EnemyState newState, string animKey, int delay)
        {
            if (CurrentState == newState) return;
            CurrentState = newState;
            currentFrame = frameTimer = 0;
            frameDelay = delay;
            currentAnimKey = animKey;
        }

        public void TakeDamage(int damage, int knockback, bool playerFacingLeft)
        {
            if (IsDead) return;
            HP -= damage;
            KnockbackX = playerFacingLeft ? -knockback : knockback;
            TransitionTo(EnemyState.Hurt, "Hurt", 3);
        }

        public override void Update(int gravity)
        {
            VelocityY += gravity;
            Bounds.Y += VelocityY;
            Bounds.X += KnockbackX;
            KnockbackX = (int)(KnockbackX * 0.75f);
            if (Math.Abs(KnockbackX) < 1) KnockbackX = 0;
            Animate();
            UpdateHurtbox();
            UpdateHitbox();

            if (!IsHitboxActive) HasHitPlayer = false;
        }

        public override abstract void UpdateHurtbox();

        public override void Draw(Graphics g)
        {
            if (!animations.ContainsKey(currentAnimKey) || animations[currentAnimKey].Count == 0)
            {
                g.FillRectangle(Brushes.Red, Bounds);
            }
            else
            {
                DrawImage(g, animations[currentAnimKey][currentFrame]);
            }

            g.DrawRectangle(Pens.Magenta, hurtBox);
            g.DrawRectangle(Pens.Yellow, DetectRange);
            g.DrawRectangle(Pens.Orange, AttackRange);
            if (IsHitboxActive) g.DrawRectangle(Pens.Red, ActiveHitbox);

            g.FillRectangle(Brushes.DarkRed, Bounds.X, Bounds.Y - 10, Bounds.Width, 6);
            g.FillRectangle(Brushes.LimeGreen, Bounds.X, Bounds.Y - 10,
                            (int)((float)HP / MaxHP * Bounds.Width), 6);
        }
    }
}