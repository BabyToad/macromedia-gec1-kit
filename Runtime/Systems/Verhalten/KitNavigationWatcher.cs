using UnityEngine;

namespace Kit
{
    /// <summary>Hidden helper of <see cref="KitNavigation"/>: looks for a changed level twice a second.</summary>
    [AddComponentMenu("")]
    internal sealed class KitNavigationWatcher : MonoBehaviour
    {
        float m_Next;

        void Update()
        {
            if (Time.unscaledTime < m_Next) return;
            m_Next = Time.unscaledTime + KitNavigation.WatchInterval;
            KitNavigation.Watch();
        }

        void OnDestroy() => KitNavigation.WatcherGone(this);
    }
}
