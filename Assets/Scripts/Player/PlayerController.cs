using UnityEngine;

namespace LastFight
{
    /// <summary>Движение игрока (WASD) и прицеливание мышью. Движение через Rigidbody2D, поэтому стены не пропускают.</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float moveSpeed = 5f;
        [SerializeField, Min(1f)] private float acceleration = 50f;
        [SerializeField] private Camera aimCamera;
        [SerializeField] private SpriteRenderer spriteRenderer;

        private Rigidbody2D rb;
        private Vector2 input;

        /// <summary>Нормализованное направление от игрока к курсору.</summary>
        public Vector2 AimDirection { get; private set; } = Vector2.right;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            if (aimCamera == null) aimCamera = Camera.main;
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            if (!GameManager.IsPlaying)
            {
                input = Vector2.zero;
                return;
            }

            input = GameInput.Move;
            UpdateAim();
        }

        private void FixedUpdate()
        {
            Vector2 target = GameManager.IsPlaying ? input * moveSpeed : Vector2.zero;
            Vector2 current = PhysicsUtil.GetVelocity(rb);
            PhysicsUtil.SetVelocity(rb, Vector2.MoveTowards(current, target, acceleration * Time.fixedDeltaTime));
        }

        private void UpdateAim()
        {
            if (aimCamera == null) return;

            Vector2 p = GameInput.PointerPosition;
            Vector3 world = aimCamera.ScreenToWorldPoint(new Vector3(p.x, p.y, Mathf.Abs(aimCamera.transform.position.z)));
            Vector2 dir = (Vector2)world - (Vector2)transform.position;
            if (dir.sqrMagnitude > 0.0001f) AimDirection = dir.normalized;

            if (spriteRenderer != null && Mathf.Abs(AimDirection.x) > 0.05f)
                spriteRenderer.flipX = AimDirection.x < 0f;
        }
    }
}
