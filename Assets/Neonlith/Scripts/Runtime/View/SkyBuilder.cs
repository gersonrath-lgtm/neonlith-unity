using UnityEngine;

namespace Neonlith.View
{
    /// <summary>
    /// Himmel und Licht je Welt (Gegenstück zu WorldTheme.apply_env, NightSky): Skybox-Material
    /// <c>Resources/Materials/Sky_w1..w4</c> (Panorama aus space_sky_w*.png, vom Editor-Menü angelegt), sonst
    /// einfarbig in der Himmelsfarbe. Ein Richtungslicht ohne Schatten, flaches Umgebungslicht, linearer Tiefennebel.
    /// </summary>
    public static class SkyBuilder
    {
        public static Light Apply(WorldTheme t, Transform parent)
        {
            var sky = Resources.Load<Material>("Materials/Sky_" + t.id);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(t.sky, Color.white, 0.25f) * t.ambient;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = t.sky;
            RenderSettings.fogStartDistance = t.fogEnd * Data.Balance.FieldM * 0.35f;
            RenderSettings.fogEndDistance = t.fogEnd * Data.Balance.FieldM;

            var go = new GameObject("Sonne");
            go.transform.SetParent(parent, false);
            go.transform.rotation = Quaternion.Euler(52f, -30f, 0f);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = Color.Lerp(Color.white, t.hi, 0.3f);
            l.intensity = 1.1f;
            l.shadows = LightShadows.None;
            return l;
        }

        /// <summary>Kamera: Himmel aus der Skybox, ohne Skybox-Material die Himmelsfarbe.</summary>
        public static void SetupCamera(Camera cam, WorldTheme t)
        {
            cam.clearFlags = RenderSettings.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            cam.backgroundColor = t.sky;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = Mathf.Max(300f, t.fogEnd * Data.Balance.FieldM * 1.5f);
        }
    }
}
