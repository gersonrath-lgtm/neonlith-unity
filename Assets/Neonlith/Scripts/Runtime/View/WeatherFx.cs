using System.Collections.Generic;
using UnityEngine;

namespace Neonlith.View
{
    /// <summary>Wetterwerte (Weather.PRESETS in scripts/view3d/weather.gd): nur Stärken, die Darstellung macht WeatherFx.</summary>
    public sealed class WeatherPreset
    {
        public string id, name, amb = "";
        public float rain, wet, puddles, wind, fog = 1f, mist, cover, moon = 1f, halo = 1f;
        public int leaves, lightning;
    }

    /// <summary>
    /// Wetter je Mission (Weather + WeatherFx in Godot, Klarbild-Regeln: Partikel begrenzt, keine Nachbearbeitung):
    /// Regen und Laub als Partikel um den Spieler, Bodennebel über die Nebelweite, Wetterleuchten und Gewitter als
    /// kurze Aufheller des Richtungslichts mit Donner (nah oder fern), Klangkulisse (Regen leicht oder stark, Wind).
    /// Die Werte der Holo-Nacht-Wetter (Missions.weather: holo, holo_wind, holo_niesel, holo_nebel, holo_regen,
    /// holo_gewitter, holo_sturm, holo_nachregen) stehen 1:1 in <see cref="Presets"/>.
    /// </summary>
    public sealed class WeatherFx : MonoBehaviour
    {
        public static readonly Dictionary<string, WeatherPreset> Presets = new Dictionary<string, WeatherPreset>
        {
            ["holo"] = new WeatherPreset { id = "holo", name = "Holofeld", rain = 0f, wet = 0.3f, puddles = 0f, leaves = 0, wind = 0f, fog = 1.25f, mist = 0f, lightning = 0, cover = 0.15f, moon = 1f, halo = 1f, amb = "" },
            ["holo_wind"] = new WeatherPreset { id = "holo_wind", name = "Wind", rain = 0f, wet = 0.3f, leaves = 260, wind = 1f, fog = 1.15f, mist = 0f, lightning = 0, cover = 0.2f, moon = 1f, amb = "wind" },
            ["holo_niesel"] = new WeatherPreset { id = "holo_niesel", name = "Niesel", rain = 0.35f, wet = 0.7f, puddles = 0.2f, wind = 0.3f, fog = 1f, mist = 0.15f, cover = 0.5f, moon = 0.7f, halo = 1.3f, amb = "rain_light" },
            ["holo_nebel"] = new WeatherPreset { id = "holo_nebel", name = "Nebel", rain = 0f, wet = 0.45f, puddles = 0.1f, wind = 0.08f, fog = 0.6f, mist = 0.75f, cover = 0.5f, moon = 0.5f, halo = 2f, amb = "" },
            ["holo_regen"] = new WeatherPreset { id = "holo_regen", name = "Regen", rain = 1f, wet = 1f, puddles = 0.85f, wind = 0.45f, fog = 0.9f, mist = 0.2f, lightning = 1, cover = 0.8f, moon = 0.2f, halo = 1.5f, amb = "rain_heavy" },
            ["holo_gewitter"] = new WeatherPreset { id = "holo_gewitter", name = "Gewitter", rain = 0.55f, wet = 0.9f, puddles = 0.5f, wind = 0.5f, fog = 0.95f, mist = 0.1f, lightning = 2, cover = 0.9f, moon = 0.1f, halo = 1.4f, amb = "rain_heavy" },
            ["holo_sturm"] = new WeatherPreset { id = "holo_sturm", name = "Sturm", rain = 0.85f, wet = 1f, puddles = 0.7f, leaves = 200, wind = 1f, fog = 0.85f, mist = 0.15f, lightning = 2, cover = 1f, moon = 0f, halo = 1.4f, amb = "rain_heavy" },
            ["holo_nachregen"] = new WeatherPreset { id = "holo_nachregen", name = "Nach dem Regen", rain = 0f, wet = 0.85f, puddles = 0.95f, wind = 0.1f, fog = 0.95f, mist = 0.85f, cover = 0.3f, moon = 1f, halo = 1.4f, amb = "" },
        };

        public WeatherPreset preset;
        Transform _head;
        Light _sun;
        float _sunBase, _flash, _nextBolt;
        AudioSource _amb;
        ParticleSystem _rain, _leaves;

