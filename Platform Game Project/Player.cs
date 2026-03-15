using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace Platform_Game_Project
{
    public enum PlayerState
    {
        Idle, Running, Jumping, Falling,
        Dashing, Hurt, Dead,
        LightAttack, HeavyAttack, DashAttack
    }

    public class Player : Entity
    {
        public PlayerState CurrentState = PlayerState.Idle;
        public Rectangle ActiveHitbox;
        public bool IsHitboxActive = false;
        public HashSet<Enemy> HitEnemiesThisSwing = new HashSet<Enemy>();
        public bool IsDeadAnimationDone =>
        CurrentState == PlayerState.Dead &&
        currentFrame == animations[currentAnimKey].Count - 1 &&
        frameTimer >= frameDelay - 1;

        // Combo
        private int comboTimer = 0;
        private const int COMBO_WINDOW = 15;
        private bool hasAttackInput, canFollowUp;

        // Dash
        private int dashTimer = 0;
        private int dashCooldownTimer = 0;
        private const int DASH_COOLDOWN = 60;
        private const int DASH_DURATION = 7;
        private const int DASH_SPEED = 30;

        // Dmg
        public int CurrentAttackDamage => CurrentState switch
        {
            PlayerState.LightAttack => 10,
            PlayerState.HeavyAttack => 30,
            PlayerState.DashAttack => 20,
            _ => 0
        };

        public int CurrentAttackKnockback => CurrentState switch
        {
            PlayerState.LightAttack => 5,
            PlayerState.HeavyAttack => 10,
            PlayerState.DashAttack => 12,
            _ => 0
        };

        public Player(int x, int y, int scale)
            : base(x, y, 64 , 44, hp: 100, scale)
        {
            LoadAllAnimations();
        }

        protected override void LoadAllAnimations()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string playerPath = Path.Combine(baseDir, "Assets", "Player");

            animations["Idle"] = LoadFolder(Path.Combine(playerPath, "Idle"));
            animations["Running"] = LoadFolder(Path.Combine(playerPath, "Run"));
            animations["Jumping"] = LoadFolder(Path.Combine(playerPath, "Jump"));
            animations["Falling"] = LoadFolder(Path.Combine(playerPath, "Fall"));
            animations["Dashing"] = LoadFolder(Path.Combine(playerPath, "Dash"));
            animations["Hurt"] = LoadFolder(Path.Combine(playerPath, "Hurt"));
            animations["LightAttack"] = LoadFolder(Path.Combine(playerPath, "Light Attack"));
            animations["HeavyAttack"] = LoadFolder(Path.Combine(playerPath, "Heavy Attack"));
            animations["DashAttack"] = LoadFolder(Path.Combine(playerPath, "Dash Attack"));
            animations["Dead"] = LoadFolder(Path.Combine(playerPath, "Dead"));
        }

        public void HandleState(bool isMoving, bool isJumping, bool isDashing, bool isLightAttacking, bool isDashAttacking)
        {
            if (CurrentState == PlayerState.Dead) return;

            if (dashTimer > 0) dashTimer--;
            if (dashCooldownTimer > 0) dashCooldownTimer--;
            if (comboTimer > 0) comboTimer--;
            else canFollowUp = false;
            if (isLightAttacking) hasAttackInput = true;

            switch (CurrentState)
            {
                case PlayerState.Idle:
                    if (!IsOnPlatform) TransitionTo(PlayerState.Falling);
                    else if (isMoving) TransitionTo(PlayerState.Running);
                    else if (isJumping) StartJump();
                    else if (isDashing && dashCooldownTimer <= 0) StartDash();
                    else if (hasAttackInput) StartAttack(isMoving);
                    break;

                case PlayerState.Running:
                    if (!IsOnPlatform) TransitionTo(PlayerState.Falling);
                    else if (!isMoving) TransitionTo(PlayerState.Idle);
                    else if (isJumping) StartJump();
                    else if (isDashing && dashCooldownTimer <= 0) StartDash();
                    else if (hasAttackInput) StartAttack(isMoving);
                    break;

                case PlayerState.Dashing:
                    if (dashTimer <= 0)
                    {
                        dashCooldownTimer = DASH_COOLDOWN;
                        TransitionTo(isMoving ? PlayerState.Running : PlayerState.Idle);
                    }
                    else if (isDashAttacking) TransitionTo(PlayerState.DashAttack);
                    break;

                case PlayerState.Jumping:
                    if (VelocityY > 0) TransitionTo(PlayerState.Falling);
                    else if (isDashing && dashCooldownTimer <= 0) StartDash();
                    break;

                case PlayerState.Falling:
                    if (IsOnPlatform)
                        TransitionTo(isMoving ? PlayerState.Running : PlayerState.Idle);
                    break;
                case PlayerState.Hurt:
                    if (IsLastFrame())
                        TransitionTo(IsOnPlatform ? (isMoving ? PlayerState.Running : PlayerState.Idle)
                                                  : PlayerState.Falling);
                    break;
                case PlayerState.Dead:               
                    break;
                case PlayerState.LightAttack:
                    if (IsLastFrame())
                    {
                        if (hasAttackInput)
                        {
                            TransitionTo(PlayerState.HeavyAttack);
                            hasAttackInput = false;
                            canFollowUp = false;
                        }
                        else
                        {
                            canFollowUp = true;
                            comboTimer = COMBO_WINDOW;
                            TransitionTo(isMoving ? PlayerState.Running : PlayerState.Idle);
                        }
                    }
                    break;

                case PlayerState.HeavyAttack:
                case PlayerState.DashAttack:
                    if (IsLastFrame())
                        TransitionTo(isMoving ? PlayerState.Running : PlayerState.Idle);
                    break;
            }
        }

        private void StartJump()
        {
            VelocityY = -25;
            IsOnPlatform = false;
            TransitionTo(PlayerState.Jumping);
        }

        private void StartDash()
        {
            dashTimer = DASH_DURATION;
            TransitionTo(PlayerState.Dashing);
        }

        private void StartAttack(bool isMoving)
        {
            TransitionTo(canFollowUp && comboTimer > 0 ? PlayerState.HeavyAttack : PlayerState.LightAttack);
            canFollowUp = false;
            hasAttackInput = false;
        }

        public void TakeDamage(int damage, int knockback, bool enemyFacingLeft)
        {
            if (HP <= 0) return;
            HP -= damage;
            Bounds.X += enemyFacingLeft ? -knockback : knockback;

            if (HP <= 0)
                TransitionTo(PlayerState.Dead);
            else
                TransitionTo(PlayerState.Hurt);
        }

        private void TransitionTo(PlayerState newState)
        {
            if (CurrentState == newState) return;

            bool isNewAttack = newState == PlayerState.LightAttack
                || newState == PlayerState.HeavyAttack
                || newState == PlayerState.DashAttack;
            if (isNewAttack) HitEnemiesThisSwing.Clear();

            CurrentState = newState;
            currentFrame = frameTimer = 0;
            frameDelay = newState switch
            {
                PlayerState.Idle => 4,
                PlayerState.Running => 3,
                PlayerState.Falling => 6,
                PlayerState.Jumping => 6,
                PlayerState.Dashing => 3,
                PlayerState.Hurt => 3,
                PlayerState.LightAttack => 2,
                PlayerState.HeavyAttack => 3,
                PlayerState.DashAttack => 2,
                PlayerState.Dead => 4,
                _ => 6
            };
            currentAnimKey = newState.ToString();
        }

        public override void Update(int gravity)
        {
            Update(gravity, 0);
        }

        public void Update(int gravity, int moveDir)
        {
            if (CurrentState == PlayerState.Dead)
            {
                AnimateOnce(); // Chạy 1 lần rồi dừng, không loop
                return;
            }

            if (CurrentState == PlayerState.Dashing)
            {
                Bounds.X += (FacingLeft ? -1 : 1) * DASH_SPEED;
                VelocityY = 0;
            }
            else
            {
                VelocityY += gravity;
                Bounds.Y += VelocityY;
                Bounds.X += moveDir * 15;
            }

            Animate();
            UpdateHitbox();
            UpdateHurtbox();
        }

        private void UpdateHitbox()
        {
            IsHitboxActive = false;

            if (CurrentState == PlayerState.LightAttack && currentFrame >= 5 && currentFrame <= 7)
            {
                IsHitboxActive = true;
                ActiveHitbox = new Rectangle(Bounds.X + (FacingLeft ? 10 : 70), Bounds.Y, 120, 100);
            }
            else if (CurrentState == PlayerState.HeavyAttack && currentFrame <= 2)
            {
                IsHitboxActive = true;
                ActiveHitbox = new Rectangle(Bounds.X + (FacingLeft ? 30 : 20), Bounds.Y, 150, 100);
            }
            else if (CurrentState == PlayerState.DashAttack && currentFrame >= 3 && currentFrame <= 4)
            {
                IsHitboxActive = true;
                ActiveHitbox = new Rectangle(Bounds.X + (FacingLeft ? 10 : 60), Bounds.Y + 20, 130, 120);
            }
        }

        public override void UpdateHurtbox()
        {
            hurtBox = new Rectangle(Bounds.X + (FacingLeft ? 85 : 60), Bounds.Y + 40, Bounds.Width - 150, Bounds.Height - 40);
        }

        public override void Draw(Graphics g)
        {
            if (!animations.ContainsKey(currentAnimKey) || animations[currentAnimKey].Count == 0) return;

            DrawImage(g, animations[currentAnimKey][currentFrame]);

            g.DrawRectangle(Pens.Red, hurtBox);
            if (IsHitboxActive) g.DrawRectangle(Pens.Yellow, ActiveHitbox);
            g.DrawRectangle(Pens.Cyan, Bounds);

            g.FillRectangle(Brushes.DarkRed, Bounds.X, Bounds.Y - 10, Bounds.Width, 6);
            g.FillRectangle(Brushes.LimeGreen, Bounds.X, Bounds.Y - 10,
                            (int)((float)HP / MaxHP * Bounds.Width), 6);
        }
    }
}