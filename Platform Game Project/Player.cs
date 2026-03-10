using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace Platform_Game_Project
{
    public enum PlayerState 
    { 
        Idle, 
        Running, 
        Jumping, 
        Falling,
        Dashing,
        LightAttack, HeavyAttack,
        DashAttack
    }

    public class Player
    {
        public Rectangle Bounds;
        public int VelocityY = 0;
        public bool IsOnPlatform = false;
        public bool FacingLeft = false;

        public Rectangle hurtBox;
        public Rectangle ActiveHitbox; // Vùng tấn công hiện tại
        public bool IsHitboxActive = false; // Chỉ bật khi đang ở đúng frame chém
        public bool HasHitEnemy = false; // Đảm bảo mỗi đòn chỉ trúng 1 lần mỗi kẻ địch

        // Combo Attack Management
        private int comboTimer = 0;      // Bộ đếm thời gian ngược để chờ nhấn phím
        private const int COMBO_WINDOW = 30; // Khoảng thời gian cửa sổ (ví dụ 30 tick ~ 0.6s)
        private bool hasAttackInput, canFollowUp;

        // Dash Management
        private int dashTimer = 0;
        private int dashCooldownTimer = 0;
        private const int DASH_COOLDOWN = 60; // Thời gian hồi sau khi dash (ví dụ 60 tick ~ 1s)
        private const int DASH_DURATION = 7; // Thời gian dash kéo dài (ví dụ 15 tick ~ 0.25s)
        private const int DASH_SPEED = 30; // Tốc độ di chuyển khi dash

        // Quản lý Animation
        private Dictionary<PlayerState, List<Image>> animations = new Dictionary<PlayerState, List<Image>>();
        public PlayerState CurrentState = PlayerState.Idle;
        private int currentFrame = 0;
        private int frameTimer = 0;
        private int frameDelay = 6;

        public Player(int x, int y, int scale)
        {
            Bounds = new Rectangle(x, y, 64 * scale, 44 * scale);
            LoadAllAnimations();
        }

        private void LoadAllAnimations()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string playerPath = Path.Combine(baseDir, "Assets", "Player");

            animations[PlayerState.Idle] = LoadFolder(Path.Combine(playerPath, "Idle"));
            animations[PlayerState.Falling] = LoadFolder(Path.Combine(playerPath, "Fall"));
            animations[PlayerState.Running] = LoadFolder(Path.Combine(playerPath, "Run"));
            animations[PlayerState.Jumping] = LoadFolder(Path.Combine(playerPath, "Jump"));
            animations[PlayerState.Dashing] = LoadFolder(Path.Combine(playerPath, "Dash"));
            animations[PlayerState.DashAttack] = LoadFolder(Path.Combine(playerPath, "Dash Attack"));
            animations[PlayerState.LightAttack] = LoadFolder(Path.Combine(playerPath, "Light Attack"));
            animations[PlayerState.HeavyAttack] = LoadFolder(Path.Combine(playerPath, "Heavy Attack"));
        }

        private List<Image> LoadFolder(string path)
        {
            List<Image> frames = new List<Image>();
            if (Directory.Exists(path))
            {
                var files = Directory.GetFiles(path, "*.png").OrderBy(f => f).ToList();
                foreach (var file in files) frames.Add(Image.FromFile(file));
            }
            return frames;
        }

        // --- STATE MACHINE LOGIC ---
        public void HandleState(bool isMoving, bool isJumping, bool isDashing, bool isLightAttacking, bool isDashAttacking)
        {
            if(dashTimer > 0) dashTimer--;
            if(dashCooldownTimer > 0) dashCooldownTimer--;

            if (comboTimer > 0) comboTimer--;
            else canFollowUp = false;
            if(isLightAttacking) hasAttackInput = true;

            switch (CurrentState)
                {
                    case PlayerState.Idle:
                        if (!IsOnPlatform) TransitionTo(PlayerState.Falling);
                        else if (isMoving) TransitionTo(PlayerState.Running);
                        else if (isJumping)
                        {
                            VelocityY = -25; // Lực nhảy (số âm để bay ngược lên trên)
                            IsOnPlatform = false; // Rời khỏi sàn
                            TransitionTo(PlayerState.Jumping);
                        }
                        else if (isDashing && dashCooldownTimer <= 0)
                        {
                            dashTimer = DASH_DURATION; // Bắt đầu dash
                            TransitionTo(PlayerState.Dashing);
                        }
                        else if (hasAttackInput)
                        {
                            if (canFollowUp && comboTimer > 0)
                            {
                                // Nếu nhấn J trong lúc đang chờ ở Idle -> Tung đòn Heavy
                                TransitionTo(PlayerState.HeavyAttack);
                            }
                            else
                            {
                                // Nếu nhấn J khi đã hết thời gian chờ -> Đánh lại đòn Light
                                TransitionTo(PlayerState.LightAttack);
                            }

                            // Reset cả 2 biến sau khi đã quyết định đòn đánh
                            canFollowUp = false;
                            hasAttackInput = false;
                        }
                        break;

                    case PlayerState.Running:
                        if (!IsOnPlatform) TransitionTo(PlayerState.Falling);
                        else if (!isMoving) TransitionTo(PlayerState.Idle);
                        else if (isJumping)
                        {
                            VelocityY = -25; // Lực nhảy (số âm để bay ngược lên trên)
                            IsOnPlatform = false; // Rời khỏi sàn
                            TransitionTo(PlayerState.Jumping);
                        }
                        else if (isDashing && dashCooldownTimer <= 0)
                        {
                            dashTimer = DASH_DURATION; // Bắt đầu dash
                            TransitionTo(PlayerState.Dashing);
                        }
                        else if (hasAttackInput)
                            {
                            if (canFollowUp && comboTimer > 0)
                            {
                                // Nếu nhấn J trong lúc đang chờ ở Idle -> Tung đòn Heavy
                                TransitionTo(PlayerState.HeavyAttack);
                            }
                            else
                            {
                                // Nếu nhấn J khi đã hết thời gian chờ -> Đánh lại đòn Light
                                TransitionTo(PlayerState.LightAttack);
                            }

                            // Reset cả 2 biến sau khi đã quyết định đòn đánh
                            canFollowUp = false;
                            hasAttackInput = false;
                        }
                        break;

                    case PlayerState.Dashing:
                        if (dashTimer <= 0)
                        {
                            dashCooldownTimer = DASH_COOLDOWN; // Bắt đầu cooldown sau khi dash kết thúc
                            TransitionTo(isMoving ? PlayerState.Running : PlayerState.Idle);
                        }
                        else if (isDashAttacking)
                        {
                            TransitionTo(PlayerState.DashAttack);
                        }
                        break;

                    case PlayerState.Jumping:
                        // Nếu vận tốc bắt đầu dương (>0) tức là bắt đầu rơi xuống
                        if (VelocityY > 0) TransitionTo(PlayerState.Falling);
                        else if (isDashing && dashCooldownTimer <= 0)
                        {
                            dashTimer = DASH_DURATION; // Bắt đầu dash
                            TransitionTo(PlayerState.Dashing);
                        }
                    break;

                    case PlayerState.Falling:
                        if (IsOnPlatform)
                        {
                            if (isMoving) TransitionTo(PlayerState.Running);
                            else TransitionTo(PlayerState.Idle);
                        }
                        break;
                    case PlayerState.LightAttack:
                        if (currentFrame == animations[PlayerState.LightAttack].Count - 1 && frameTimer >= frameDelay - 1)
                        {
                            if (hasAttackInput)
                            {
                                TransitionTo(PlayerState.HeavyAttack);
                                hasAttackInput = false;
                                canFollowUp = false;

                            }
                            else
                            { // Hết ảnh mà không bấm kịp
                                canFollowUp = true;
                                comboTimer = COMBO_WINDOW; // Cho phép bấm tiếp trong khoảng thời gian này
                                TransitionTo(isMoving ? PlayerState.Running : PlayerState.Idle);
                            }
                        }
                        break;
                    case PlayerState.HeavyAttack:
                        if (currentFrame == animations[PlayerState.HeavyAttack].Count - 1 && frameTimer >= frameDelay - 1)
                        {
                            TransitionTo(isMoving ? PlayerState.Running : PlayerState.Idle);
                        }
                        break;
                    case PlayerState.DashAttack:
                        if (currentFrame == animations[PlayerState.DashAttack].Count - 1 && frameTimer >= frameDelay - 1)
                        {
                            TransitionTo(isMoving ? PlayerState.Running : PlayerState.Idle);
                        }
                        break;
                }
        }

        private void TransitionTo(PlayerState newState)
        {
            if (CurrentState != newState)
            {
                CurrentState = newState;
                currentFrame = 0;   // Reset về frame đầu tiên
                frameTimer = 0;     // Reset bộ đếm thời gian

                switch (newState)
                {
                    case PlayerState.Idle:
                        // Số càng lớn = Càng chậm
                        frameDelay = 4;
                        break;

                    case PlayerState.Running:
                        // Số càng nhỏ = Càng nhanh (chân đảo liên tục)
                        frameDelay = 3;
                        break;

                    case PlayerState.Falling:
                        // Thường rơi chỉ có 1-2 ảnh, để mức trung bình
                        frameDelay = 6;
                        break;
                    case PlayerState.Jumping:
                        frameDelay = 6;
                        break;
                    case PlayerState.Dashing:
                        frameDelay = 3;
                        break;
                    case PlayerState.LightAttack:
                        frameDelay = 2;
                        break;
                    case PlayerState.HeavyAttack:
                        frameDelay = 4;
                        break;
                    case PlayerState.DashAttack:
                        frameDelay = 2;
                        break;
                }
            }
        }

        public void Update(int gravity, int moveDir)
        {
            if (CurrentState == PlayerState.Dashing)
            {
                int direction = FacingLeft ? -1 : 1;
                Bounds.X += direction * DASH_SPEED; // Di chuyển nhanh hơn khi dash
                VelocityY = 0;
            }
            else
            {
                VelocityY += gravity;
                Bounds.Y += VelocityY;
                Bounds.X += moveDir * 15; // moveDir là -1, 0, hoặc 1
            }

            Animate();
            UpdateHitbox();
            UpdateHurtbox();
        }

        // Hàm để tính toán Hitbox dựa trên đòn đánh và hướng nhìn
        private void UpdateHitbox()
        {
            if (CurrentState == PlayerState.LightAttack)
            {
                // Bật hitbox từ frame thứ 2 đến frame thứ 4 (ví dụ vậy)
                if (currentFrame >= 5 && currentFrame <= 7)
                {
                    IsHitboxActive = true;
                    int offsetX = FacingLeft ? 20 : 200;
                    ActiveHitbox = new Rectangle(Bounds.X + offsetX, Bounds.Y + 20, 100, 150);
                }
                else IsHitboxActive = false;
            }
            else if (CurrentState == PlayerState.HeavyAttack)
            {
                if (currentFrame >= 0 && currentFrame <= 2)
                {
                    IsHitboxActive = true;
                    int offsetX = FacingLeft ? 50 : 50;
                    ActiveHitbox = new Rectangle(Bounds.X + offsetX, Bounds.Y + 20, 220, 150);
                }
                else IsHitboxActive = false;
            }
            else if (CurrentState == PlayerState.DashAttack)
            {
                if (currentFrame >= 3 && currentFrame <= 4)
                {
                    IsHitboxActive = true;
                    int offsetX = FacingLeft ? 20 : 150;
                    ActiveHitbox = new Rectangle(Bounds.X + offsetX, Bounds.Y + 20, 150, 200);
                }
            }
            else
            {
                IsHitboxActive = false;
            }
        }

        public void UpdateHurtbox()
        {
            int offsetX = FacingLeft ? 80 : 70;
            hurtBox = new Rectangle(Bounds.X + offsetX, Bounds.Y + 40, Bounds.Width - 200, Bounds.Height - 40);
        }

        private void Animate()
        {
            // Kiểm tra kho ảnh của trạng thái hiện tại có ảnh nào không
            if (!animations.ContainsKey(CurrentState) || animations[CurrentState].Count == 0)
                return;

            frameTimer++;
            if (frameTimer >= frameDelay)
            {
                // % giúp vòng lặp quay lại frame 0 khi đi đến cuối danh sách
                currentFrame = (currentFrame + 1) % animations[CurrentState].Count;
                frameTimer = 0;
            }
        }

        public void Draw(Graphics g)
        {
            if (!animations.ContainsKey(CurrentState) || animations[CurrentState].Count == 0) return;

            Image currentImg = animations[CurrentState][currentFrame];

            if (FacingLeft)
            {
                // Tạo một bản sao tạm thời để lật ảnh (hoặc dùng hàm RotateFlip)
                Image flippedImg = (Image)currentImg.Clone();
                flippedImg.RotateFlip(RotateFlipType.RotateNoneFlipX);
                g.DrawImage(flippedImg, Bounds);
                flippedImg.Dispose(); // Giải phóng bộ nhớ sau khi vẽ
            }
            else
            {
                g.DrawImage(currentImg, Bounds);
            }

            g.DrawRectangle(Pens.Red, hurtBox); // Vẽ viền magenta cho Hurtbox

            if (IsHitboxActive)
            {
                // Vẽ viền đỏ cho Hitbox tấn công
                g.DrawRectangle(Pens.Red, ActiveHitbox);
            }
            // Vẽ viền xanh cho thân nhân vật (Hurtbox)
            g.DrawRectangle(Pens.Cyan, Bounds);
        }
    }
}