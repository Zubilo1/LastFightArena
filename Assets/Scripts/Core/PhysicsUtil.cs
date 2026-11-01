using UnityEngine;

namespace LastFight
{
    /// <summary>Мелкие общие помощники для физики (линия видимости, скорость Rigidbody2D).</summary>
    public static class PhysicsUtil
    {
        /// <summary>true, если между точками нет препятствий (стен, ящиков, бочек).</summary>
        public static bool HasLineOfSight(Vector2 from, Vector2 to, LayerMask obstacleMask)
        {
            return Physics2D.Linecast(from, to, obstacleMask).collider == null;
        }

        // В Unity 6 свойство velocity переименовано в linearVelocity.
        public static void SetVelocity(Rigidbody2D rb, Vector2 value)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = value;
#else
            rb.velocity = value;
#endif
        }

        public static Vector2 GetVelocity(Rigidbody2D rb)
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }
    }
}
