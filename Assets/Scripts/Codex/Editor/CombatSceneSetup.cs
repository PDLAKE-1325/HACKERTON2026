#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class CombatSceneSetup
{
    private const string GeneratedRoot = "Assets/CodexGenerated";

    [MenuItem("Tools/Codex/Finish Combat Scene Setup")]
    public static void FinishCombatSceneSetup()
    {
        EnsureFolder();

        GameObject player = Require("Player");
        GameObject enemy = Require("Enemy");
        GameObject systems = Require("GameSystems");
        GameObject canvasObject = Require("GameplayCanvas");
        GameObject vcamObject = Require("Gameplay VCam");
        Camera mainCamera = Require("Main Camera").GetComponent<Camera>();

        PhysicsMaterial2D zeroFriction = CreateZeroFrictionMaterial();
        Sprite skillSprite = CreateColorSprite("SkillOverlay", new Color(0.1f, 0.8f, 1f, 0.45f));
        Sprite finishableSprite = CreateColorSprite("MarkFinishable", new Color(0.2f, 1f, 0.35f, 1f));
        Sprite unavailableSprite = CreateColorSprite("MarkUnavailable", new Color(1f, 0.2f, 0.2f, 1f));
        Sprite muzzleSprite = CreateColorSprite("MuzzleFlash", new Color(1f, 0.85f, 0.15f, 1f));
        Sprite hitSprite = CreateColorSprite("HitEffect", new Color(1f, 0.35f, 0.15f, 1f));

        ConfigurePhysics(player, enemy, zeroFriction);
        ConfigureMaterials(player, enemy);
        ConfigureUi(canvasObject, mainCamera);
        ConfigureCamera(vcamObject, player);
        ConfigureVisuals(player, skillSprite);
        ConfigureAnimations(player, enemy);

        LineRenderer trailPrefab = CreateTrailPrefab();
        GameObject muzzlePrefab = CreateSpritePrefab("MuzzleFlash", muzzleSprite, 0.22f);
        GameObject hitPrefab = CreateSpritePrefab("HitEffect", hitSprite, 0.28f);
        SpriteRenderer markerPrefab = CreateMarkerPrefab(finishableSprite);
        Text damageTextPrefab = CreateDamageTextPrefab();

        Slider playerSlider = Require("PlayerHealthBar").GetComponent<Slider>();
        TargetHealthBar targetHealthBar = Require("TargetHealthBar").GetComponent<TargetHealthBar>();
        SpriteRenderer skillOverlay = Require("SkillOverlay").GetComponent<SpriteRenderer>();

        SetObjectReferences(player.GetComponent<PlayerController>(), new Dictionary<string, UnityEngine.Object>
        {
            { "movement", player.GetComponent<PlayerMovement>() },
            { "combat", player.GetComponent<PlayerCombat>() },
            { "skill", player.GetComponent<PlayerSkill>() }
        });

        SetObjectReferences(player.GetComponent<PlayerMovement>(), new Dictionary<string, UnityEngine.Object>
        {
            { "body", player.GetComponent<Rigidbody2D>() },
            { "groundCheck", Require("GroundCheck").transform },
            { "wallCheckOrigin", Require("WallCheckOrigin").transform }
        });
        SetLayerMask(player.GetComponent<PlayerMovement>(), "groundLayer", LayerMask.GetMask("Ground"));
        SetLayerMask(player.GetComponent<PlayerMovement>(), "wallLayer", LayerMask.GetMask("Wall"));

        SetObjectReferences(player.GetComponent<PlayerCombat>(), new Dictionary<string, UnityEngine.Object>
        {
            { "animator", player.GetComponent<Animator>() },
            { "meleeAttackPoint", Require("MeleeAttackPoint").transform },
            { "muzzle", Require("Muzzle").transform },
            { "aimCamera", mainCamera },
            { "targetHealthBar", targetHealthBar },
            { "bulletTrailPrefab", trailPrefab },
            { "muzzleFlashPrefab", muzzlePrefab },
            { "hitEffectPrefab", hitPrefab }
        });
        SetLayerMask(player.GetComponent<PlayerCombat>(), "enemyLayer", LayerMask.GetMask("Enemy"));
        SetLayerMask(player.GetComponent<PlayerCombat>(), "rangedHitLayers", LayerMask.GetMask("Enemy", "Ground", "Wall"));

        SetObjectReferences(player.GetComponent<PlayerSkill>(), new Dictionary<string, UnityEngine.Object>
        {
            { "targetCamera", mainCamera },
            { "skillSprite", skillOverlay }
        });
        SetLayerMask(player.GetComponent<PlayerSkill>(), "enemyLayer", LayerMask.GetMask("Enemy"));

        SetObjectReferences(player.GetComponent<PlayerHealth>(), new Dictionary<string, UnityEngine.Object>
        {
            { "body", player.GetComponent<Rigidbody2D>() },
            { "animator", player.GetComponent<Animator>() },
            { "movement", player.GetComponent<PlayerMovement>() },
            { "healthSlider", playerSlider },
            { "gameOverSprite", Require("GameOverUI") }
        });

        SetObjectReferences(enemy.GetComponent<EnemyBase>(), new Dictionary<string, UnityEngine.Object>
        {
            { "body", enemy.GetComponent<Rigidbody2D>() },
            { "animator", enemy.GetComponent<Animator>() },
            { "groundAheadCheck", Require("GroundAheadCheck").transform },
            { "attackPoint", Require("AttackPoint").transform },
            { "playerSkill", player.GetComponent<PlayerSkill>() },
            { "markerPrefab", markerPrefab },
            { "finishableMarkSprite", finishableSprite },
            { "unavailableMarkSprite", unavailableSprite }
        });
        SetLayerMask(enemy.GetComponent<EnemyBase>(), "groundLayer", LayerMask.GetMask("Ground"));
        SetLayerMask(enemy.GetComponent<EnemyBase>(), "playerLayer", LayerMask.GetMask("Player"));

        SetObjectReferences(systems.GetComponent<CameraManager>(), new Dictionary<string, UnityEngine.Object>
        {
            { "initialCamera", vcamObject.GetComponent<CinemachineCamera>() }
        });

        SetObjectReferences(systems.GetComponent<DamageTextManager>(), new Dictionary<string, UnityEngine.Object>
        {
            { "overlayCanvas", canvasObject.GetComponent<Canvas>() },
            { "damageTextPrefab", damageTextPrefab },
            { "worldCamera", mainCamera }
        });

        ConfigureCameraZone(vcamObject.GetComponent<CinemachineCamera>());

        Require("GameOverUI").SetActive(false);
        EditorUtility.SetDirty(player);
        EditorUtility.SetDirty(enemy);
        EditorUtility.SetDirty(systems);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CombatSceneSetup] Combat scene setup completed and saved.");
    }

    private static void ConfigurePhysics(GameObject player, GameObject enemy, PhysicsMaterial2D material)
    {
        ConfigureBody(player.GetComponent<Rigidbody2D>());
        ConfigureBody(enemy.GetComponent<Rigidbody2D>());

        foreach (Collider2D collider in UnityEngine.Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
        {
            if (collider.gameObject.layer == LayerMask.NameToLayer("Player") ||
                collider.gameObject.layer == LayerMask.NameToLayer("Enemy") ||
                collider.gameObject.layer == LayerMask.NameToLayer("Ground") ||
                collider.gameObject.layer == LayerMask.NameToLayer("Wall"))
            {
                collider.sharedMaterial = material;
                EditorUtility.SetDirty(collider);
            }
        }
    }

    private static void ConfigureBody(Rigidbody2D body)
    {
        body.bodyType = RigidbodyType2D.Dynamic;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        EditorUtility.SetDirty(body);
    }

    private static void ConfigureMaterials(GameObject player, GameObject enemy)
    {
        ConfigureMaterial(
            "Assets/Materials/CodexPlayer.mat",
            new Color(0.12f, 0.55f, 1f, 1f),
            player);
        ConfigureMaterial(
            "Assets/Materials/CodexEnemy.mat",
            new Color(1f, 0.18f, 0.18f, 1f),
            enemy);
        ConfigureMaterial(
            "Assets/Materials/CodexGround.mat",
            new Color(0.12f, 0.14f, 0.18f, 1f),
            Require("Ground"),
            Require("Left Wall"),
            Require("Right Wall"));
    }

    private static void ConfigureMaterial(string path, Color color, params GameObject[] objects)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
            throw new InvalidOperationException($"Required material '{path}' was not found.");

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        EditorUtility.SetDirty(material);

        foreach (GameObject target in objects)
        {
            MeshRenderer renderer = target.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            EditorUtility.SetDirty(renderer);
        }
    }

    private static void ConfigureUi(GameObject canvasObject, Camera mainCamera)
    {
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = null;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        ConfigureSlider(Require("PlayerHealthBar"), Require("PlayerHealthFill"), 100f);
        ConfigureSlider(Require("TargetHealthBar"), Require("TargetHealthFill"), 100f);

        TargetHealthBar targetBar = Require("TargetHealthBar").GetComponent<TargetHealthBar>();
        SetObjectReferences(targetBar, new Dictionary<string, UnityEngine.Object>
        {
            { "healthBarRoot", targetBar.gameObject },
            { "healthSlider", targetBar.GetComponent<Slider>() }
        });

        Text gameOver = Require("GameOverUI").GetComponent<Text>();
        gameOver.text = "GAME OVER";
        gameOver.fontSize = 64;
        gameOver.alignment = TextAnchor.MiddleCenter;
        gameOver.color = new Color(1f, 0.2f, 0.2f, 1f);
        gameOver.raycastTarget = false;
    }

    private static void ConfigureSlider(GameObject root, GameObject fillObject, float maximum)
    {
        Slider slider = root.GetComponent<Slider>();
        Image fill = fillObject.GetComponent<Image>();
        slider.fillRect = fill.rectTransform;
        slider.targetGraphic = fill;
        slider.minValue = 0f;
        slider.maxValue = maximum;
        slider.value = maximum;
        slider.interactable = false;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        EditorUtility.SetDirty(slider);
        EditorUtility.SetDirty(fill);
    }

    private static void ConfigureCamera(GameObject vcamObject, GameObject player)
    {
        CinemachineCamera vcam = vcamObject.GetComponent<CinemachineCamera>();
        vcam.Follow = player.transform;

        LensSettings lens = vcam.Lens;
        lens.OrthographicSize = 6f;
        vcam.Lens = lens;

        CinemachineFollow follow = vcamObject.GetComponent<CinemachineFollow>();
        if (follow == null)
            follow = vcamObject.AddComponent<CinemachineFollow>();
        follow.FollowOffset = new Vector3(0f, 1f, -10f);

        CinemachineBasicMultiChannelPerlin noise =
            vcamObject.GetComponent<CinemachineBasicMultiChannelPerlin>();
        noise.NoiseProfile = CreateNoiseProfile();
        noise.AmplitudeGain = 0f;
        noise.FrequencyGain = 1f;
        EditorUtility.SetDirty(vcam);
        EditorUtility.SetDirty(follow);
        EditorUtility.SetDirty(noise);
    }

    private static void ConfigureVisuals(GameObject player, Sprite sprite)
    {
        SpriteRenderer overlay = Require("SkillOverlay").GetComponent<SpriteRenderer>();
        overlay.sprite = sprite;
        overlay.color = new Color(1f, 1f, 1f, 0f);
        overlay.sortingOrder = 20;
        overlay.transform.localPosition = new Vector3(0f, 0f, -0.6f);
        overlay.transform.localScale = new Vector3(4f, 3f, 1f);
        overlay.gameObject.SetActive(false);
        EditorUtility.SetDirty(overlay);
    }

    private static void ConfigureAnimations(GameObject player, GameObject enemy)
    {
        EnsureAnimationClock(player);
        EnsureAnimationClock(enemy);

        AnimationClip playerIdle = CreateClip("PlayerIdle", 0.2f, null, true);
        AnimationClip playerMelee = CreateClip("PlayerMelee", 0.24f, new[]
        {
            new AnimationEvent { time = 0.06f, functionName = "EnableAttackHitbox" },
            new AnimationEvent { time = 0.16f, functionName = "DisableAttackHitbox" }
        }, false);
        AnimationClip playerShoot = CreateClip("PlayerShoot", 0.2f, null, false);
        AnimationClip playerHit = CreateClip("PlayerHit", 0.18f, null, false);
        AnimationClip playerDeath = CreateClip("PlayerDeath", 0.4f, null, false);

        AnimatorController playerController = CreateController(
            "PlayerCombat",
            playerIdle,
            new[]
            {
                new TriggerState("Melee", playerMelee, true),
                new TriggerState("Shoot", playerShoot, true),
                new TriggerState("Hit", playerHit, true),
                new TriggerState("Death", playerDeath, false)
            });
        player.GetComponent<Animator>().runtimeAnimatorController = playerController;

        AnimationClip enemyIdle = CreateClip("EnemyIdle", 0.2f, null, true);
        AnimationClip enemyAttack = CreateClip("EnemyAttack", 0.3f, new[]
        {
            new AnimationEvent { time = 0.08f, functionName = "EnableAttackHitbox" },
            new AnimationEvent { time = 0.2f, functionName = "DisableAttackHitbox" }
        }, false);
        AnimationClip enemyHit = CreateClip("EnemyHit", 0.18f, null, false);
        AnimationClip enemyDeath = CreateClip("EnemyDeath", 0.4f, null, false);

        AnimatorController enemyController = CreateController(
            "EnemyCombat",
            enemyIdle,
            new[]
            {
                new TriggerState("Attack", enemyAttack, true),
                new TriggerState("Hit", enemyHit, true),
                new TriggerState("Death", enemyDeath, false)
            });
        enemy.GetComponent<Animator>().runtimeAnimatorController = enemyController;
    }

    private static AnimationClip CreateClip(
        string name,
        float duration,
        AnimationEvent[] events,
        bool loop)
    {
        string path = $"{GeneratedRoot}/{name}.anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip { name = name, frameRate = 60f };
            AssetDatabase.CreateAsset(clip, path);
        }

        AnimationCurve clock = AnimationCurve.Constant(0f, duration, 0f);
        AnimationUtility.SetEditorCurve(
            clip,
            EditorCurveBinding.FloatCurve("AnimationClock", typeof(Transform), "m_LocalPosition.x"),
            clock);
        AnimationUtility.SetAnimationEvents(clip, events ?? Array.Empty<AnimationEvent>());

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimatorController CreateController(
        string name,
        AnimationClip idleClip,
        TriggerState[] triggeredStates)
    {
        string path = $"{GeneratedRoot}/{name}.controller";
        AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (existing != null)
            AssetDatabase.DeleteAsset(path);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState idle = stateMachine.AddState("Idle");
        idle.motion = idleClip;
        stateMachine.defaultState = idle;

        foreach (TriggerState definition in triggeredStates)
        {
            controller.AddParameter(definition.Trigger, AnimatorControllerParameterType.Trigger);
            AnimatorState state = stateMachine.AddState(definition.Trigger);
            state.motion = definition.Clip;

            AnimatorStateTransition enter = stateMachine.AddAnyStateTransition(state);
            enter.hasExitTime = false;
            enter.duration = 0f;
            enter.canTransitionToSelf = false;
            enter.AddCondition(AnimatorConditionMode.If, 0f, definition.Trigger);

            if (definition.ReturnToIdle)
            {
                AnimatorStateTransition exit = state.AddTransition(idle);
                exit.hasExitTime = true;
                exit.exitTime = 1f;
                exit.duration = 0f;
            }
        }

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void EnsureAnimationClock(GameObject owner)
    {
        Transform clock = owner.transform.Find("AnimationClock");
        if (clock != null)
            return;

        GameObject clockObject = new GameObject("AnimationClock");
        Undo.RegisterCreatedObjectUndo(clockObject, "Create animation clock");
        clockObject.transform.SetParent(owner.transform, false);
    }

    private static PhysicsMaterial2D CreateZeroFrictionMaterial()
    {
        string path = $"{GeneratedRoot}/ZeroFriction.physicsMaterial2D";
        PhysicsMaterial2D material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
        if (material == null)
        {
            material = new PhysicsMaterial2D("ZeroFriction")
            {
                friction = 0f,
                bounciness = 0f
            };
            AssetDatabase.CreateAsset(material, path);
        }
        return material;
    }

    private static NoiseSettings CreateNoiseProfile()
    {
        string path = $"{GeneratedRoot}/CombatShake.asset";
        NoiseSettings profile = AssetDatabase.LoadAssetAtPath<NoiseSettings>(path);
        if (profile != null)
            return profile;

        profile = ScriptableObject.CreateInstance<NoiseSettings>();
        profile.name = "CombatShake";
        profile.PositionNoise = new[]
        {
            new NoiseSettings.TransformNoiseParams
            {
                X = new NoiseSettings.NoiseParams { Frequency = 2.2f, Amplitude = 0.45f },
                Y = new NoiseSettings.NoiseParams { Frequency = 2.8f, Amplitude = 0.45f },
                Z = new NoiseSettings.NoiseParams { Frequency = 2f, Amplitude = 0.05f }
            },
            new NoiseSettings.TransformNoiseParams
            {
                X = new NoiseSettings.NoiseParams { Frequency = 7f, Amplitude = 0.12f },
                Y = new NoiseSettings.NoiseParams { Frequency = 9f, Amplitude = 0.12f },
                Z = new NoiseSettings.NoiseParams { Frequency = 8f, Amplitude = 0.02f }
            }
        };
        profile.OrientationNoise = Array.Empty<NoiseSettings.TransformNoiseParams>();
        AssetDatabase.CreateAsset(profile, path);
        return profile;
    }

    private static Sprite CreateColorSprite(string name, Color color)
    {
        string path = $"{GeneratedRoot}/{name}.asset";
        Sprite existing = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        if (existing != null)
            return existing;

        Texture2D texture = new Texture2D(16, 16, TextureFormat.RGBA32, false)
        {
            name = name + "Texture",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        Color[] pixels = Enumerable.Repeat(color, 16 * 16).ToArray();
        texture.SetPixels(pixels);
        texture.Apply();
        AssetDatabase.CreateAsset(texture, path);

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 16f, 16f),
            new Vector2(0.5f, 0.5f),
            16f);
        sprite.name = name;
        AssetDatabase.AddObjectToAsset(sprite, texture);
        EditorUtility.SetDirty(texture);
        EditorUtility.SetDirty(sprite);
        return sprite;
    }

    private static LineRenderer CreateTrailPrefab()
    {
        string materialPath = $"{GeneratedRoot}/TrailMaterial.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Sprites/Default"));
            material.color = new Color(1f, 0.85f, 0.2f, 1f);
            AssetDatabase.CreateAsset(material, materialPath);
        }

        string prefabPath = $"{GeneratedRoot}/BulletTrail.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            GameObject temporary = new GameObject("BulletTrail");
            LineRenderer renderer = temporary.AddComponent<LineRenderer>();
            renderer.useWorldSpace = true;
            renderer.positionCount = 0;
            renderer.widthMultiplier = 0.055f;
            renderer.numCapVertices = 2;
            renderer.sharedMaterial = material;
            renderer.startColor = new Color(1f, 0.95f, 0.5f, 1f);
            renderer.endColor = new Color(1f, 0.35f, 0.1f, 0f);
            prefab = PrefabUtility.SaveAsPrefabAsset(temporary, prefabPath);
            UnityEngine.Object.DestroyImmediate(temporary);
        }
        return prefab.GetComponent<LineRenderer>();
    }

    private static GameObject CreateSpritePrefab(string name, Sprite sprite, float scale)
    {
        string path = $"{GeneratedRoot}/{name}.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab != null)
            return prefab;

        GameObject temporary = new GameObject(name);
        SpriteRenderer renderer = temporary.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 30;
        temporary.transform.localScale = Vector3.one * scale;
        prefab = PrefabUtility.SaveAsPrefabAsset(temporary, path);
        UnityEngine.Object.DestroyImmediate(temporary);
        return prefab;
    }

    private static SpriteRenderer CreateMarkerPrefab(Sprite sprite)
    {
        string path = $"{GeneratedRoot}/MarkRenderer.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            GameObject temporary = new GameObject("MarkRenderer");
            SpriteRenderer renderer = temporary.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 25;
            temporary.transform.localScale = Vector3.one * 0.45f;
            prefab = PrefabUtility.SaveAsPrefabAsset(temporary, path);
            UnityEngine.Object.DestroyImmediate(temporary);
        }
        return prefab.GetComponent<SpriteRenderer>();
    }

    private static Text CreateDamageTextPrefab()
    {
        string path = $"{GeneratedRoot}/DamageText.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            GameObject temporary = new GameObject("DamageText", typeof(RectTransform));
            Text text = temporary.AddComponent<Text>();
            text.text = "0";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 34;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 0.85f, 0.15f, 1f);
            text.raycastTarget = false;
            text.rectTransform.sizeDelta = new Vector2(160f, 80f);
            prefab = PrefabUtility.SaveAsPrefabAsset(temporary, path);
            UnityEngine.Object.DestroyImmediate(temporary);
        }
        return prefab.GetComponent<Text>();
    }

    private static void ConfigureCameraZone(CinemachineCamera camera)
    {
        GameObject zone = GameObject.Find("CameraZone_Main");
        if (zone == null)
        {
            zone = new GameObject("CameraZone_Main");
            Undo.RegisterCreatedObjectUndo(zone, "Create camera zone");
            zone.transform.position = new Vector3(0f, 1f, 0f);
        }

        BoxCollider2D trigger = zone.GetComponent<BoxCollider2D>();
        if (trigger == null)
            trigger = zone.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(18f, 8f);

        CameraZone cameraZone = zone.GetComponent<CameraZone>();
        if (cameraZone == null)
            cameraZone = zone.AddComponent<CameraZone>();
        SetObjectReferences(cameraZone, new Dictionary<string, UnityEngine.Object>
        {
            { "zoneCamera", camera }
        });
        SetLayerMask(cameraZone, "playerLayer", LayerMask.GetMask("Player"));
    }

    private static void SetObjectReferences(
        UnityEngine.Object target,
        Dictionary<string, UnityEngine.Object> references)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        foreach (KeyValuePair<string, UnityEngine.Object> pair in references)
        {
            SerializedProperty property = serializedObject.FindProperty(pair.Key);
            if (property == null)
                throw new InvalidOperationException($"Missing serialized property '{pair.Key}' on {target.name}.");
            property.objectReferenceValue = pair.Value;
        }
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void SetLayerMask(UnityEngine.Object target, string propertyName, int value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException($"Missing layer property '{propertyName}' on {target.name}.");
        property.intValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static GameObject Require(string name)
    {
        GameObject found = Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(candidate => candidate.scene.IsValid() && candidate.name == name);
        if (found == null)
            throw new InvalidOperationException($"Required scene object '{name}' was not found.");
        return found;
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(GeneratedRoot))
            AssetDatabase.CreateFolder("Assets", "CodexGenerated");
    }

    private readonly struct TriggerState
    {
        public TriggerState(string trigger, AnimationClip clip, bool returnToIdle)
        {
            Trigger = trigger;
            Clip = clip;
            ReturnToIdle = returnToIdle;
        }

        public string Trigger { get; }
        public AnimationClip Clip { get; }
        public bool ReturnToIdle { get; }
    }
}
#endif
