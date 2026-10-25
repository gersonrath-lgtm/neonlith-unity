using System.Collections.Generic;
using Neonlith.Data;
using Neonlith.XR;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Neonlith.Game
{
    /// <summary>
    /// Ablauf einer Mission (Gegenstück zu Game + GameState + WaveManager in Godot, auf den ersten Stand reduziert):
    /// Funk zu Beginn, Countdown, Wellen aus campaign.json (Gruppen mit Verzögerung und Abstand, Wege reihum),
    /// Leben am Kern, Sieg nach der letzten Welle, Niederlage bei null Leben. Spielzeit läuft getrennt von der
    /// Bildzeit (Pause hält sie an). X startet die nächste Welle früher, A startet nach Sieg oder Niederlage neu.
    /// </summary>
    public sealed class GameSession : MonoBehaviour
    {
        public const int EnemyLayer = 8;
        public enum Phase { Intro, Build, Wave, Victory, Defeat }

        public MissionData mission;
        public Phase phase = Phase.Intro;
        public int lives;
        public int wave = -1;          // Index der laufenden oder nächsten Welle
        public float countdown;
        public int kills, essence;
        public string message = "";
        public float messageUntil;
        public bool paused;
        public readonly List<Enemy> enemies = new List<Enemy>();
        public float DeltaTime { get; private set; }

        struct Spawn { public float time; public string type; public int path; }
        readonly List<Spawn> _queue = new List<Spawn>();
        float _waveTime;
        bool _xLatch, _aLatch, _menuLatch;
        Transform _enemyRoot;
        AudioSource _music, _sfx;

        public static GameSession Create(MissionData m)
        {
            var go = new GameObject("Session");
            var s = go.AddComponent<GameSession>();
            s.mission = m;
            s.lives = m.lives;
            s._enemyRoot = new GameObject("Gegner").transform;
            s._music = go.AddComponent<AudioSource>();
            s._music.loop = true;
            s._music.volume = 0.5f;
            s._sfx = go.AddComponent<AudioSource>();
            s._sfx.spatialBlend = 0f;
            s.PlayMusic("theme_game");
            s.Say(m.commsStart.Length > 0 ? m.commsStart : $"Mission {m.number}: {m.name}", 6f);
            s.countdown = 8f;
            return s;
        }

        void Update()
        {
            Buttons();
            DeltaTime = paused ? 0f : Time.deltaTime;
            if (paused) return;
            switch (phase)
            {
                case Phase.Intro:
                case Phase.Build:
                    countdown -= DeltaTime;
                    if (countdown <= 0f) StartWave();
                    break;
                case Phase.Wave:
                    StepWave();
                    break;
            }
        }

        void Buttons()
        {
            var x = XRInput.StartWave;
            if (x && !_xLatch && (phase == Phase.Build || phase == Phase.Intro)) { countdown = 0f; }
            _xLatch = x;
            var a = XRInput.Confirm;
            if (a && !_aLatch && (phase == Phase.Victory || phase == Phase.Defeat)) Restart();
            _aLatch = a;
            var mnu = XRInput.Pause;
            if (mnu && !_menuLatch && phase != Phase.Victory && phase != Phase.Defeat)
            {
                paused = !paused;
                Say(paused ? "Pause (Menü-Taste: weiter)" : "Weiter", 2f);
            }
            _menuLatch = mnu;
        }

        void StartWave()
        {
            wave++;
            if (wave >= mission.waves.Count) { Win(); return; }
            var w = mission.waves[wave];
            _queue.Clear();
            _waveTime = 0f;
            var paths = Mathf.Max(mission.paths.Count, 1);
            for (var gi = 0; gi < w.groups.Count; gi++)
            {
                var g = w.groups[gi];
                var boss = Balance.Enemy(g.type).boss;
                for (var i = 0; i < g.count; i++)
                {
                    var pid = 0;
                    if (paths > 1 && !boss) pid = g.path >= 0 ? g.path % paths : (i + gi) % paths;
                    _queue.Add(new Spawn { time = g.delay + g.interval * i, type = g.type, path = pid });
                }
            }
            _queue.Sort((p, q) => p.time.CompareTo(q.time));
            phase = Phase.Wave;
            Say($"Welle {wave + 1}/{mission.waves.Count}: {w.title}", 3f);
            Sfx("wave_start", 0.8f);
            if (w.boss) PlayMusic("theme_boss");
        }

        void StepWave()
        {
            _waveTime += DeltaTime;
            while (_queue.Count > 0 && _queue[0].time <= _waveTime)
            {
                var s = _queue[0];
                _queue.RemoveAt(0);
                var t = Balance.Enemy(s.type);
                var x = wave < mission.hpIndex.Length ? mission.hpIndex[wave] : wave;
                var f = wave < mission.hpWave.Length ? mission.hpWave[wave] : 1f;
                var path = mission.paths[Mathf.Clamp(s.path, 0, mission.paths.Count - 1)];
                enemies.Add(Enemy.Spawn(this, t, path, s.path, Balance.EnemyHp(t, x, f), _enemyRoot));
            }
            if (_queue.Count == 0 && enemies.Count == 0) WaveComplete();
        }

        void WaveComplete()
        {
            var w = mission.waves[wave];
            essence += w.reward;
            Sfx("wave_clear", 0.7f);
            if (wave + 1 >= mission.waves.Count) { Win(); return; }
            phase = Phase.Build;
            countdown = Balance.BuildTime;
            Say($"Welle {wave + 1} geschafft. Nächste in {Balance.BuildTime:0} s (X: sofort)", 4f);
            if (w.boss) PlayMusic("theme_game");
        }

        public void EnemyKilled(Enemy e)
        {
            if (!e.alive) return;
            kills++;
            essence += e.type.bounty;
            enemies.Remove(e);
            Sfx(e.type.boss ? "boss_down" : (e.type.size >= 1.3f ? "enemy_die_big" : "enemy_die"), 0.5f, e.transform.position);
            e.Despawn();
        }

        public void EnemyReached(Enemy e)
        {
            if (!e.alive) return;
            enemies.Remove(e);
            lives = Mathf.Max(0, lives - e.type.damage);
            Sfx("life_lost", 0.8f);
            XRInput.Haptic(0.4f, 0.12f);
            e.Despawn();
            if (lives <= 0) Lose();
        }

        void Win()
        {
            phase = Phase.Victory;
            Say((mission.commsEnd.Length > 0 ? mission.commsEnd + "\n" : "") + "Mission geschafft. A: neu starten", 999f);
            Sfx("victory", 0.9f);
            PlayMusic("theme_menu");
        }

        void Lose()
        {
            phase = Phase.Defeat;
            foreach (var e in enemies.ToArray()) e.Despawn();
            enemies.Clear();
            Say("Der Kern ist gefallen. A: neu starten", 999f);
            Sfx("defeat", 0.9f);
            PlayMusic("theme_menu");
        }

        void Restart()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void Say(string text, float seconds)
        {
            message = text;
            messageUntil = Time.time + seconds;
        }

        public string MessageNow => Time.time < messageUntil ? message : "";

        /// <summary>Klang aus Resources/Audio/SFX (Kopie von assets/audio/sfx), 2D oder an einem Ort.</summary>
        public void Sfx(string name, float volume, Vector3? at = null)
        {
            var clip = Resources.Load<AudioClip>("Audio/SFX/" + name);
            if (clip == null) return;
            if (at.HasValue) AudioSource.PlayClipAtPoint(clip, at.Value, volume);
            else _sfx.PlayOneShot(clip, volume);
        }

        void PlayMusic(string name)
        {
            var clip = Resources.Load<AudioClip>("Audio/Music/" + name);
            if (clip == null || _music.clip == clip) return;
            _music.clip = clip;
            _music.Play();
        }
    }
}
