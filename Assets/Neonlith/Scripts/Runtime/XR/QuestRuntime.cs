using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.XR;

namespace Neonlith.XR
{
    /// <summary>
    /// Quest-Laufzeit (Gegenstück zu den [xr]-Einstellungen in project.godot, AutoRes und Settings): Auflösung
    /// 1,2-fach, Bildrate 90 Hz über das Meta-OpenXR-Paket (per Reflexion, damit das Projekt auch ohne das Paket
    /// übersetzt), Foveation Stufe 2 von 3 fest (Godot foveation_level = 2, dynamic = false), und AutoRes: der
    /// Darstellungsmaßstab folgt der Bildzeit zwischen 0,7 und 1,0 (docs/PERFORMANCE.md „Auflösung automatisch“).
    /// </summary>
    public sealed class QuestRuntime : MonoBehaviour
    {
        public float targetHz = 90f;
        public float resolutionScale = 1.2f;     // Settings: Standard 1,2 (4.99.65)
        public float foveation = 0.67f;          // Stufe 2 von 3
        public bool autoRes = true;
        float _retry, _avg = 1f / 90f, _check;
        bool _hzDone, _fovDone;

        public static QuestRuntime Create()
        {
            var go = new GameObject("QuestLaufzeit");
            return go.AddComponent<QuestRuntime>();
        }

        void Start()
        {
            XRSettings.eyeTextureResolutionScale = resolutionScale;
            XRSettings.renderViewportScale = 1f;
        }

        void Update()
        {
            _avg = Mathf.Lerp(_avg, Time.unscaledDeltaTime, 0.05f);
            _retry -= Time.unscaledDeltaTime;
            if (_retry <= 0f && (!_hzDone || !_fovDone))
            {
                _retry = 1f;
                var displays = new List<XRDisplaySubsystem>();
                SubsystemManager.GetSubsystems(displays);
                foreach (var d in displays)
                {
                    if (!d.running) continue;
                    if (!_fovDone)
                    {
                        try
                        {
                            d.foveatedRenderingLevel = foveation;
                            d.foveatedRenderingFlags = XRDisplaySubsystem.FoveatedRenderingFlags.None;
                            _fovDone = true;
                            Debug.Log($"Neonlith: Foveation {foveation:0.00} fest");
                        }
                        catch (System.Exception ex) { Debug.LogWarning("Neonlith: Foveation nicht setzbar: " + ex.Message); _fovDone = true; }
                    }
                    if (!_hzDone) _hzDone = RequestRefreshRate(d, targetHz);
                }
            }
            if (autoRes)
            {
                _check -= Time.unscaledDeltaTime;
                if (_check <= 0f)
                {
                    _check = 0.5f;
                    var budget = 1f / targetHz;
                    var s = XRSettings.renderViewportScale;
                    if (_avg > budget * 1.12f) s -= 0.05f;
                    else if (_avg < budget * 0.9f) s += 0.03f;
                    XRSettings.renderViewportScale = Mathf.Clamp(s, 0.7f, 1f);
                }
            }
        }

        /// <summary>Meta OpenXR: XRDisplaySubsystemExtensions.TryRequestDisplayRefreshRate (com.unity.xr.meta-openxr), per Reflexion.</summary>
        static bool RequestRefreshRate(XRDisplaySubsystem d, float hz)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType("UnityEngine.XR.OpenXR.Features.Meta.XRDisplaySubsystemExtensions");
                if (t == null) continue;
                var m = t.GetMethod("TryRequestDisplayRefreshRate", BindingFlags.Public | BindingFlags.Static);
                if (m == null) continue;
                try
                {
                    var ok = m.Invoke(null, new object[] { d, hz });
                    Debug.Log($"Neonlith: Bildrate {hz} Hz angefordert: {ok}");
                    return true;
                }
                catch (System.Exception ex) { Debug.LogWarning("Neonlith: Bildrate nicht setzbar: " + ex.Message); return true; }
            }
            Debug.Log("Neonlith: Meta-OpenXR-Paket nicht gefunden, Bildrate bleibt Standard (72 Hz).");
            return true;
        }
    }
}
