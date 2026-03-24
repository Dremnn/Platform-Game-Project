using Platform_Game_Project;

public class MeleeSkeleton : Enemy
{
    public MeleeSkeleton(int x, int y, int scale) : base(x, y, 96, 64, hp: 80, scale)
    {
        moveSpeed = 2;
        detectRangeSize = 100;
        LoadAllAnimations();
    }

    protected override void LoadAllAnimations()
    {
        string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Enemy", "Melee", "Skeleton");
        animations["Idle"] = LoadSpritesheet(Path.Combine(root, "Skeleton_01_White_Idle.png"), 8, 96, 64);
        animations["Run"] = LoadSpritesheet(Path.Combine(root, "Skeleton_01_White_Walk.png"), 10, 96, 64);
        animations["Attack"] = LoadSpritesheet(Path.Combine(root, "Skeleton_01_White_Attack1.png"), 10, 96, 64);
        animations["Hurt"] = LoadSpritesheet(Path.Combine(root, "Skeleton_01_White_Hurt.png"), 5, 96, 64);
        animations["Dead"] = LoadSpritesheet(Path.Combine(root, "Skeleton_01_White_Dead.png"), 13, 96, 64);
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
                    TransitionTo(EnemyState.Running, "Run", 3);
                break;

            case EnemyState.Running:
                if (AttackRange.IntersectsWith(player.hurtBox))
                    TransitionTo(EnemyState.Attack, "Attack", 4);
                else if (!DetectRange.IntersectsWith(player.hurtBox))
                    TransitionTo(EnemyState.Idle, "Idle", 4);
                else
                    Bounds.X += dx > 0 ? moveSpeed : -moveSpeed;
                break;

            case EnemyState.Attack:
                if (IsLastFrame())
                {
                    if (AttackRange.IntersectsWith(player.hurtBox))
                        TransitionTo(EnemyState.Attack, "Attack", 4);
                    else
                        TransitionTo(EnemyState.Running, "Run", 3);
                }
                break;

            case EnemyState.Hurt:
                if (IsLastFrame())
                    TransitionTo(EnemyState.Idle, "Idle", 4);
                break;

            case EnemyState.Dead:
                return;
        }
    }

    protected override void UpdateHitbox()
    {
        IsHitboxActive = false;
        if (CurrentState != EnemyState.Attack) return;
        if (currentFrame >= 6 && currentFrame <= 8)
        {
            IsHitboxActive = true;
            ActiveHitbox = new Rectangle(
                Bounds.X + (FacingLeft ? 0 : Bounds.Width - 100),
                Bounds.Y + 30, 100, Bounds.Height - 80
            );
        }
    }

    public override void UpdateHurtbox()
    {
        hurtBox = new Rectangle(
            Bounds.X + (FacingLeft ? 80 : 70),
            Bounds.Y + 40,
            Bounds.Width - 150,
            Bounds.Height - 40
        );
        AttackRange = new Rectangle(Bounds.X, Bounds.Y - 30, Bounds.Width, Bounds.Height + 30);
        DetectRange = new Rectangle(
            Bounds.X - detectRangeSize,
            Bounds.Y - detectRangeSize / 2,
            Bounds.Width + detectRangeSize * 2,
            Bounds.Height + detectRangeSize
        );
    }
}