using System;
using UnityEngine;

namespace FBTS
{
    /// <summary>
    /// A short, skippable transition animation. GameManager pauses the current screen while one plays,
    /// calls <see cref="onUpdate"/> every frame with normalized time 0..1, and runs <see cref="onComplete"/>
    /// at the end or when the player skips. Visuals go under <see cref="root"/> and are cleaned up automatically.
    /// </summary>
    public class Cutscene
    {
        public float duration = 3f;
        public Func<float, string> caption;        // normalized time -> caption text
        public Action<Transform> onStart;          // build visuals under root
        public Action<float, float> onUpdate;      // (t 0..1, deltaTime)
        public Action onComplete;                  // game-state change happens here, so skipping is safe
        public Transform root;

        public static float Smooth(float t) => t * t * (3f - 2f * t);

        /// <summary>Remap t from [a,b] to [0,1], clamped.</summary>
        public static float Phase(float t, float a, float b) => Mathf.Clamp01((t - a) / (b - a));
    }

    public static class Fx
    {
        /// <summary>Move/rotate/stretch an existing line sprite made by Draw.Line.</summary>
        public static void SetLine(SpriteRenderer sr, Vector2 a, Vector2 b, float width)
        {
            var tr = sr.transform;
            tr.localPosition = (a + b) / 2f;
            tr.localScale = new Vector3(Mathf.Max(0.001f, Vector2.Distance(a, b)), width, 1f);
            tr.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
        }

        public static void SetAlpha(SpriteRenderer sr, float a)
        {
            var c = sr.color;
            c.a = a;
            sr.color = c;
        }
    }
}
