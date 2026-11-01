using UnityEngine;

namespace LastFight
{
    /// <summary>Кратковременная подсветка спрайта при получении урона. Без корутин — таймер в Update.</summary>
    public class HitFlash : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer target;
        [SerializeField] private Color flashColor = new Color(1f, 0.3f, 0.3f, 1f);
        [SerializeField, Min(0.01f)] private float duration = 0.1f;

        private Color baseColor = Color.white;
        private bool flashing;
        private float endTime;

        private void Awake()
        {
            if (target == null) target = GetComponent<SpriteRenderer>();
            if (target != null) baseColor = target.color;
        }

        public void Flash()
        {
            if (target == null) return;
            flashing = true;
            endTime = Time.unscaledTime + duration;
            target.color = flashColor;
        }

        /// <summary>Меняет «обычный» цвет спрайта (например, серый после смерти).</summary>
        public void SetTint(Color color)
        {
            baseColor = color;
            flashing = false;
            if (target != null) target.color = color;
        }

        private void Update()
        {
            if (flashing && Time.unscaledTime >= endTime)
            {
                flashing = false;
                if (target != null) target.color = baseColor;
            }
        }
    }
}
