using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fusion;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// Herramienta de editor "de una sola corrida" que termina de armar Banana
/// Rush a partir de los sprites en Assets/Sprites: configura el importer de
/// cada textura, slicea las hojas de animacion y el tileset del piso, genera
/// las animaciones/Animator de Moniko, arma los prefabs de red (Player,
/// Banana, BananaExplosiva, BananaGameManager) y deja lista la escena Game
/// (camara, fondo, piso). Se puede volver a correr sin problema: cada paso
/// sobreescribe lo anterior en vez de duplicar.
///
/// Se puede ejecutar desde el menu Tools/Banana Rush/Run Full Setup, o desde
/// linea de comandos en batchmode con -executeMethod BananaRushSetupTool.RunAll.
/// </summary>
public static class BananaRushSetupTool
{
    private const float PixelsPerUnit = 64f;

    private const string PlayerSpritesDir = "Assets/Sprites/Player";
    private const string PropsSpritesDir = "Assets/Sprites/Props";
    private const string EnvironmentSpritesDir = "Assets/Sprites/Environment";
    private const string AnimationsDir = "Assets/Animations/Player";
    private const string ResourcesDir = "Assets/Resources";

    [MenuItem("Tools/Banana Rush/Run Full Setup")]
    public static void RunAll()
    {
        try
        {
            Debug.Log("[BananaRush] === Empieza el setup ===");

            ConfigureSingleSprite($"{PlayerSpritesDir}/Moniko_Idle.png", new Vector2(0.5f, 0f), FilterMode.Point);
            Sprite idleSprite = LoadSingleSprite($"{PlayerSpritesDir}/Moniko_Idle.png");

            Sprite[] walkFrames = SliceUniformGrid($"{PlayerSpritesDir}/Moniko_Walk.png", 64, 64, "Moniko_Walk", new Vector2(0.5f, 0f));
            Sprite[] jumpFrames = SliceUniformGrid($"{PlayerSpritesDir}/Moniko_Jump.png", 64, 64, "Moniko_Jump", new Vector2(0.5f, 0f));
            walkFrames = walkFrames.Where(s => s != null).ToArray();
            jumpFrames = jumpFrames.Where(s => s != null).ToArray();
            Debug.Log($"[BananaRush] Walk frames: {walkFrames.Length}, Jump frames: {jumpFrames.Length}");

            ConfigureSingleSprite($"{PropsSpritesDir}/Banana.png", new Vector2(0.5f, 0.5f), FilterMode.Point);
            Sprite bananaSprite = LoadSingleSprite($"{PropsSpritesDir}/Banana.png");

            ConfigureSingleSprite($"{PropsSpritesDir}/Banana_Explosiva.png", new Vector2(0.5f, 0.5f), FilterMode.Point);
            Sprite bananaExplosivaSprite = LoadSingleSprite($"{PropsSpritesDir}/Banana_Explosiva.png");

            ConfigureSingleSprite($"{EnvironmentSpritesDir}/Background.png", new Vector2(0.5f, 0.5f), FilterMode.Bilinear);
            Sprite backgroundSprite = LoadSingleSprite($"{EnvironmentSpritesDir}/Background.png");

            Sprite groundTile = SliceGroundTileset($"{EnvironmentSpritesDir}/GroundTileset.png");
            Sprite dirtFillSprite = EnsureSolidColorSprite($"{EnvironmentSpritesDir}/DirtFill.png", new Color32(56, 34, 28, 255));

            RuntimeAnimatorController animatorController = BuildPlayerAnimator(idleSprite, walkFrames, jumpFrames);

            ConfigurePhysics2D();

            BuildPlayerPrefab(animatorController, idleSprite);
            BuildBananaPrefab("Banana", bananaSprite, 5);
            BuildBananaPrefab("BananaExplosiva", bananaExplosivaSprite, -10);
            BuildGameManagerPrefab();

            UpdateGameScene(backgroundSprite, groundTile, dirtFillSprite);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[BananaRush] === SETUP COMPLETADO OK ===");
        }
        catch (Exception ex)
        {
            Debug.LogError("[BananaRush] Fallo el setup: " + ex);
            throw;
        }
    }

