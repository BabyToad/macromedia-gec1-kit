using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Kit
{
    /// <summary>
    /// A way to show a number. „Wert anzeigen“ calls <see cref="Show"/> whenever the number changes.
    /// Write your own by inheriting from this class (a gauge, a sound that gets faster, a colour …).
    /// </summary>
    public abstract class KitValueView : MonoBehaviour
    {
        /// <summary>Show <paramref name="value"/>, which normally lies between <paramref name="min"/> and <paramref name="max"/>.</summary>
        public abstract void Show(float value, float min, float max);

        /// <summary>0 … 1 between min and max (0 if the range is empty).</summary>
        protected static float Fraction(float value, float min, float max) => max > min ? Mathf.Clamp01((value - min) / (max - min)) : 0f;
    }
}
