using UnityEngine;

namespace LastFight
{
    /// <summary>
    /// ИИ врага: ждёт -> заметил игрока (дистанция + прямая видимость) -> преследует ->
    /// подошёл на дистанцию атаки -> бьёт с заданной периодичностью (не каждый кадр).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(EnemyHealth))]
    public class EnemyAI : MonoBehaviour
    {
        private enum State { Idle, Chase, Attack }

        [Header("Характеристики (меняйте в Inspector)")]
        [SerializeField, Min(0f)] private float moveSpeed = 2.5f;
        [SerializeField, Min(0f)] private float detectionRange = 9f;
        [SerializeField, Min(0.1f)] private float attackRange = 1.1f;
        [SerializeField, Min(0)] private int damage = 8;
        [Tooltip("Сколько ударов в секунду")]
        [SerializeField, Min(0.05f)] private float attacksPerSecond = 1f;
        [Tooltip("Пауза перед первым ударом, когда враг только подошёл")]
        [SerializeField, Min(0f)] private float firstAttackDelay = 0.4f;

        [Header("Окружение")]
        [SerializeField] private LayerMask obstacleMask;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField, Min(0.1f)] private float avoidDistance = 0.8f;

        private Rigidbody2D rb;
        private CircleCollider2D body;
        private EnemyHealth health;
        private PlayerHealth target;
        private State state = State.Idle;
        private float nextAttackTime;
        private float avoidSide = 1f;

        public EnemyHealth Health => health;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            health = GetComponent<EnemyHealth>();
            body = GetComponent<CircleCollider2D>();
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (obstacleMask.value == 0) obstacleMask = LayerMask.GetMask("Obstacle");
        }

        /// <summary>Вызывается WaveManager при создании врага.</summary>
        public void Initialize(PlayerHealth playerHealth)
        {
            target = playerHealth;
        }

        private void Update()
        {
            if (target == null || target.IsDead || !GameManager.IsPlaying)
            {
                state = State.Idle;
                return;
            }

            Vector2 pos = transform.position;
            Vector2 targetPos = target.transform.position;
            Vector2 toTarget = targetPos - pos;
            float distance = toTarget.magnitude;
            bool visible = PhysicsUtil.HasLineOfSight(pos, targetPos, obstacleMask);

            State previous = state;

            if (state == State.Idle && distance <= detectionRange && visible)
                state = State.Chase; // заметил — дальше преследует всегда

            if (state != State.Idle)
            {
                if (distance <= attackRange && visible)
                {
                    if (previous != State.Attack)
                        nextAttackTime = Mathf.Max(nextAttackTime, Time.time + firstAttackDelay);
                    state = State.Attack;
                    TryAttack();
                }
                else
                {
                    state = State.Chase;
                }
            }

            if (spriteRenderer != null && Mathf.Abs(toTarget.x) > 0.05f)
                spriteRenderer.flipX = toTarget.x < 0f;
        }

        private void FixedUpdate()
        {
            if (state != State.Chase || target == null)
            {
                PhysicsUtil.SetVelocity(rb, Vector2.zero);
                return;
            }

            Vector2 pos = rb.position;
            Vector2 dir = (Vector2)target.transform.position - pos;
            float distance = dir.magnitude;
            if (distance < 0.01f)
            {
                PhysicsUtil.SetVelocity(rb, Vector2.zero);
                return;
            }

            dir /= distance;
            dir = ChooseFreeDirection(pos, dir);
            PhysicsUtil.SetVelocity(rb, dir * moveSpeed);
        }

        private void TryAttack()
        {
            if (Time.time < nextAttackTime) return;
            nextAttackTime = Time.time + 1f / attacksPerSecond;
            target.TakeDamage(damage);
        }

        // Простое обхождение препятствий: если путь прямо закрыт, пробуем повернуть на 45/90/135 градусов.
        private Vector2 ChooseFreeDirection(Vector2 pos, Vector2 desired)
        {
            float radius = (body != null ? body.radius * Mathf.Abs(transform.lossyScale.x) : 0.4f) * 0.9f;

            if (IsFree(pos, radius, desired)) return desired;

            for (int step = 1; step <= 3; step++)
            {
                float angle = 45f * step;

                Vector2 first = Rotate(desired, angle * avoidSide);
                if (IsFree(pos, radius, first)) return first;

                Vector2 second = Rotate(desired, -angle * avoidSide);
                if (IsFree(pos, radius, second))
                {
                    avoidSide = -avoidSide; // запоминаем удачную сторону, чтобы не дёргаться
                    return second;
                }
            }
            return desired;
        }

        private bool IsFree(Vector2 pos, float radius, Vector2 dir)
        {
            return Physics2D.CircleCast(pos, radius, dir, avoidDistance, obstacleMask).collider == null;
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            return Quaternion.Euler(0f, 0f, degrees) * v;
        }
    }
}
