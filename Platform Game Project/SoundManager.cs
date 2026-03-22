using System;
using System.Collections.Generic;
using System.Media;
using System.IO;

namespace Platform_Game_Project
{
    public class SoundManager
    {
        private Dictionary<string, SoundPlayer> sounds = new Dictionary<string, SoundPlayer>();

        public SoundManager()
        {
            string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Sound");

            // Player
            Load("player_hit", Path.Combine(root, "Player", "hit.wav"));
            Load("player_hurt", Path.Combine(root, "Player", "hurt.wav"));

        }

        private void Load(string key, string path)
        {
            sounds[key] = new SoundPlayer(path);
            sounds[key].Load();
        }

        public void Play(string key)
        {
            sounds[key].Play();
        }
    }
}