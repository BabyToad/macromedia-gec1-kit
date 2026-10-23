using UnityEngine;

namespace Kit
{
    /// <summary>Draws the Blende. Created by <see cref="ScreenFade"/> on first use; survives scene loads.</summary>
    [AddComponentMenu("")]   // not for Add Component: ScreenFade makes exactly one
    public sealed class ScreenFadeOverlay : MonoBehaviour
    {
        Texture2D m_White;

        void Awake() { m_White = Texture2D.whiteTexture; }
        void Update() => ScreenFade.Step();

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || ScreenFade.Amount <= 0f) return;
            GUI.depth = -10000;                           // above every other OnGUI and every Canvas
            var c = ScreenFade.Color; c.a = ScreenFade.Amount;
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), m_White);
            GUI.color = old;
        }
    }
}
