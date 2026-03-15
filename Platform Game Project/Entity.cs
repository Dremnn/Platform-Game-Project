using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace Platform_Game_Project
{
    public abstract class Entity
    {
        public Rectangle Bounds;
        public Rectangle hurtBox;
        public int VelocityY = 0;
        public bool IsOnPlatform = false;
        public bool FacingLeft = false;
        public int HP;
        public int MaxHP;
        public bool IsDead => HP <= 0;

        protected Dictionary<string, List<Image>> animations = new Dictionary<string, List<Image>>();
        protected string currentAnimKey = "Idle";
        public int currentFrame = 0;
        protected int frameTimer = 0;
        protected int frameDelay = 6;

        public Entity(int x, int y, int width, int height, int hp, int scale)
        {
            Bounds = new Rectangle(x, y, width * scale, height * scale);
            HP = MaxHP = hp;
        }

        public abstract void Update(int gravity);
        public abstract void Draw(Graphics g);
        protected abstract void LoadAllAnimations();
        public abstract void UpdateHurtbox();

        protected void Animate()
        {
            if (!animations.ContainsKey(currentAnimKey) || animations[currentAnimKey].Count == 0) return;
            frameTimer++;
            if (frameTimer >= frameDelay)
            {
                currentFrame = (currentFrame + 1) % animations[currentAnimKey].Count;
                frameTimer = 0;
            }
        }

        protected void AnimateOnce()
        {
            if (!animations.ContainsKey(currentAnimKey) || animations[currentAnimKey].Count == 0) return;
            frameTimer++;
            if (frameTimer >= frameDelay)
            {
                frameTimer = 0;
                if (currentFrame < animations[currentAnimKey].Count - 1)
                    currentFrame++;
                // Không tăng nữa khi đến frame cuối — dừng lại
            }
        }

        protected bool IsLastFrame()
        {
            if (!animations.ContainsKey(currentAnimKey)) return false;
            return currentFrame == animations[currentAnimKey].Count - 1
                   && frameTimer >= frameDelay - 1;
        }

        protected void TransitionTo(string newKey, int delay)
        {
            if (currentAnimKey == newKey) return;
            currentAnimKey = newKey;
            currentFrame = 0;
            frameTimer = 0;
            frameDelay = delay;
        }

        protected List<Image> LoadFolder(string path)
        {
            List<Image> frames = new List<Image>();
            if (!Directory.Exists(path)) return frames;

            var files = Directory.GetFiles(path, "*.png")
                .OrderBy(f => {
                    string name = Path.GetFileNameWithoutExtension(f);
                    // Lấy số ở cuối tên file, ví dụ "Warrior_Death_10" -> 10
                    var match = System.Text.RegularExpressions.Regex.Match(name, @"\d+$");
                    return match.Success ? int.Parse(match.Value) : 0;
                })
                .ToList();

            foreach (var file in files) frames.Add(Image.FromFile(file));
            return frames;
        }

        protected List<Image> LoadSpritesheet(string filePath, int frameCount, int frameWidth, int frameHeight)
        {
            List<Image> frames = new List<Image>();
            if (!File.Exists(filePath)) return frames;

            using (Image fullSheet = Image.FromFile(filePath))
            {
                for (int i = 0; i < frameCount; i++)
                {
                    Bitmap frame = new Bitmap(frameWidth, frameHeight);
                    using (Graphics g = Graphics.FromImage(frame))
                    {
                        // Cắt phần ảnh tương ứng từ tấm sheet
                        g.DrawImage(fullSheet, new Rectangle(0, 0, frameWidth, frameHeight),
                                    new Rectangle(i * frameWidth, 0, frameWidth, frameHeight),
                                    GraphicsUnit.Pixel);
                    }
                    frames.Add(frame);
                }
            }
            return frames;
        }

        protected void DrawImage(Graphics g, Image img)
        {
            if (FacingLeft)
            {
                Image flippedImg = (Image)img.Clone();
                flippedImg.RotateFlip(RotateFlipType.RotateNoneFlipX);
                g.DrawImage(flippedImg, Bounds);
                flippedImg.Dispose();
            }
            else
            {
                g.DrawImage(img, Bounds);
            }
        }
    }
}