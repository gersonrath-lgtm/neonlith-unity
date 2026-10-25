using UnityEngine;

namespace Neonlith.Game
{
    /// <summary>Etwas, das die Spielerwaffe außer Gegnern treffen kann (Kiste des Nachschubs, Flugalien). onShot(dmg) liefert true, wenn es dabei zerbricht.</summary>
    public sealed class ShotTarget : MonoBehaviour
    {
        public System.Func<float, bool> onShot;
    }
}
