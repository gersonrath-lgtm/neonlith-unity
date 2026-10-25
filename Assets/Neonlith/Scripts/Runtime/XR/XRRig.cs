using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;

namespace Neonlith.XR
{
    /// <summary>
    /// Spieler in der Brille (Gegenstück zu XROrigin3D + XRCamera3D + XRController3D in XRMain und zum Standmodus
    /// XRStand): Ursprung am Boden (Tracking „Floor“), Kamera und beide Hände folgen dem Tracking über
    /// TrackedPoseDriver; Gehen mit dem linken Stick (kopfrelativ, 2,2 m/s), Drehen mit dem rechten in Stufen von 30°,
    /// Begrenzung auf die Scheibe des Standdecks (GunTower.stand_area). Keine künstliche Kameraführung.
    /// </summary>
    public sealed class XRRig : MonoBehaviour
    {
        public const float WalkSpeed = 2.2f;      // XRStand.WALK_SPEED
        public const float TurnSnapDeg = 30f;     // XRStand.TURN_SNAP_DEG
        public const float TurnSmoothDeg = 100f;  // XRStand.TURN_SMOOTH_DEG: stufenlos, Grad je Sekunde bei vollem Ausschlag

        public Camera head;
        public Transform leftHand, rightHand;
        /// <summary>Waffenhand (rechts, Linkshänder links).</summary>
        public Transform WeaponHand => XRInput.LeftHanded ? leftHand : rightHand;
        public Transform OtherHand => XRInput.LeftHanded ? rightHand : leftHand;

        Vector3 _center;
        float _radius = 4f;
        bool _snapLatch;
        bool _oriented;
        Vector3 _lookAt;
        float _originRetry;

        /// <summary>Standplatz verlegen (Halle: Nabe oder Balkon). follow = der Spieler gleitet mit (Nabe fährt unter ihm weg).</summary>
        public void SetCenter(Vector3 center, bool follow)
        {
            if (follow) transform.position += center - _center;
            _center = center;
        }

        public void SetRadius(float r) => _radius = r;

        public static XRRig Create(Vector3 floorCenter, float radius, Vector3 lookAt)
        {
            XRInput.Init();
            var go = new GameObject("XRRig");
            var rig = go.AddComponent<XRRig>();
            rig._center = floorCenter;
            rig._radius = radius;
            rig._lookAt = lookAt;
            go.transform.position = floorCenter;

            var camGo = new GameObject("Kopf");
            camGo.transform.SetParent(go.transform, false);
            camGo.tag = "MainCamera";
            rig.head = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            Track(camGo, XRInput.HeadPos, XRInput.HeadRot);

            rig.leftHand = Hand(go.transform, "HandLinks", XRInput.LeftAimPos, XRInput.LeftAimRot);
            rig.rightHand = Hand(go.transform, "HandRechts", XRInput.RightAimPos, XRInput.RightAimRot);
            return rig;
        }

        static Transform Hand(Transform parent, string name, InputAction pos, InputAction rot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Track(go, pos, rot);
            return go.transform;
        }

        static void Track(GameObject go, InputAction pos, InputAction rot)
        {
            var d = go.AddComponent<TrackedPoseDriver>();
            d.positionInput = new InputActionProperty(pos);
            d.rotationInput = new InputActionProperty(rot);
            d.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            d.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
        }

        void Update()
        {
            EnsureFloorOrigin();
            Orient();
            Walk();
            Turn();
            Clamp();
        }

        /// <summary>Tracking-Ursprung am Boden (Godot: reference_space = Stage): Augenhöhe kommt aus dem Tracking.</summary>
        void EnsureFloorOrigin()
        {
            _originRetry -= Time.deltaTime;
            if (_originRetry > 0f) return;
            _originRetry = 1f;
            var subs = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(subs);
            foreach (var s in subs)
            {
                if (s.GetTrackingOriginMode() != TrackingOriginModeFlags.Floor)
                    s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
            }
        }

        /// <summary>Beim ersten Tracking: Blick auf den Wegbeginn, Füße in der Deckmitte.</summary>
        void Orient()
        {
            if (_oriented) return;
            var local = head.transform.localPosition;
            if (local.sqrMagnitude < 1e-4f) return;
            _oriented = true;
            var want = _lookAt - _center; want.y = 0f;
            var have = head.transform.forward; have.y = 0f;
            if (want.sqrMagnitude > 1e-4f && have.sqrMagnitude > 1e-4f)
                transform.RotateAround(head.transform.position, Vector3.up, Vector3.SignedAngle(have, want, Vector3.up));
            var hp = head.transform.position;
            transform.position += new Vector3(_center.x - hp.x, 0f, _center.z - hp.z);
        }

        void Walk()
        {
            var s = XRInput.WalkStick;
            if (s.sqrMagnitude < 0.04f) return;
            var fwd = head.transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) return;
            fwd.Normalize();
            var right = Vector3.Cross(Vector3.up, fwd);
            transform.position += (fwd * s.y + right * s.x) * (WalkSpeed * Time.deltaTime);
        }

        void Turn()
        {
            var x = XRInput.TurnStick.x;
            if (!Data.Progress.SnapTurn)
            {
                if (Mathf.Abs(x) > 0.15f) transform.RotateAround(head.transform.position, Vector3.up, x * TurnSmoothDeg * Time.deltaTime);
                return;
            }
            if (Mathf.Abs(x) < 0.3f) { _snapLatch = false; return; }
            if (_snapLatch || Mathf.Abs(x) < 0.6f) return;
            _snapLatch = true;
            transform.RotateAround(head.transform.position, Vector3.up, Mathf.Sign(x) * TurnSnapDeg);
        }

        /// <summary>Der Kopf bleibt auf der Scheibe des Decks (GunTower.clamp_stand).</summary>
        void Clamp()
        {
            var hp = head.transform.position;
            var d = new Vector3(hp.x - _center.x, 0f, hp.z - _center.z);
            var l = d.magnitude;
            if (l <= _radius) return;
            transform.position -= d * ((l - _radius) / l);
        }
    }
}
