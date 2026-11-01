using System.IO;
using System.Linq;
using LastFight;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LastFightEditor
{
    /// <summary>
    /// Меню Last Fight > Build Arena Scene автоматически:
    ///  - создаёт слои Player / Enemy / Obstacle;
    ///  - рисует pixel-art спрайты (PNG в Assets/Art/Generated);
    ///  - создаёт префаб врага;
    ///  - собирает сцену Assets/Scenes/LastFightArena.unity (арена, игрок, менеджеры, UI) и расставляет все ссылки;
    ///  - добавляет сцену в Build Settings.
    /// </summary>
    public static class LastFightArenaSetup
    {
        private const string ArtDir = "Assets/Art/Generated";
        private const string PrefabDir = "Assets/Prefabs";
        private const string SceneDir = "Assets/Scenes";
        private const string ScenePath = SceneDir + "/LastFightArena.unity";
        private const string MenuScenePath = SceneDir + "/MainMenu.unity";
        private const float Ppu = 16f;

        private class SpriteSet
        {
            public Sprite floor, wall, player, enemy, crate, barrel, slash, pixel;
        }

        // -------------------------------------------------------------------------------------
        [MenuItem("Last Fight/Build Arena Scene")]
        public static void BuildArenaScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Last Fight", "Остановите режим Play и повторите.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder("Assets/Art");
            EnsureFolder(ArtDir);
            EnsureFolder(PrefabDir);
            EnsureFolder(SceneDir);

            EnsureLayer("Player");
            EnsureLayer("Enemy");
            EnsureLayer("Obstacle");
            int playerLayer = LayerMask.NameToLayer("Player");
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            int obstacleLayer = LayerMask.NameToLayer("Obstacle");
            if (playerLayer < 0 || enemyLayer < 0 || obstacleLayer < 0)
            {
                EditorUtility.DisplayDialog("Last Fight",
                    "Не удалось создать слои. Создайте вручную слои Player, Enemy, Obstacle (Project Settings > Tags and Layers) и повторите.", "OK");
                return;
            }

            int obstacleMask = 1 << obstacleLayer;
            int enemyMask = 1 << enemyLayer;
            int spawnBlockMask = obstacleMask | enemyMask | (1 << playerLayer);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SpriteSet sprites = CreateSprites();
            EnemyAI enemyPrefab = CreateEnemyPrefab(sprites, enemyLayer, obstacleMask);

            Camera cam = CreateCamera();
            CreateArena(sprites, obstacleLayer);
            Transform[] spawnPoints = CreateSpawnPoints();
            GameObject playerGo = CreatePlayer(sprites, playerLayer, cam, enemyMask, obstacleMask);

            PlayerHealth playerHealth = playerGo.GetComponent<PlayerHealth>();

            GameManager gameManager = new GameObject("GameManager").AddComponent<GameManager>();
            SetRef(gameManager, "player", playerHealth);

            new GameObject("AudioManager").AddComponent<AudioManager>();

            Transform enemiesRoot = new GameObject("Enemies").transform;
            WaveManager waveManager = new GameObject("WaveManager").AddComponent<WaveManager>();
            SetRef(waveManager, "enemyPrefab", enemyPrefab);
            SetRef(waveManager, "player", playerHealth);
            SetRef(waveManager, "gameManager", gameManager);
            SetRef(waveManager, "enemiesRoot", enemiesRoot);
            SetInt(waveManager, "spawnBlockMask", spawnBlockMask);
            SetRefArray(waveManager, "spawnPoints", spawnPoints);
            SetWaves(waveManager, new[] { 3, 5, 7 });

            CreateUi(gameManager, playerHealth, waveManager);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Last Fight: Arena",
                "Сцена создана: " + ScenePath + "\nНажмите Play.\n\nWASD — движение, ЛКМ — атака, ESC — пауза, R — рестарт после конца игры.", "OK");
        }

        // ------------------------------------------------------------------ folders / layers
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static void EnsureLayer(string layerName)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) return;

            var tagManager = new SerializedObject(assets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            if (layers == null) return;

            for (int i = 0; i < layers.arraySize; i++)
                if (layers.GetArrayElementAtIndex(i).stringValue == layerName) return;

            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(slot.stringValue))
                {
                    slot.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    return;
                }
            }
        }

        // ------------------------------------------------------------------ serialized helpers
        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogError("Поле '" + field + "' не найдено в " + target.GetType().Name); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInt(Object target, string field, int value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogError("Поле '" + field + "' не найдено в " + target.GetType().Name); return; }
            p.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRefArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogError("Поле '" + field + "' не найдено в " + target.GetType().Name); return; }
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetWaves(WaveManager manager, int[] counts)
        {
            var so = new SerializedObject(manager);
            SerializedProperty p = so.FindProperty("waves");
            p.arraySize = counts.Length;
            for (int i = 0; i < counts.Length; i++)
                p.GetArrayElementAtIndex(i).FindPropertyRelative("enemyCount").intValue = counts[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddSceneToBuildSettings(string path)
        {
            var list = EditorBuildSettings.scenes.ToList();
            if (!list.Any(sc => sc.path == path)) list.Add(new EditorBuildSettingsScene(path, true));

            // Главное меню всегда первой сценой (индекс 0), чтобы игра стартовала с него.
            int menuIndex = list.FindIndex(sc => sc.path == MenuScenePath);
            if (menuIndex > 0)
            {
                var menu = list[menuIndex];
                list.RemoveAt(menuIndex);
                list.Insert(0, menu);
            }
            EditorBuildSettings.scenes = list.ToArray();
        }

        private static void CreateEventSystem()
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            ObjectFactory.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(es);
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        // ------------------------------------------------------------------ main menu scene
        [MenuItem("Last Fight/Build Main Menu Scene")]
        public static void BuildMainMenuScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Last Fight", "Остановите режим Play и повторите.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(SceneDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.05f, 0.09f, 1f);
            camGo.AddComponent<AudioListener>();

            new GameObject("AudioManager").AddComponent<AudioManager>();

            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            Transform root = canvasGo.transform;

            CreateEventSystem();

            Vector2 mid = new Vector2(0.5f, 0.5f);
            Text title = NewText("Title", root, "LAST FIGHT", 140, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.2f, 1f));
            Place(title.rectTransform, mid, mid, mid, new Vector2(0f, 270f), new Vector2(1400f, 180f));
            Text subtitle = NewText("Subtitle", root, "A R E N A", 80, TextAnchor.MiddleCenter, new Color(0.95f, 0.3f, 0.3f, 1f));
            Place(subtitle.rectTransform, mid, mid, mid, new Vector2(0f, 150f), new Vector2(1000f, 110f));

            Button play = NewButton(root, "PlayButton", "PLAY", mid, mid, new Vector2(0f, -10f), new Vector2(420f, 90f));
            Button quit = NewButton(root, "QuitButton", "QUIT", mid, mid, new Vector2(0f, -120f), new Vector2(420f, 90f));

            Text hint = NewText("ControlsHint", root, "WASD - move     LMB - attack     ESC - pause     R - restart", 32, TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.85f, 1f));
            Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(1600f, 50f));

            var menu = canvasGo.AddComponent<MainMenuController>();
            SetRef(menu, "playButton", play);
            SetRef(menu, "quitButton", quit);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MenuScenePath);
            AddSceneToBuildSettings(MenuScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Last Fight: Arena",
                "Главное меню создано: " + MenuScenePath + "\nОткройте его и нажмите Play. Оно стоит первым в Build Settings.\n\nЕсли игровая сцена ещё не создана или создана давно — запустите Last Fight > Build Arena Scene (чтобы в паузе появилась кнопка MAIN MENU).", "OK");
        }

        // ------------------------------------------------------------------ sprites
        private static SpriteSet CreateSprites()
        {
            var set = new SpriteSet();
            set.floor = SaveSprite("floor", MakeFloor(), 16, 16, Ppu);
            set.wall = SaveSprite("wall", MakeWall(), 16, 16, Ppu);
            set.player = SaveSprite("player", MakePlayer(), 16, 16, Ppu);
            set.enemy = SaveSprite("enemy", MakeEnemy(), 16, 16, Ppu);
            set.crate = SaveSprite("crate", MakeCrate(), 16, 16, Ppu);
            set.barrel = SaveSprite("barrel", MakeBarrel(), 16, 16, Ppu);
            set.slash = SaveSprite("slash", MakeSlash(), 48, 48, Ppu);
            var white = new[] { new Color32(255, 255, 255, 255) };
            set.pixel = SaveSprite("pixel", white, 1, 1, 1f);
            return set;
        }

        private static Sprite SaveSprite(string name, Color32[] pixels, int w, int h, float ppu)
        {
            string assetPath = ArtDir + "/" + name + ".png";
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(Application.dataPath + "/../" + assetPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePixelsPerUnit = ppu;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static Color32 C(int r, int g, int b, int a = 255) => new Color32((byte)r, (byte)g, (byte)b, (byte)a);

        private static void Fill(Color32[] px, int w, int x0, int y0, int x1, int y1, Color32 c)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    px[y * w + x] = c;
        }

        private static void Dot(Color32[] px, int w, int x, int y, Color32 c) => px[y * w + x] = c;

        private static void PixelOutline(Color32[] px, int w, int h, Color32 c)
        {
            var copy = (Color32[])px.Clone();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (px[y * w + x].a != 0) continue;
                    bool near = (x > 0 && px[y * w + x - 1].a != 0) || (x < w - 1 && px[y * w + x + 1].a != 0)
                             || (y > 0 && px[(y - 1) * w + x].a != 0) || (y < h - 1 && px[(y + 1) * w + x].a != 0);
                    if (near) copy[y * w + x] = c;
                }
            System.Array.Copy(copy, px, px.Length);
        }

        private static Color32[] MakeFloor()
        {
            const int n = 16;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int hash = ((x * 73856093) ^ (y * 19349663)) & 255;
                    int v = 60;
                    if (hash < 22) v += 8; else if (hash > 232) v -= 8;
                    px[y * n + x] = C(v, v, v + 10);
                }
            for (int i = 0; i < n; i++) { Dot(px, n, i, 0, C(44, 44, 54)); Dot(px, n, n - 1, i, C(44, 44, 54)); }
            return px;
        }

        private static Color32[] MakeWall()
        {
            const int n = 16;
            var px = new Color32[n * n];
            Fill(px, n, 0, 0, n - 1, n - 1, C(52, 48, 62));
            Color32 mortar = C(26, 24, 32);
            Color32 light = C(74, 70, 86);
            for (int x = 0; x < n; x++) { Dot(px, n, x, 0, mortar); Dot(px, n, x, 8, mortar); Dot(px, n, x, 7, light); Dot(px, n, x, 15, light); }
            for (int y = 0; y < 8; y++) Dot(px, n, 0, y, mortar);
            for (int y = 8; y < 16; y++) Dot(px, n, 8, y, mortar);
            return px;
        }

        private static Color32[] MakePlayer()
        {
            const int n = 16;
            var px = new Color32[n * n];
            Color32 skin = C(235, 190, 150), blue = C(60, 110, 225), darkBlue = C(35, 65, 150), dark = C(40, 40, 70), hair = C(110, 70, 40), eye = C(20, 20, 30);
            Fill(px, n, 5, 1, 6, 3, dark); Fill(px, n, 9, 1, 10, 3, dark);
            Fill(px, n, 4, 4, 11, 9, blue); Fill(px, n, 4, 6, 11, 6, darkBlue);
            Fill(px, n, 3, 5, 3, 8, blue); Fill(px, n, 12, 5, 12, 8, blue);
            Dot(px, n, 3, 4, skin); Dot(px, n, 12, 4, skin);
            Fill(px, n, 5, 10, 10, 13, skin);
            Fill(px, n, 5, 13, 10, 14, hair); Dot(px, n, 4, 13, hair); Dot(px, n, 11, 13, hair);
            Dot(px, n, 6, 11, eye); Dot(px, n, 9, 11, eye);
            PixelOutline(px, n, n, C(15, 15, 25));
            return px;
        }

        private static Color32[] MakeEnemy()
        {
            const int n = 16;
            var px = new Color32[n * n];
            Color32 green = C(120, 185, 80), armor = C(175, 60, 60), darkArmor = C(110, 35, 35), dark = C(60, 40, 30), eye = C(255, 230, 60), horn = C(210, 210, 200);
            Fill(px, n, 5, 1, 6, 3, dark); Fill(px, n, 9, 1, 10, 3, dark);
            Fill(px, n, 3, 4, 12, 9, armor); Fill(px, n, 3, 6, 12, 6, darkArmor);
            Fill(px, n, 2, 5, 2, 8, green); Fill(px, n, 13, 5, 13, 8, green);
            Fill(px, n, 4, 10, 11, 13, green);
            Dot(px, n, 4, 14, horn); Dot(px, n, 11, 14, horn);
            Dot(px, n, 6, 12, eye); Dot(px, n, 9, 12, eye);
            Fill(px, n, 6, 10, 9, 10, C(70, 110, 45));
            PixelOutline(px, n, n, C(25, 15, 15));
            return px;
        }

        private static Color32[] MakeCrate()
        {
            const int n = 16;
            var px = new Color32[n * n];
            Color32 wood = C(160, 108, 55), edge = C(92, 56, 26), light = C(190, 135, 75);
            Fill(px, n, 0, 0, n - 1, n - 1, wood);
            for (int i = 0; i < n; i++) { Dot(px, n, i, 0, edge); Dot(px, n, i, n - 1, edge); Dot(px, n, 0, i, edge); Dot(px, n, n - 1, i, edge); Dot(px, n, i, 1, edge); Dot(px, n, i, n - 2, edge); Dot(px, n, 1, i, edge); Dot(px, n, n - 2, i, edge); }
            for (int i = 2; i < n - 2; i++) { Dot(px, n, i, i, edge); Dot(px, n, i, n - 1 - i, edge); }
            Fill(px, n, 2, 7, 4, 7, light);
            return px;
        }

        private static Color32[] MakeBarrel()
        {
            const int n = 16;
            var px = new Color32[n * n];
            Color32 wood = C(130, 85, 45), edge = C(70, 42, 20), band = C(60, 60, 70), light = C(165, 115, 65);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x - 7.5f, dy = y - 7.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > 7.6f) continue;
                    px[y * n + x] = d > 6.4f ? edge : wood;
                }
            for (int x = 1; x < n - 1; x++)
            {
                foreach (int y in new[] { 4, 5, 10, 11 })
                    if (px[y * n + x].a != 0) px[y * n + x] = band;
            }
            Fill(px, n, 5, 7, 6, 8, light);
            return px;
        }

        private static Color32[] MakeSlash()
        {
            const int n = 48;
            var px = new Color32[n * n];
            float cx = 23.5f, cy = 23.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Abs(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
                    if (r >= 17f && r <= 23.5f && ang <= 50f)
                    {
                        int alpha = (int)Mathf.Lerp(230f, 90f, ang / 50f);
                        px[y * n + x] = C(225, 240, 255, alpha);
                    }
                }
            return px;
        }

        // ------------------------------------------------------------------ prefab / scene objects
        private static EnemyAI CreateEnemyPrefab(SpriteSet sprites, int enemyLayer, int obstacleMask)
        {
            var go = new GameObject("Enemy") { layer = enemyLayer };

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprites.enemy;
            sr.sortingOrder = 5;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.4f;

            var flash = go.AddComponent<HitFlash>();
            var health = go.AddComponent<EnemyHealth>();
            var ai = go.AddComponent<EnemyAI>();

            // Полоска здоровья над врагом.
            var back = new GameObject("HealthBarBack");
            back.transform.SetParent(go.transform, false);
            back.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            back.transform.localScale = new Vector3(0.9f, 0.14f, 1f);
            var backSr = back.AddComponent<SpriteRenderer>();
            backSr.sprite = sprites.pixel; backSr.color = new Color(0f, 0f, 0f, 0.8f); backSr.sortingOrder = 9;

            var fill = new GameObject("HealthBarFill");
            fill.transform.SetParent(go.transform, false);
            fill.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            fill.transform.localScale = new Vector3(0.8f, 0.08f, 1f);
            var fillSr = fill.AddComponent<SpriteRenderer>();
            fillSr.sprite = sprites.pixel; fillSr.color = new Color(0.9f, 0.2f, 0.2f, 1f); fillSr.sortingOrder = 10;

            SetRef(flash, "target", sr);
            SetRef(health, "barFill", fill.transform);
            SetRef(health, "flash", flash);
            SetRef(ai, "spriteRenderer", sr);
            SetInt(ai, "obstacleMask", obstacleMask);

            string path = PrefabDir + "/Enemy.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return saved.GetComponent<EnemyAI>();
        }

        private static Camera CreateCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = new Vector3(0f, 0f, -10f);
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.04f, 0.06f, 1f);
            go.AddComponent<AudioListener>();
            go.AddComponent<ArenaCamera>();
            return cam;
        }

        private static void CreateArena(SpriteSet sprites, int obstacleLayer)
        {
            var arena = new GameObject("Arena").transform;

            var floor = new GameObject("Floor");
            floor.transform.SetParent(arena, false);
            var floorSr = floor.AddComponent<SpriteRenderer>();
            floorSr.sprite = sprites.floor;
            floorSr.drawMode = SpriteDrawMode.Tiled;
            floorSr.size = new Vector2(24f, 14f);
            floorSr.sortingOrder = 0;

            CreateWall("Wall_Top", arena, sprites.wall, new Vector2(0f, 7.5f), new Vector2(26f, 1f), obstacleLayer);
            CreateWall("Wall_Bottom", arena, sprites.wall, new Vector2(0f, -7.5f), new Vector2(26f, 1f), obstacleLayer);
            CreateWall("Wall_Left", arena, sprites.wall, new Vector2(-12.5f, 0f), new Vector2(1f, 16f), obstacleLayer);
            CreateWall("Wall_Right", arena, sprites.wall, new Vector2(12.5f, 0f), new Vector2(1f, 16f), obstacleLayer);

            var props = new GameObject("Props").transform;
            props.SetParent(arena, false);
            Vector2[] crates = { new Vector2(-6, 3), new Vector2(-5, 3), new Vector2(-6, 2), new Vector2(6, -3), new Vector2(5, -3), new Vector2(6, -2), new Vector2(0, 1), new Vector2(1, 1) };
            Vector2[] barrels = { new Vector2(-7, -4.5f), new Vector2(8, 4), new Vector2(2, 4), new Vector2(-3, -1), new Vector2(9, -5) };
            for (int i = 0; i < crates.Length; i++) CreateProp("Crate_" + (i + 1), props, sprites.crate, crates[i], false, obstacleLayer);
            for (int i = 0; i < barrels.Length; i++) CreateProp("Barrel_" + (i + 1), props, sprites.barrel, barrels[i], true, obstacleLayer);
        }

        private static void CreateWall(string name, Transform parent, Sprite sprite, Vector2 pos, Vector2 size, int layer)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = size;
            sr.sortingOrder = 2;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = size;
        }

        private static void CreateProp(string name, Transform parent, Sprite sprite, Vector2 pos, bool round, int layer)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 3;
            if (round) go.AddComponent<CircleCollider2D>().radius = 0.45f;
            else go.AddComponent<BoxCollider2D>().size = Vector2.one;
        }

        private static Transform[] CreateSpawnPoints()
        {
            var root = new GameObject("SpawnPoints").transform;
            Vector2[] points = { new Vector2(-10, 5), new Vector2(10, 5), new Vector2(-10, -1), new Vector2(10, -1), new Vector2(0, 5.5f) };
            var result = new Transform[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                var p = new GameObject("SpawnPoint_" + (i + 1)).transform;
                p.SetParent(root, false);
                p.position = points[i];
                result[i] = p;
            }
            return result;
        }

        private static GameObject CreatePlayer(SpriteSet sprites, int playerLayer, Camera cam, int enemyMask, int obstacleMask)
        {
            var go = new GameObject("Player") { layer = playerLayer };
            go.transform.position = new Vector3(0f, -4.5f, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprites.player;
            sr.sortingOrder = 6;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            go.AddComponent<CircleCollider2D>().radius = 0.4f;

            var flash = go.AddComponent<HitFlash>();
            var health = go.AddComponent<PlayerHealth>();
            var controller = go.AddComponent<PlayerController>();
            var combat = go.AddComponent<PlayerCombat>();

            var slash = new GameObject("SlashEffect");
            slash.transform.SetParent(go.transform, false);
            var slashSr = slash.AddComponent<SpriteRenderer>();
            slashSr.sprite = sprites.slash;
            slashSr.sortingOrder = 7;
            slashSr.enabled = false;

            SetRef(flash, "target", sr);
            SetRef(health, "flash", flash);
            SetRef(controller, "aimCamera", cam);
            SetRef(controller, "spriteRenderer", sr);
            SetRef(combat, "slashRenderer", slashSr);
            SetInt(combat, "enemyMask", enemyMask);
            SetInt(combat, "obstacleMask", obstacleMask);
            return go;
        }

        // ------------------------------------------------------------------ UI
        private static Font UiFont()
        {
#if UNITY_2022_2_OR_NEWER
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void Place(RectTransform r, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            r.anchorMin = anchorMin; r.anchorMax = anchorMax; r.pivot = pivot;
            r.anchoredPosition = pos; r.sizeDelta = size;
        }

        private static void Stretch(RectTransform r, float inset)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(inset, inset); r.offsetMax = new Vector2(-inset, -inset);
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            RectTransform rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static Text NewText(string name, Transform parent, string content, int size, TextAnchor align, Color color)
        {
            RectTransform rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = UiFont();
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = align;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = content;
            var o = rt.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.85f);
            o.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        private static Button NewButton(Transform parent, string name, string label, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            Image img = NewImage(name, parent, Color.white);
            Place(img.rectTransform, anchor, anchor, pivot, pos, size);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            ColorBlock cb = btn.colors;
            cb.normalColor = new Color(0.22f, 0.22f, 0.28f, 1f);
            cb.highlightedColor = new Color(0.38f, 0.38f, 0.5f, 1f);
            cb.pressedColor = new Color(0.14f, 0.14f, 0.18f, 1f);
            cb.selectedColor = cb.normalColor;
            cb.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            btn.colors = cb;
            Text t = NewText("Label", img.rectTransform, label, 28, TextAnchor.MiddleCenter, Color.white);
            Stretch(t.rectTransform, 0f);
            return btn;
        }

        private static GameObject NewPanel(Transform canvas, string name, Color tint, string title, Color titleColor, int titleSize, out RectTransform rect)
        {
            Image img = NewImage(name, canvas, tint);
            Stretch(img.rectTransform, 0f);
            rect = img.rectTransform;
            Text t = NewText("Title", rect, title, titleSize, TextAnchor.MiddleCenter, titleColor);
            Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 190f), new Vector2(1200f, 160f));
            img.gameObject.SetActive(false);
            return img.gameObject;
        }

        private static void CreateUi(GameManager gameManager, PlayerHealth player, WaveManager waveManager)
        {
            // Canvas + EventSystem
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            Transform root = canvasGo.transform;

            CreateEventSystem();

            // Health bar
            Image healthBack = NewImage("HealthBar", root, new Color(0f, 0f, 0f, 0.7f));
            Place(healthBack.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(30f, -30f), new Vector2(440f, 46f));
            RectTransform fillArea = NewRect("FillArea", healthBack.rectTransform);
            Stretch(fillArea, 5f);
            Image fill = NewImage("Fill", fillArea, new Color(0.25f, 0.8f, 0.3f, 1f));
            Stretch(fill.rectTransform, 0f);
            Text hpText = NewText("HealthText", healthBack.rectTransform, "HP  100 / 100", 26, TextAnchor.MiddleCenter, Color.white);
            Stretch(hpText.rectTransform, 0f);

            // Wave / kills / pause
            Text waveText = NewText("WaveText", root, "WAVE 1 / 3", 44, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.5f, 1f));
            Place(waveText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(500f, 60f));

            Text killsText = NewText("KillsText", root, "KILLS: 0", 36, TextAnchor.MiddleRight, Color.white);
            Place(killsText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-200f, -35f), new Vector2(320f, 50f));

            Button pauseButton = NewButton(root, "PauseButton", "PAUSE", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30f, -30f), new Vector2(150f, 56f));

            Text banner = NewText("WaveBanner", root, "WAVE 1", 90, TextAnchor.MiddleCenter, new Color(1f, 0.95f, 0.6f, 1f));
            Place(banner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(1000f, 140f));
            banner.gameObject.SetActive(false);

            // Pause panel
            RectTransform pauseRect;
            GameObject pausePanel = NewPanel(root, "PausePanel", new Color(0f, 0f, 0f, 0.7f), "PAUSED", Color.white, 96, out pauseRect);
            Button resume = NewButton(pauseRect, "ResumeButton", "RESUME  (ESC)", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(360f, 70f));
            Button pauseRestart = NewButton(pauseRect, "RestartButton", "RESTART", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(360f, 70f));
            Button pauseMenu = NewButton(pauseRect, "MenuButton", "MAIN MENU", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(360f, 70f));

            // Victory panel
            RectTransform victoryRect;
            GameObject victoryPanel = NewPanel(root, "VictoryPanel", new Color(0f, 0.12f, 0f, 0.8f), "VICTORY!", new Color(1f, 0.85f, 0.2f, 1f), 120, out victoryRect);
            Text victoryStats = NewText("Stats", victoryRect, "Enemies defeated: 0", 44, TextAnchor.MiddleCenter, Color.white);
            Place(victoryStats.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 70f), new Vector2(1000f, 70f));
            Button victoryRestart = NewButton(victoryRect, "RestartButton", "RESTART  (R)", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(400f, 80f));
            Button victoryMenu = NewButton(victoryRect, "MenuButton", "MAIN MENU", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -140f), new Vector2(400f, 80f));

            // Defeat panel
            RectTransform defeatRect;
            GameObject defeatPanel = NewPanel(root, "DefeatPanel", new Color(0.15f, 0f, 0f, 0.8f), "DEFEAT", new Color(0.95f, 0.25f, 0.25f, 1f), 120, out defeatRect);
            Text defeatStats = NewText("Stats", defeatRect, "Wave 1  |  Kills: 0", 44, TextAnchor.MiddleCenter, Color.white);
            Place(defeatStats.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 70f), new Vector2(1000f, 70f));
            Button defeatRestart = NewButton(defeatRect, "RestartButton", "RESTART  (R)", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(400f, 80f));
            Button defeatMenu = NewButton(defeatRect, "MenuButton", "MAIN MENU", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -140f), new Vector2(400f, 80f));

            // UIManager
            var ui = canvasGo.AddComponent<UIManager>();
            SetRef(ui, "gameManager", gameManager);
            SetRef(ui, "player", player);
            SetRef(ui, "waveManager", waveManager);
            SetRef(ui, "healthFill", fill.rectTransform);
            SetRef(ui, "healthFillImage", fill);
            SetRef(ui, "healthText", hpText);
            SetRef(ui, "waveText", waveText);
            SetRef(ui, "killsText", killsText);
            SetRef(ui, "waveBanner", banner);
            SetRef(ui, "pauseButton", pauseButton);
            SetRef(ui, "pausePanel", pausePanel);
            SetRef(ui, "victoryPanel", victoryPanel);
            SetRef(ui, "defeatPanel", defeatPanel);
            SetRef(ui, "resumeButton", resume);
            SetRef(ui, "pauseRestartButton", pauseRestart);
            SetRef(ui, "victoryRestartButton", victoryRestart);
            SetRef(ui, "defeatRestartButton", defeatRestart);
            SetRef(ui, "victoryStatsText", victoryStats);
            SetRef(ui, "defeatStatsText", defeatStats);
            SetRef(ui, "pauseMenuButton", pauseMenu);
            SetRef(ui, "victoryMenuButton", victoryMenu);
            SetRef(ui, "defeatMenuButton", defeatMenu);
        }
    }
}
