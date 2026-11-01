using System;
using UnityEngine;

namespace LastFight
{
    /// <summary>Здоровье врага: получение урона, полоска над головой, смерть.</summary>
    public class EnemyHealth : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxHealth = 40;

        [Header("Полоска здоровья (необязательно)")]
        [SerializeField] private Transform barFill;
        [SerializeField, Min(0.1f)] private float barWidth = 0.8f;
        [SerializeField] private HitFlash flash;

        private int current;
        private bool isDead;

        public int MaxHealth => maxHealth;
        public int Current => current;
        public bool IsDead => isDead;

        public event Action<EnemyHealth> Died;

        private void Awake()
        {
            current = maxHealth;
            if (flash == null) flash = GetComponent<HitFlash>();
            UpdateBar();
        }

        public void TakeDamage(int amount)
        {
            if (isDead || amount <= 0) return;

            current = Mathf.Max(0, current - amount);
            if (flash != null) flash.Flash();
            UpdateBar();

            if (current == 0) Die();
        }

        private void Die()
        {
            isDead = true;
            AudioManager.Play(SoundId.EnemyDeath);
            Died?.Invoke(this);
            Destroy(gameObject);
        }

        private void UpdateBar()
        {
            if (barFill == null) return;
            float ratio = Mathf.Clamp01(current / (float)maxHealth);

            Vector3 scale = barFill.localScale;
            scale.x = barWidth * ratio;
            barFill.localScale = scale;

            Vector3 pos = barFill.localPosition;
            pos.x = -barWidth * (1f - ratio) * 0.5f; // полоска «убывает» к левому краю
            barFill.localPosition = pos;
        }
    }
}
