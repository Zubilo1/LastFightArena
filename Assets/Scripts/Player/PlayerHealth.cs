using System;
using UnityEngine;

namespace LastFight
{
    /// <summary>Здоровье игрока: получение урона, смерть, события для UI и GameManager.</summary>
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxHealth = 100;
        [SerializeField] private HitFlash flash;
        [SerializeField] private Color deadTint = new Color(0.4f, 0.4f, 0.4f, 1f);

        public int Current { get; private set; }
        public int Max => maxHealth;
        public bool IsDead => Current <= 0;

        /// <summary>(текущее здоровье, максимальное здоровье)</summary>
        public event Action<int, int> HealthChanged;
        public event Action Died;

        private void Awake()
        {
            Current = maxHealth;
            if (flash == null) flash = GetComponent<HitFlash>();
        }

        public void TakeDamage(int amount)
        {
            if (IsDead || amount <= 0) return;

            Current = Mathf.Max(0, Current - amount);
            HealthChanged?.Invoke(Current, maxHealth);

            if (Current == 0)
            {
                if (flash != null) flash.SetTint(deadTint);
                Died?.Invoke();
            }
            else
            {
                if (flash != null) flash.Flash();
                AudioManager.Play(SoundId.PlayerHurt);
            }
        }
    }
}
