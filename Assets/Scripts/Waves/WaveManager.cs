using System;
using UnityEngine;

namespace LastFight
{
    [Serializable]
    public class WaveDefinition
    {
        [Min(1)] public int enemyCount = 3;
    }

    /// <summary>
    /// Управление волнами: 3 -> 5 -> 7 врагов. Следующая волна стартует только когда
    /// все враги предыдущей уничтожены. После последней волны объявляется победа.
    /// Работает на таймерах в Update (без корутин) и замирает на паузе.
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        private enum Phase { Waiting, Spawning, Fighting, Finished }

        [Header("Ссылки")]
        [SerializeField] private EnemyAI enemyPrefab;
        [SerializeField] private PlayerHealth player;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private Transform enemiesRoot;

        [Header("Волны")]
        [SerializeField] private WaveDefinition[] waves =
        {
            new WaveDefinition { enemyCount = 3 },
            new WaveDefinition { enemyCount = 5 },
            new WaveDefinition { enemyCount = 7 }
        };

        [Header("Тайминги и спавн")]
        [SerializeField, Min(0f)] private float firstWaveDelay = 1.5f;
        [SerializeField, Min(0f)] private float delayBetweenWaves = 3f;
        [SerializeField, Min(0f)] private float spawnInterval = 0.35f;
        [SerializeField, Min(0f)] private float spawnJitter = 0.5f;
        [Tooltip("Слои, на которых нельзя появляться врагу (стены, враги, игрок)")]
        [SerializeField] private LayerMask spawnBlockMask;

        private Phase phase = Phase.Waiting;
        private int waveIndex = -1;
        private int spawnedInWave;
        private int aliveEnemies;
        private float timer;
        private int spawnCursor;
        private bool configured;

        public int CurrentWave => waveIndex + 1; // 1..N, 0 до старта
        public int TotalWaves => waves != null ? waves.Length : 0;
        public int AliveEnemies => aliveEnemies;

        /// <summary>(номер волны с 1, всего волн)</summary>
        public event Action<int, int> WaveStarted;

        private void Start()
        {
            configured = enemyPrefab != null && player != null && gameManager != null
                         && spawnPoints != null && spawnPoints.Length > 0
                         && waves != null && waves.Length > 0;

            if (!configured)
            {
                Debug.LogError("WaveManager: не все ссылки назначены (enemyPrefab / player / gameManager / spawnPoints / waves).", this);
                enabled = false;
                return;
            }

            timer = firstWaveDelay;
        }

        private void Update()
        {
            if (!configured || !GameManager.IsPlaying) return;

            switch (phase)
            {
                case Phase.Waiting:
                    timer -= Time.deltaTime;
                    if (timer <= 0f) StartNextWave();
                    break;

                case Phase.Spawning:
                    timer -= Time.deltaTime;
                    if (timer <= 0f)
                    {
                        SpawnEnemy();
                        spawnedInWave++;
                        timer = spawnInterval;
                        if (spawnedInWave >= waves[waveIndex].enemyCount) phase = Phase.Fighting;
                    }
                    break;

                case Phase.Fighting:
                    if (aliveEnemies <= 0) FinishWave();
                    break;
            }
        }

        private void StartNextWave()
        {
            waveIndex++;
            spawnedInWave = 0;
            timer = 0f;
            phase = Phase.Spawning;
            WaveStarted?.Invoke(CurrentWave, TotalWaves);
        }

        private void FinishWave()
        {
            if (waveIndex >= waves.Length - 1)
            {
                phase = Phase.Finished;
                gameManager.WinGame();
            }
            else
            {
                phase = Phase.Waiting;
                timer = delayBetweenWaves;
            }
        }

        private void SpawnEnemy()
        {
            Vector2 position = FindSpawnPosition();
            EnemyAI enemy = Instantiate(enemyPrefab, position, Quaternion.identity, enemiesRoot);
            enemy.Initialize(player);
            enemy.Health.Died += OnEnemyDied;
            aliveEnemies++;
        }

        private Vector2 FindSpawnPosition()
        {
            Vector2 result = Vector2.zero;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Transform point = spawnPoints[spawnCursor % spawnPoints.Length];
                spawnCursor++;
                result = (Vector2)point.position + UnityEngine.Random.insideUnitCircle * spawnJitter;
                if (Physics2D.OverlapCircle(result, 0.45f, spawnBlockMask) == null) return result;
            }
            return result;
        }

        private void OnEnemyDied(EnemyHealth enemy)
        {
            enemy.Died -= OnEnemyDied;
            aliveEnemies = Mathf.Max(0, aliveEnemies - 1);
            gameManager.AddKill();
        }
    }
}
