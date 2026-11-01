using UnityEngine;

namespace LastFight
{
    /// <summary>Подгоняет размер ортографической камеры, чтобы арена целиком помещалась на экране при любом соотношении сторон.</summary>
    [RequireComponent(typeof(Camera))]
    public class ArenaCamera : MonoBehaviour
    {
        [SerializeField] private Vector2 visibleArea = new Vector2(26f, 16f);

        private Camera cam;
        private float lastAspect = -1f;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
            Fit();
        }

        private void Update()
        {
            if (!Mathf.Approximately(lastAspect, cam.aspect)) Fit();
        }

        private void Fit()
        {
            lastAspect = cam.aspect;
            float byHeight = visibleArea.y * 0.5f;
            float byWidth = visibleArea.x * 0.5f / Mathf.Max(0.01f, cam.aspect);
            cam.orthographicSize = Mathf.Max(byHeight, byWidth);
        }
    }
}