    // -----------------------------------------------------------------
    // Import de sprites
    // -----------------------------------------------------------------

    private static void ConfigureSingleSprite(string path, Vector2 pivot, FilterMode filterMode)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = filterMode;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;

        // TextureImporter ya no expone "spriteAlignment" directo en Unity 6:
        // hay que pasar por TextureImporterSettings para poder fijar un pivot
        // custom en modo Single.
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        importer.SetTextureSettings(settings);

        importer.SaveAndReimport();
    }

    private static Sprite LoadSingleSprite(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Sprite[] SliceUniformGrid(string path, int frameWidth, int frameHeight, string namePrefix, Vector2 pivot)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.isReadable = true;
        importer.SaveAndReimport();

        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        int columns = Mathf.Max(1, texture.width / frameWidth);
        int rows = Mathf.Max(1, texture.height / frameHeight);

        var rects = new List<(string name, Rect rect)>();
        int index = 0;
        for (int row = rows - 1; row >= 0; row--)
        {
            for (int col = 0; col < columns; col++)
            {
                var rect = new Rect(col * frameWidth, row * frameHeight, frameWidth, frameHeight);
                if (!HasOpaquePixels(texture, rect))
                {
                    continue;
                }

                rects.Add(($"{namePrefix}_{index}", rect));
                index++;
            }
        }

        ApplySpriteRects(importer, path, rects, pivot);

        var allSprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name);
        return rects.Select(r => allSprites.TryGetValue(r.name, out Sprite s) ? s : null).ToArray();
    }

    private static Sprite SliceGroundTileset(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.isReadable = true;
        importer.SaveAndReimport();

        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        List<RectInt> islands = DetectOpaqueIslands(texture);

        Debug.Log($"[BananaRush] GroundTileset: se detectaron {islands.Count} piezas.");
        for (int i = 0; i < islands.Count; i++)
        {
            RectInt island = islands[i];
            Debug.Log($"[BananaRush]  - GroundTile_{i}: pos=({island.x},{island.y}) size={island.width}x{island.height} area={island.width * island.height}");
        }

        var rects = new List<(string name, Rect rect)>();
        for (int i = 0; i < islands.Count; i++)
        {
            RectInt island = islands[i];
            rects.Add(($"GroundTile_{i}", new Rect(island.x, island.y, island.width, island.height)));
        }

        ApplySpriteRects(importer, path, rects, new Vector2(0.5f, 0.5f));

        // Cada pieza es un bloque de piso completo (pasto arriba + tierra con
        // piedritas abajo, ver zoom durante el desarrollo), pensado para
        // repetirse solo HORIZONTALMENTE. El tileset trae 3 variantes iguales
        // arriba (bordes + una del medio) y otras 3 abajo con menos pasto; nos
        // quedamos con la fila de arriba (mas pasto visible) y, dentro de esa
        // fila, la pieza mas centrada (las de los costados son remates).
        int topY = islands.Max(i => i.y);
        float centerX = texture.width / 2f;
        int grassIndex = MostCenteredInRow(islands, topY, centerX);

        var allSprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name);
        Sprite grass = allSprites[$"GroundTile_{grassIndex}"];
        Debug.Log($"[BananaRush] GroundTileset: se usa 'GroundTile_{grassIndex}' (y={topY}, la mas centrada) como bloque de piso.");
        return grass;
    }

    /// <summary>
    /// Genera (si no existe) un sprite chico de color solido, para usar como
    /// relleno debajo de la franja de pasto/tierra del piso sin depender de
    /// otro tile del tileset.
    /// </summary>
    private static Sprite EnsureSolidColorSprite(string path, Color32 color)
    {
        if (!File.Exists(path))
        {
            const int size = 8;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
        }

        ConfigureSingleSprite(path, new Vector2(0.5f, 0.5f), FilterMode.Point);
        return LoadSingleSprite(path);
    }

    private static int MostCenteredInRow(List<RectInt> islands, int rowY, float centerX)
    {
        int bestIndex = 0;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < islands.Count; i++)
        {
            if (islands[i].y != rowY)
            {
                continue;
            }

            float distance = Mathf.Abs((islands[i].x + islands[i].width / 2f) - centerX);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static void ApplySpriteRects(TextureImporter importer, string path, List<(string name, Rect rect)> rects, Vector2 pivot)
    {
        var factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
        dataProvider.InitSpriteEditorDataProvider();

        // Reusar los spriteID existentes por nombre: si se regeneran, los
        // clips de animacion quedan apuntando a IDs viejos y ese frame se
        // ve vacio (el personaje "desaparece" un instante).
        SpriteRect[] existing = dataProvider.GetSpriteRects() ?? Array.Empty<SpriteRect>();
        var existingByName = existing.ToDictionary(r => r.name, r => r);

        SpriteRect[] spriteRects = rects.Select(r =>
        {
            if (existingByName.TryGetValue(r.name, out SpriteRect old))
            {
                old.rect = r.rect;
                old.pivot = pivot;
                old.alignment = SpriteAlignment.Custom;
                return old;
            }

            return new SpriteRect
            {
                name = r.name,
                spriteID = GUID.Generate(),
                rect = r.rect,
                pivot = pivot,
                alignment = SpriteAlignment.Custom,
            };
        }).ToArray();

        dataProvider.SetSpriteRects(spriteRects);

        var nameFileIdProvider = dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        SpriteNameFileIdPair[] pairs = spriteRects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToArray();
        nameFileIdProvider.SetNameFileIdPairs(pairs);

        dataProvider.Apply();
        importer.SaveAndReimport();
    }

    private static List<RectInt> DetectOpaqueIslands(Texture2D texture)
    {
        int w = texture.width;
        int h = texture.height;
        Color32[] pixels = texture.GetPixels32();
        bool[] visited = new bool[w * h];
        var islands = new List<RectInt>();
        var stack = new Stack<Vector2Int>();

        bool IsOpaque(int x, int y) => pixels[y * w + x].a > 10;

        void TryVisit(int nx, int ny)
        {
            if (nx < 0 || nx >= w || ny < 0 || ny >= h)
            {
                return;
            }

            int nIdx = ny * w + nx;
            if (visited[nIdx] || !IsOpaque(nx, ny))
            {
                return;
            }

            visited[nIdx] = true;
            stack.Push(new Vector2Int(nx, ny));
        }

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                if (visited[idx] || !IsOpaque(x, y))
                {
                    continue;
                }

                int minX = x, maxX = x, minY = y, maxY = y;
                stack.Clear();
                stack.Push(new Vector2Int(x, y));
                visited[idx] = true;

                while (stack.Count > 0)
                {
                    Vector2Int p = stack.Pop();
                    if (p.x < minX) minX = p.x;
                    if (p.x > maxX) maxX = p.x;
                    if (p.y < minY) minY = p.y;
                    if (p.y > maxY) maxY = p.y;

                    TryVisit(p.x + 1, p.y);
                    TryVisit(p.x - 1, p.y);
                    TryVisit(p.x, p.y + 1);
                    TryVisit(p.x, p.y - 1);
                }

                islands.Add(new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1));
            }
        }

        return islands;
    }

    // -----------------------------------------------------------------
    // Animaciones + Animator de Moniko
    // -----------------------------------------------------------------

    private static RuntimeAnimatorController BuildPlayerAnimator(Sprite idleSprite, Sprite[] walkFrames, Sprite[] jumpFrames)
    {
        EnsureFolder(AnimationsDir);

        AnimationClip idleClip = CreateSpriteClip($"{AnimationsDir}/Moniko_Idle.anim", new[] { idleSprite }, 4f, true);
        AnimationClip walkClip = CreateSpriteClip($"{AnimationsDir}/Moniko_Walk.anim", walkFrames, 10f, true);

        // El spritesheet de salto arranca en pose de piso y termina aterrizando.
        // En el aire solo queremos las poses de vuelo, y sin loop: si loopea
        // se ve el agachado otra vez a mitad de salto.
        Sprite[] airborneJump = PickAirborneJumpFrames(jumpFrames);
        AnimationClip jumpClip = CreateSpriteClip($"{AnimationsDir}/Moniko_Jump.anim", airborneJump, 12f, false);

        string controllerPath = $"{AnimationsDir}/Moniko.controller";
        if (File.Exists(controllerPath))
        {
            AssetDatabase.DeleteAsset(controllerPath);
        }

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        AnimatorControllerLayer[] layers = controller.layers;
        layers[0].defaultWeight = 1f;
        controller.layers = layers;
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

        // Ojo: el default tiene que ser "true". Los personajes arrancan
        // parados en el piso, y con default "false" el Animator entra al
        // estado Idle pero ENSEGUIDA evalua la transicion "Idle -> Jump
        // cuando Grounded=false" (que ya se cumple con el default, antes de
        // que el primer FixedUpdateNetwork llegue a corregirlo) y se queda
        // trabado mostrando la pose de salto para siempre.
        controller.AddParameter(new AnimatorControllerParameter
        {
            name = "Grounded",
            type = AnimatorControllerParameterType.Bool,
            defaultBool = true,
        });

        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        AnimatorState idleState = sm.AddState("Idle");
        idleState.motion = idleClip;
        sm.defaultState = idleState;

        AnimatorState walkState = sm.AddState("Walk");
        walkState.motion = walkClip;

        AnimatorState jumpState = sm.AddState("Jump");
        jumpState.motion = jumpClip;

        AddInstantTransition(idleState, walkState, t => t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed"));
        AddInstantTransition(walkState, idleState, t => t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed"));

        AddInstantTransition(idleState, jumpState, t => t.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded"));
        AddInstantTransition(walkState, jumpState, t => t.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded"));

        AddInstantTransition(jumpState, idleState, t =>
        {
            t.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
            t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        });
        AddInstantTransition(jumpState, walkState, t =>
        {
            t.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
            t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        });

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static void AddInstantTransition(AnimatorState from, AnimatorState to, Action<AnimatorStateTransition> configureConditions)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = 0f;
        transition.exitTime = 0f;
        configureConditions(transition);
    }

    private static bool HasOpaquePixels(Texture2D texture, Rect rect)
    {
        int x = Mathf.Clamp(Mathf.RoundToInt(rect.x), 0, texture.width - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt(rect.y), 0, texture.height - 1);
        int width = Mathf.Clamp(Mathf.RoundToInt(rect.width), 1, texture.width - x);
        int height = Mathf.Clamp(Mathf.RoundToInt(rect.height), 1, texture.height - y);

        Color32[] pixels = texture.GetPixels32();
        for (int py = y; py < y + height; py++)
        {
            int row = py * texture.width;
            for (int px = x; px < x + width; px++)
            {
                if (pixels[row + px].a > 10)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static Sprite[] PickAirborneJumpFrames(Sprite[] jumpFrames)
    {
        Sprite[] valid = jumpFrames.Where(s => s != null).ToArray();
        if (valid.Length >= 5)
        {
            return valid.Skip(2).Take(valid.Length - 3).ToArray();
        }

        return valid;
    }

    private static AnimationClip CreateSpriteClip(string path, Sprite[] frames, float frameRate, bool loop)
    {
        frames = frames.Where(s => s != null).ToArray();
        if (frames.Length == 0)
        {
            throw new InvalidOperationException($"No hay sprites validos para el clip {path}.");
        }

        if (File.Exists(path))
        {
            AssetDatabase.DeleteAsset(path);
        }

        var clip = new AnimationClip { frameRate = frameRate };
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        var binding = new EditorCurveBinding
        {
            path = string.Empty,
            type = typeof(SpriteRenderer),
            propertyName = "m_Sprite",
        };

        // Si el ultimo keyframe queda justo en el ultimo frame, ese frame se
        // ve un instante. Se agrega un key extra al final: en loop repite el
        // primero (ciclo parejo); si no loopea, sostiene el ultimo sprite.
        var keyframes = new ObjectReferenceKeyframe[frames.Length + 1];
        for (int i = 0; i < frames.Length; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe { time = i / frameRate, value = frames[i] };
        }

        Sprite holdSprite = loop ? frames[0] : frames[frames.Length - 1];
        keyframes[frames.Length] = new ObjectReferenceKeyframe { time = frames.Length / frameRate, value = holdSprite };

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    // -----------------------------------------------------------------
    // Physics2D
    // -----------------------------------------------------------------

    private static void ConfigurePhysics2D()
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        int groundLayer = LayerMask.NameToLayer("Ground");
        int bananaLayer = LayerMask.NameToLayer("Banana");

        if (playerLayer < 0 || groundLayer < 0 || bananaLayer < 0)
        {
            throw new InvalidOperationException("Faltan los layers Player/Ground/Banana en TagManager.asset.");
        }

        // Los jugadores no chocan entre si, pero si con el piso y las bananas.
        Physics2D.IgnoreLayerCollision(playerLayer, playerLayer, true);
        Physics2D.IgnoreLayerCollision(playerLayer, groundLayer, false);
        Physics2D.IgnoreLayerCollision(playerLayer, bananaLayer, false);
        Physics2D.IgnoreLayerCollision(groundLayer, bananaLayer, true);
        Physics2D.IgnoreLayerCollision(bananaLayer, bananaLayer, true);

        Debug.Log("[BananaRush] Physics2D layer collision matrix configurada.");
    }

    // -----------------------------------------------------------------
    // Prefabs
    // -----------------------------------------------------------------

    private static void BuildPlayerPrefab(RuntimeAnimatorController controller, Sprite idleSprite)
    {
        string path = $"{ResourcesDir}/Player.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);

        root.layer = LayerMask.NameToLayer("Player");
        root.transform.position = new Vector3(0f, BananaRushConfig.GroundTopY, 0f);

        var spriteRenderer = root.GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = idleSprite;
        spriteRenderer.sortingLayerName = "Player";

        var rigidbody = root.GetComponent<Rigidbody2D>();
        if (rigidbody == null)
        {
            rigidbody = root.AddComponent<Rigidbody2D>();
        }

        rigidbody.bodyType = RigidbodyType2D.Kinematic;
        rigidbody.gravityScale = 0f;
        rigidbody.freezeRotation = true;
        rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
        rigidbody.sleepMode = RigidbodySleepMode2D.NeverSleep;

        var collider = root.GetComponent<BoxCollider2D>();
        if (collider == null)
        {
            collider = root.AddComponent<BoxCollider2D>();
        }

        collider.size = new Vector2(0.7f, 0.9f);
        collider.offset = new Vector2(0f, 0.45f);
        collider.isTrigger = false;

        var animator = root.GetComponent<Animator>();
        if (animator == null)
        {
            animator = root.AddComponent<Animator>();
        }

        animator.runtimeAnimatorController = controller;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.keepAnimatorStateOnDisable = true;

        var playerController = root.GetComponent<PlayerController>();
        var so = new SerializedObject(playerController);
        so.FindProperty("_moveSpeed").floatValue = 8f;
        so.FindProperty("_jumpForce").floatValue = 9f;
        so.FindProperty("_gravity").floatValue = 20f;
        so.FindProperty("_maxFallSpeed").floatValue = 20f;
        so.FindProperty("_animator").objectReferenceValue = animator;
        so.FindProperty("_animatorController").objectReferenceValue = controller;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);

        Debug.Log("[BananaRush] Player.prefab actualizado.");
    }

    private const float BananaScale = 0.65f;

    private static void BuildBananaPrefab(string prefabName, Sprite sprite, int pointValue)
    {
        string path = $"{ResourcesDir}/{prefabName}.prefab";

        var go = new GameObject(prefabName);
        try
        {
            go.layer = LayerMask.NameToLayer("Banana");
            go.transform.localScale = new Vector3(BananaScale, BananaScale, 1f);

            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingLayerName = "Props";

            var collider = go.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.32f;

            go.AddComponent<NetworkObject>();
            go.AddComponent<NetworkTransform>();

            var banana = go.AddComponent<BananaController>();
            var so = new SerializedObject(banana);
            so.FindProperty("_pointValue").intValue = pointValue;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(go, path);
            Debug.Log($"[BananaRush] {prefabName}.prefab creado (puntos={pointValue}).");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    private static void BuildGameManagerPrefab()
    {
        string path = $"{ResourcesDir}/BananaGameManager.prefab";

        var go = new GameObject("BananaGameManager");
        try
        {
            go.AddComponent<NetworkObject>();
            go.AddComponent<BananaGameManager>();

            PrefabUtility.SaveAsPrefabAsset(go, path);
            Debug.Log("[BananaRush] BananaGameManager.prefab creado.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    // -----------------------------------------------------------------
    // Escena Game: camara, fondo y piso
    // -----------------------------------------------------------------

    private static void UpdateGameScene(Sprite backgroundSprite, Sprite groundTile, Sprite dirtFillSprite)
    {
        const string scenePath = "Assets/Scenes/Game.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        GameObject cameraGO = GameObject.Find("Main Camera");
        if (cameraGO == null)
        {
            cameraGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        }

        cameraGO.tag = "MainCamera";
        var cam = cameraGO.GetComponent<Camera>();
        if (cam == null)
        {
            cam = cameraGO.AddComponent<Camera>();
        }

        cam.orthographic = true;
        cam.orthographicSize = BananaRushConfig.CameraOrthographicSize;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.72f, 0.86f, 0.55f);
        cameraGO.transform.position = new Vector3(0f, BananaRushConfig.CameraCenterY, -10f);

        GameObject backgroundGO = GameObject.Find("Background");
        if (backgroundGO == null)
        {
            backgroundGO = new GameObject("Background");
        }

        var bgRenderer = backgroundGO.GetComponent<SpriteRenderer>();
        if (bgRenderer == null)
        {
            bgRenderer = backgroundGO.AddComponent<SpriteRenderer>();
        }

        bgRenderer.sprite = backgroundSprite;
        bgRenderer.sortingLayerName = "Background";

        const float desiredWidth = 64f;
        const float desiredHeight = 27f;
        float spriteWidthUnits = backgroundSprite.rect.width / backgroundSprite.pixelsPerUnit;
        float spriteHeightUnits = backgroundSprite.rect.height / backgroundSprite.pixelsPerUnit;
        backgroundGO.transform.position = new Vector3(0f, BananaRushConfig.CameraCenterY, 0f);
        backgroundGO.transform.localScale = new Vector3(desiredWidth / spriteWidthUnits, desiredHeight / spriteHeightUnits, 1f);

        // El piso se arma con dos capas: la pieza del tileset (que ya trae
        // pasto arriba + tierra abajo dibujados) tileada SOLO a lo ancho y
        // usada una unica vez de alto (repetirla en vertical duplicaba el
        // pasto y quedaba a rayas), mas un relleno de color solido por debajo
        // que ocupa el resto del grosor del collider para que no se vea
        // "flotando".
        float groundWidth = BananaRushConfig.GroundHalfWidth * 2f;
        float grassHeight = groundTile.rect.height / groundTile.pixelsPerUnit;
        float dirtHeight = Mathf.Max(0.1f, BananaRushConfig.GroundThickness - grassHeight);

        GameObject groundGO = GameObject.Find("Ground");
        if (groundGO == null)
        {
            groundGO = new GameObject("Ground");
        }

        groundGO.layer = LayerMask.NameToLayer("Ground");
        groundGO.transform.position = Vector3.zero;

        // Corridas viejas de esta herramienta dejaban el SpriteRenderer
        // directo en "Ground"; ahora el dibujado vive en los hijos
        // GrassTop/DirtFill, asi que si quedo ese componente hay que sacarlo.
        var obsoleteRenderer = groundGO.GetComponent<SpriteRenderer>();
        if (obsoleteRenderer != null)
        {
            UnityEngine.Object.DestroyImmediate(obsoleteRenderer, true);
        }

        var groundCollider = groundGO.GetComponent<BoxCollider2D>();
        if (groundCollider == null)
        {
            groundCollider = groundGO.AddComponent<BoxCollider2D>();
        }

        groundCollider.size = new Vector2(groundWidth, BananaRushConfig.GroundThickness);
        groundCollider.offset = new Vector2(0f, BananaRushConfig.GroundTopY - BananaRushConfig.GroundThickness / 2f);
        groundCollider.isTrigger = false;

        Transform grassTransform = EnsureChild(groundGO.transform, "GrassTop");
        var grassRenderer = GetOrAddComponent<SpriteRenderer>(grassTransform.gameObject);
        grassRenderer.sprite = groundTile;
        grassRenderer.drawMode = SpriteDrawMode.Tiled;
        grassRenderer.tileMode = SpriteTileMode.Continuous;
        grassRenderer.size = new Vector2(groundWidth, grassHeight);
        grassRenderer.sortingLayerName = "Ground";
        grassRenderer.sortingOrder = 1;
        grassTransform.position = new Vector3(0f, BananaRushConfig.GroundTopY - grassHeight / 2f, 0f);

        Transform dirtTransform = EnsureChild(groundGO.transform, "DirtFill");
        var dirtRenderer = GetOrAddComponent<SpriteRenderer>(dirtTransform.gameObject);
        dirtRenderer.sprite = dirtFillSprite;
        dirtRenderer.drawMode = SpriteDrawMode.Tiled;
        dirtRenderer.tileMode = SpriteTileMode.Continuous;
        dirtRenderer.size = new Vector2(groundWidth, dirtHeight);
        dirtRenderer.sortingLayerName = "Ground";
        dirtRenderer.sortingOrder = 0;
        dirtTransform.position = new Vector3(0f, BananaRushConfig.GroundTopY - grassHeight - dirtHeight / 2f, 0f);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[BananaRush] Escena Game actualizada (camara, fondo, piso).");
    }

    // -----------------------------------------------------------------
    // Verificacion visual (no se guarda nada, solo genera un PNG para
    // inspeccionar a ojo que el nivel/personajes/props se vean bien)
    // -----------------------------------------------------------------

    [MenuItem("Tools/Banana Rush/Capture Visual Test")]
    public static void CaptureVisualTest()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Single);

        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ResourcesDir}/Player.prefab");
        var bananaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ResourcesDir}/Banana.prefab");
        var bananaExplosivaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ResourcesDir}/BananaExplosiva.prefab");

        SpawnTestPlayer(playerPrefab, new Vector3(-12f, 0f, 0f), new Color(1.00f, 0.50f, 0.50f), false);
        SpawnTestPlayer(playerPrefab, new Vector3(-4f, 0f, 0f), new Color(0.55f, 0.75f, 1.00f), true);
        SpawnTestPlayer(playerPrefab, new Vector3(4f, 0f, 0f), new Color(0.60f, 1.00f, 0.60f), false);
        SpawnTestPlayer(playerPrefab, new Vector3(12f, 0f, 0f), Color.white, true);

        UnityEngine.Object.Instantiate(bananaPrefab, new Vector3(-6f, 6f, 0f), Quaternion.identity);
        UnityEngine.Object.Instantiate(bananaPrefab, new Vector3(2f, 8f, 0f), Quaternion.identity);
        UnityEngine.Object.Instantiate(bananaExplosivaPrefab, new Vector3(8f, 5f, 0f), Quaternion.identity);

        Camera cam = Camera.main;
        const int width = 1280;
        const int height = 720;
        var rt = new RenderTexture(width, height, 24);
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;

        string outPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "visual_test.png"));
        File.WriteAllBytes(outPath, tex.EncodeToPNG());
        Debug.Log("[BananaRush] Captura de prueba guardada en " + outPath);

        UnityEngine.Object.DestroyImmediate(rt);
        UnityEngine.Object.DestroyImmediate(tex);

        EditorApplication.Exit(0);
    }

    /// <summary>
    /// Prueba puntual para el bug de "el mono arranca trabado en la pose de
    /// salto": crea un Animator con el controller de Moniko, lo hace avanzar
    /// varios frames SIN llamar SetBool (el peor caso, para que dependa
    /// pura y exclusivamente del default del parametro "Grounded") y loguea
    /// en que estado termina. Si el default esta bien (true), tiene que
    /// quedarse en Idle.
    /// </summary>
    [MenuItem("Tools/Banana Rush/Test Animator Grounded Default")]
    public static void TestAnimatorGroundedDefault()
    {
        var go = new GameObject("AnimTest", typeof(SpriteRenderer), typeof(Animator));
        try
        {
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"{AnimationsDir}/Moniko.controller");
            if (controller == null)
            {
                Debug.LogError("[BananaRush] TEST: no se encontro Moniko.controller. Corre RunAll primero.");
                return;
            }

            var animator = go.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            LogAnimatorState(animator, "frame 0 (recien creado)");

            for (int i = 0; i < 20; i++)
            {
                animator.Update(0.05f);
            }

            LogAnimatorState(animator, "despues de 20 frames sin llamar SetBool");

            // Ahora el ciclo real: saltar (Grounded=false) y aterrizar
            // (Grounded=true) de nuevo, para confirmar que las transiciones
            // van y vuelven bien (no que quedo pegado en Idle porque las
            // transiciones estan rotas).
            animator.SetBool("Grounded", false);
            for (int i = 0; i < 5; i++)
            {
                animator.Update(0.05f);
            }

            LogAnimatorState(animator, "SetBool(Grounded,false) + 5 frames (deberia ser Jump)");

            animator.SetBool("Grounded", true);
            for (int i = 0; i < 5; i++)
            {
                animator.Update(0.05f);
            }

            LogAnimatorState(animator, "SetBool(Grounded,true) + 5 frames (deberia volver a Idle)");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    private static void LogAnimatorState(Animator animator, string label)
    {
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        Debug.Log($"[BananaRush] TEST ANIMATOR [{label}]: isIdle={state.IsName("Idle")} isWalk={state.IsName("Walk")} isJump={state.IsName("Jump")} normalizedTime={state.normalizedTime:F2}");
    }

    private static void SpawnTestPlayer(GameObject prefab, Vector3 position, Color tint, bool flip)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.position = position;
        var sr = instance.GetComponent<SpriteRenderer>();
        sr.color = tint;
        sr.flipX = flip;
    }

    // -----------------------------------------------------------------
    // Utils
    // -----------------------------------------------------------------

    private static Transform EnsureChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            return existing;
        }

        var child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private static T GetOrAddComponent<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string folderName = Path.GetFileName(path);

        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, folderName);
    }
}
