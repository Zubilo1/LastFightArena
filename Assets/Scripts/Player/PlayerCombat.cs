using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LastFight
{
    /// <summary>
    /// Атака игрока (ЛКМ). Удар мгновенный: за одно нажатие один раз ищем врагов в секторе
    /// перед игроком и наносим каждому урон ровно один раз. Между ударами — перезарядка.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Параметры атаки")]
        [SerializeField, Min(1)] private int damage = 20;
        [SerializeField, Min(0.1f)] private float attackRange = 1.5f;
        [SerializeField, Range(10f, 360f)] private float attackArc = 110f;
        [SerializeField, Min(0.05f)] private float attackCooldown = 0.4f;

        [Header("Слои")]
        [SerializeField] private LayerMask enemyMask;
        [SerializeField] private LayerMask obstacleMask;

        [Header("Визуальный эффект удара")]
        [SerializeField] private SpriteRenderer slashRenderer;
        [SerializeField, Min(0.02f)] private float slashDuration = 0.12f;

        private readonly Collider2D[] buffer = new Collider2D[32];
        private readonly HashSet<EnemyHealth> alreadyHit = new HashSet<EnemyHealth>();
        private PlayerController controller;
        private ContactFilter2D enemyFilter;
        private float nextAttackTime;
        private float slashEndTime;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            if (enemyMask.value == 0) enemyMask = LayerMask.GetMask("Enemy");
            if (obstacleMask.value == 0) obstacleMask = LayerMask.GetMask("Obstacle");

            enemyFilter = new ContactFilter2D();
            enemyFilter.SetLayerMask(enemyMask);
            enemyFilter.useTriggers = false;

            if (slashRenderer != null) slashRenderer.enabled = false;
        }

        private void Update()
        {
            if (slashRenderer != null && slashRenderer.enabled && Time.unscaledTime >= slashEndTime)
                slashRenderer.enabled = false;

            if (!GameManager.IsPlaying) return;

            if (GameInput.AttackPressed && Time.time >= nextAttackTime && !IsPointerOverUi())
                Attack();
        }

        private static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private void Attack()
        {
            nextAttackTime = Time.time + attackCooldown;
            Vector2 origin = transform.position;
            Vector2 aim = controller.AimDirection;

            AudioManager.Play(SoundId.PlayerAttack);
            ShowSlash(aim);

            alreadyHit.Clear();
            int count = Physics2D.OverlapCircle(origin, attackRange, enemyFilter, buffer);
            bool hitAny = false;

            for (int i = 0; i < count; i++)
            {
                Collider2D c = buffer[i];
                if (c == null) continue;

                EnemyHealth enemy = c.GetComponent<EnemyHealth>();
                if (enemy == null || !alreadyHit.Add(enemy)) continue;

                Vector2 center = c.bounds.center;
                Vector2 toEnemy = center - origin;

                // Враг должен быть в секторе перед игроком и не за стеной.
                if (Vector2.Angle(aim, toEnemy) > attackArc * 0.5f) continue;
                if (!PhysicsUtil.HasLineOfSight(origin, center, obstacleMask)) continue;

                enemy.TakeDamage(damage);
                hitAny = true;
            }

            if (hitAny) AudioManager.Play(SoundId.Hit);
        }

        private void ShowSlash(Vector2 aim)
        {
            if (slashRenderer == null) return;
            float angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            slashRenderer.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            slashRenderer.enabled = true;
            slashEndTime = Time.unscaledTime + slashDuration;
        }
    }
}
