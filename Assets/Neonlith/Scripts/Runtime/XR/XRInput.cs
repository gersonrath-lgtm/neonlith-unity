using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace Neonlith.XR
{
    /// <summary>
    /// Eingaben der Quest-Controller über das Input System (Gegenstück zu openxr_action_map.tres, Belegung aus
    /// docs/STAND.md): Aktionen entstehen im Code, keine .inputactions-Datei nötig. Linkshänder spiegeln die Hände.
    /// </summary>
    public static class XRInput
    {
        public static InputAction LeftStick, RightStick;
        public static InputAction LeftTrigger, RightTrigger, LeftGrip, RightGrip;
        public static InputAction ButtonA, ButtonB, ButtonX, ButtonY, Menu;
        public static InputAction HeadPos, HeadRot, LeftAimPos, LeftAimRot, RightAimPos, RightAimRot, LeftGripPos, LeftGripRot, RightGripPos, RightGripRot;
        static bool _ready;

        public static bool LeftHanded;

        public static void Init()
        {
            if (_ready) return;
            _ready = true;
            LeftStick = Vec("<XRController>{LeftHand}/thumbstick");
            RightStick = Vec("<XRController>{RightHand}/thumbstick");
            LeftTrigger = Axis("<XRController>{LeftHand}/trigger");
            RightTrigger = Axis("<XRController>{RightHand}/trigger");
            LeftGrip = Axis("<XRController>{LeftHand}/grip");
            RightGrip = Axis("<XRController>{RightHand}/grip");
            ButtonA = Button("<XRController>{RightHand}/primaryButton");
            ButtonB = Button("<XRController>{RightHand}/secondaryButton");
            ButtonX = Button("<XRController>{LeftHand}/primaryButton");
            ButtonY = Button("<XRController>{LeftHand}/secondaryButton");
            Menu = Button("<XRController>{LeftHand}/menu");
            HeadPos = Pos("<XRHMD>/centerEyePosition");
            HeadRot = Rot("<XRHMD>/centerEyeRotation");
            LeftAimPos = Pos("<XRController>{LeftHand}/pointerPosition");
            LeftAimRot = Rot("<XRController>{LeftHand}/pointerRotation");
            RightAimPos = Pos("<XRController>{RightHand}/pointerPosition");
            RightAimRot = Rot("<XRController>{RightHand}/pointerRotation");
            LeftGripPos = Pos("<XRController>{LeftHand}/devicePosition");
            LeftGripRot = Rot("<XRController>{LeftHand}/deviceRotation");
            RightGripPos = Pos("<XRController>{RightHand}/devicePosition");
            RightGripRot = Rot("<XRController>{RightHand}/deviceRotation");
        }

        static InputAction Vec(string path) { var a = new InputAction(path, InputActionType.Value, path, expectedControlType: "Vector2"); a.Enable(); return a; }
        static InputAction Axis(string path) { var a = new InputAction(path, InputActionType.Value, path, expectedControlType: "Axis"); a.Enable(); return a; }
        static InputAction Button(string path) { var a = new InputAction(path, InputActionType.Button, path); a.Enable(); return a; }
        static InputAction Pos(string path) { var a = new InputAction(path, InputActionType.Value, path, expectedControlType: "Vector3"); a.Enable(); return a; }
        static InputAction Rot(string path) { var a = new InputAction(path, InputActionType.Value, path, expectedControlType: "Quaternion"); a.Enable(); return a; }

        // Belegung des Standmodus (Linkshänder: Hände getauscht)
        public static Vector2 WalkStick => (LeftHanded ? RightStick : LeftStick).ReadValue<Vector2>();
        public static Vector2 TurnStick => (LeftHanded ? LeftStick : RightStick).ReadValue<Vector2>();
        public static float WeaponTrigger => (LeftHanded ? LeftTrigger : RightTrigger).ReadValue<float>();
        public static float ForegripHeld => (LeftHanded ? RightGrip : LeftGrip).ReadValue<float>();
        public static float OtherTrigger => (LeftHanded ? RightTrigger : LeftTrigger).ReadValue<float>();   // freie Hand: Bauen
        public static float WeaponGrip => (LeftHanded ? LeftGrip : RightGrip).ReadValue<float>();          // Griff der Waffenhand: Ladeschuss
        public static bool Confirm => ButtonA.IsPressed();          // A: bestätigen, Bau-Modus
        public static bool Back => ButtonB.IsPressed();             // B: zurück
        public static bool StartWave => ButtonX.IsPressed();        // X: Welle starten
        public static bool Pause => Menu.IsPressed();

        /// <summary>Vibration der Waffenhand (0..1, Sekunden).</summary>
        public static void Haptic(float amplitude, float seconds, bool weaponHand = true)
        {
            var node = (weaponHand != LeftHanded) ? XRNode.RightHand : XRNode.LeftHand;
            var dev = InputDevices.GetDeviceAtXRNode(node);
            if (dev.isValid && dev.TryGetHapticCapabilities(out var cap) && cap.supportsImpulse)
                dev.SendHapticImpulse(0, Mathf.Clamp01(amplitude), seconds);
        }
    }
}
