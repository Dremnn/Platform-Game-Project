using Platform_Game_Project;

public abstract class MeleeEnemy : Enemy
{
    protected int moveSpeed = 2;

    protected MeleeEnemy(int x, int y, int width, int height, int hp, int scale)
        : base(x, y, width, height, hp, scale) { }

    public override void UpdateAI(Player player)
    {
        if (IsDead) return;

        int dx = player.Bounds.X - Bounds.X;
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
                // Đợi animation attack xong mới chuyển state
                if (IsLastFrame())
                {
                    if (AttackRange.IntersectsWith(player.hurtBox))
                        TransitionTo(EnemyState.Attack, "Attack", 4); // Đánh tiếp
                    else
                        TransitionTo(EnemyState.Running, "Run", 3);
                }
                break;

            case EnemyState.Hurt:
                // Đợi animation hurt xong mới cho AI tiếp tục
                if (IsLastFrame())
                    TransitionTo(EnemyState.Idle, "Idle", 4);
                break;
        }
    }
}