        public static WeatherFx Create(string id, Transform head, Light sun, WorldTheme theme)
        {
            var go = new GameObject("Wetter");
            var w = go.AddComponent<WeatherFx>();
            w.preset = Presets.TryGetValue(id, out var p) ? p : Presets["holo"];
            w._head = head; w._sun = sun;
            w._sunBase = sun != null ? sun.intensity : 1f;
            w.Apply(theme);
            return w;
        }

        void Apply(WorldTheme theme)
        {
            // Sichtweite: fog < 1 dichter; Bodennebel senkt sie weiter
            var k = preset.fog * (1f - 0.35f * preset.mist);
            RenderSettings.fogEndDistance = theme.fogEnd * Data.Balance.FieldM * k;
            RenderSettings.fogStartDistance = RenderSettings.fogEndDistance * 0.3f;
            if (_sun != null) _sun.intensity = _sunBase * Mathf.Lerp(1f, 0.75f, preset.cover);
            if (preset.rain > 0f) _rain = Particles("Regen", Mathf.RoundToInt(900f * preset.rain), new Color(0.75f, 0.85f, 1f, 0.55f), new Vector3(0.012f, 0.25f, 0.012f), -9f, preset.wind, 1.6f, theme);
            if (preset.leaves > 0) _leaves = Particles("Laub", preset.leaves, Color.Lerp(theme.neon, Color.black, 0.5f), new Vector3(0.08f, 0.012f, 0.06f), -1.2f, preset.wind, 6f, theme);
            var ambName = preset.amb == "rain_light" ? "amb_rain_light" : preset.amb == "rain_heavy" ? "amb_rain_heavy" : preset.amb == "wind" ? "amb_mars_wind" : "";
            if (ambName.Length > 0)
            {
                var clip = Resources.Load<AudioClip>("Audio/SFX/" + ambName);
                if (clip != null)
                {
                    _amb = gameObject.AddComponent<AudioSource>();
                    _amb.clip = clip; _amb.loop = true; _amb.volume = 0.35f; _amb.spatialBlend = 0f; _amb.Play();
                }
            }
            _nextBolt = Random.Range(4f, 9f);
        }

        /// <summary>Partikel in einem Kasten über dem Spieler (begrenzt, unbeleuchtet, ohne Kollision).</summary>
        ParticleSystem Particles(string name, int count, Color col, Vector3 size, float fall, float wind, float life, WorldTheme theme)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true; main.startLifetime = life; main.startSpeed = 0f; main.maxParticles = count;
            main.startSize3D = true; main.startSizeX = size.x; main.startSizeY = size.y; main.startSizeZ = size.z;
            main.startColor = col; main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = 0f;
            var em = ps.emission; em.rateOverTime = count / life;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(24f, 0.5f, 24f); sh.position = Vector3.up * 9f;
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(wind * 2.5f, wind * 4f); vel.y = new ParticleSystem.MinMaxCurve(fall, fall * 0.8f); vel.z = new ParticleSystem.MinMaxCurve(-wind, wind);
            if (name == "Laub") { var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-3f, 3f); }
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = name == "Regen" ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            r.lengthScale = 1f; r.velocityScale = 0.04f;
            r.sharedMaterial = NeonMaterials.Neon("weather_" + name, col);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        void Update()
        {
            if (_head != null) transform.position = new Vector3(_head.position.x, _head.position.y, _head.position.z);
            if (preset.lightning > 0 && _sun != null)
            {
                _nextBolt -= Time.deltaTime;
                if (_nextBolt <= 0f)
                {
                    _nextBolt = preset.lightning == 2 ? Random.Range(5f, 12f) : Random.Range(12f, 25f);
                    _flash = preset.lightning == 2 ? 1f : 0.45f;
                    var near = preset.lightning == 2 && Random.value < 0.4f;
                    var clip = Resources.Load<AudioClip>("Audio/SFX/" + (near ? "thunder_near" : "thunder_far"));
                    if (clip != null) AudioSource.PlayClipAtPoint(clip, _head != null ? _head.position : Vector3.zero, near ? 0.8f : 0.5f);
                }
                if (_flash > 0f)
                {
                    _flash -= Time.deltaTime * 3f;
                    _sun.intensity = _sunBase * Mathf.Lerp(1f, 0.75f, preset.cover) + Mathf.Max(0f, _flash) * 1.6f * (0.6f + 0.4f * Mathf.PerlinNoise(Time.time * 30f, 0f));
                }
            }
        }
    }
}
