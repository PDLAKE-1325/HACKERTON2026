#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class BossEnemySetup
{
    private const string GeneratedRoot = "Assets/CodexGenerated";
    private const string PrefabRoot = "Assets/Prefabs";
    private const string DeathParticlePath = GeneratedRoot + "/EnemyDeathBurst.prefab";
    private const string DeathMaterialPath = GeneratedRoot + "/DeathParticleMaterial.mat";
    private const string NormalMobPath = PrefabRoot + "/NormalMob.prefab";
    private const string BossRockPath = PrefabRoot + "/BossRock.prefab";
    private const string BossPath = PrefabRoot + "/BossEnemy.prefab";

    [MenuItem("Tools/Codex/Repair Enemy Death Particle And Create Boss")]
    public static void RepairDeathParticleAndCreateBoss()
    {
        EnsureFolders();

        GameObject deathParticlePrefab = RepairDeathParticlePrefab();
        RepairNormalMobReference(deathParticlePrefab);
        BossRockProjectile rockPrefab = CreateBossRockPrefab();
        CreateBossPrefab(deathParticlePrefab, rockPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[BossEnemySetup] Death particle repaired and boss prefabs created.");
    }

    private static GameObject RepairDeathParticlePrefab()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(DeathMaterialPath);
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            throw new InvalidOperationException("A particle shader could not be found.");

        if (material == null)
        {
            material = new Material(shader) { name = "DeathParticleMaterial" };
            AssetDatabase.CreateAsset(material, DeathMaterialPath);
        }
        else
        {
            material.shader = shader;
        }

        Color particleColor = new Color(1f, 0.3f, 0.08f, 1f);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", Color.white);
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", 5f);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", 10f);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = 3000;
        EditorUtility.SetDirty(material);

        bool loadedPrefabContents = AssetDatabase.LoadAssetAtPath<GameObject>(DeathParticlePath) != null;
        GameObject root = loadedPrefabContents
            ? PrefabUtility.LoadPrefabContents(DeathParticlePath)
            : new GameObject("EnemyDeathBurst");

        try
        {
            ParticleSystem particle = root.GetComponent<ParticleSystem>();
            if (particle == null)
                particle = root.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = particle.main;
            main.duration = 0.55f;
            main.loop = false;
            main.playOnAwake = true;
            main.useUnscaledTime = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.45f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                particleColor,
                new Color(1f, 0.9f, 0.15f, 1f));
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;

            ParticleSystem.EmissionModule emission = particle.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 30) });

            ParticleSystem.ShapeModule shape = particle.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.25f;

            ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = 100;

            PrefabUtility.SaveAsPrefabAsset(root, DeathParticlePath);
        }
        finally
        {
            if (loadedPrefabContents)
                PrefabUtility.UnloadPrefabContents(root);
            else
                UnityEngine.Object.DestroyImmediate(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(DeathParticlePath);
    }

    private static void RepairNormalMobReference(GameObject deathParticlePrefab)
    {
        GameObject normalMob = AssetDatabase.LoadAssetAtPath<GameObject>(NormalMobPath);
        if (normalMob == null)
            throw new InvalidOperationException("NormalMob.prefab does not exist.");

        GameObject root = PrefabUtility.LoadPrefabContents(NormalMobPath);
        try
        {
            EnemyBase enemy = root.GetComponent<EnemyBase>();
            if (enemy == null)
                throw new InvalidOperationException("NormalMob.prefab requires EnemyBase.");

            SetObjectReference(enemy, "deathParticlePrefab", deathParticlePrefab);
            PrefabUtility.SaveAsPrefabAsset(root, NormalMobPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static BossRockProjectile CreateBossRockPrefab()
    {
        Sprite square = LoadGeneratedSprite("WorldSquare.asset");
        GameObject rock = new GameObject("BossRock");
        try
        {
            rock.transform.localScale = Vector3.one * 0.8f;

            SpriteRenderer renderer = rock.AddComponent<SpriteRenderer>();
            renderer.sprite = square;
            renderer.color = new Color(0.35f, 0.3f, 0.25f, 1f);
            renderer.sortingOrder = 30;

            Rigidbody2D body = rock.AddComponent<Rigidbody2D>();
            body.gravityScale = 1.2f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CircleCollider2D collider = rock.AddComponent<CircleCollider2D>();
            collider.radius = 0.5f;

            BossRockProjectile projectile = rock.AddComponent<BossRockProjectile>();
            SetObjectReference(projectile, "body", body);
            SetObjectReference(projectile, "bodyCollider", collider);
            SetLayerMask(projectile, "playerLayer", LayerMask.GetMask("Player"));

            PrefabUtility.SaveAsPrefabAsset(rock, BossRockPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rock);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(BossRockPath)
            .GetComponent<BossRockProjectile>();
    }

    private static void CreateBossPrefab(
        GameObject deathParticlePrefab,
        BossRockProjectile rockPrefab)
    {
        GameObject normalMobPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NormalMobPath);
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            GeneratedRoot + "/EnemyCombat.controller");
        Sprite square = LoadGeneratedSprite("WorldSquare.asset");
        SpriteRenderer markerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            GeneratedRoot + "/MarkRenderer.prefab")?.GetComponent<SpriteRenderer>();
        Sprite finishableMark = LoadGeneratedSprite("MarkFinishable.asset");
        Sprite unavailableMark = LoadGeneratedSprite("MarkUnavailable.asset");

        GameObject bossObject = new GameObject("BossEnemy");
        try
        {
            bossObject.layer = LayerMask.NameToLayer("Enemy");
            bossObject.transform.localScale = new Vector3(2.5f, 2.5f, 1f);

            SpriteRenderer renderer = bossObject.AddComponent<SpriteRenderer>();
            renderer.sprite = square;
            renderer.color = new Color(0.55f, 0.12f, 0.75f, 1f);
            renderer.sortingOrder = 12;

            Rigidbody2D body = bossObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeAll;

            BoxCollider2D collider = bossObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.2f, 1.4f);

            Animator animator = bossObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            BossEnemy boss = bossObject.AddComponent<BossEnemy>();
            Transform rockSpawnPoint = CreateChild(bossObject.transform, "RockSpawnPoint", new Vector3(0f, 1.1f, 0f));
            Transform minionSpawnPoint = CreateChild(bossObject.transform, "MinionSpawnPoint", new Vector3(1.5f, -0.3f, 0f));
            CreateChild(bossObject.transform, "AnimationClock", Vector3.zero);

            SetObjectReference(boss, "body", body);
            SetObjectReference(boss, "animator", animator);
            SetObjectReference(boss, "deathParticlePrefab", deathParticlePrefab);
            SetObjectReference(boss, "markerPrefab", markerPrefab);
            SetObjectReference(boss, "finishableMarkSprite", finishableMark);
            SetObjectReference(boss, "unavailableMarkSprite", unavailableMark);
            SetObjectReference(boss, "rockPrefab", rockPrefab);
            SetObjectReference(boss, "rockSpawnPoint", rockSpawnPoint);
            SetObjectReference(boss, "normalMobPrefab", normalMobPrefab);
            SetObjectReference(boss, "minionSpawnPoint", minionSpawnPoint);
            SetFloat(boss, "maxHealth", 500f);
            SetFloat(boss, "moveSpeed", 0f);
            SetFloat(boss, "chaseSpeed", 0f);
            SetLayerMask(boss, "groundLayer", LayerMask.GetMask("Ground"));
            SetLayerMask(boss, "wallLayer", LayerMask.GetMask("Wall"));
            SetLayerMask(boss, "playerLayer", LayerMask.GetMask("Player"));

            PrefabUtility.SaveAsPrefabAsset(bossObject, BossPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(bossObject);
        }
    }

    private static Transform CreateChild(Transform parent, string name, Vector3 localPosition)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = localPosition;
        return child.transform;
    }

    private static Sprite LoadGeneratedSprite(string fileName)
    {
        Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(GeneratedRoot + "/" + fileName)
            .OfType<Sprite>()
            .FirstOrDefault();
        if (sprite == null)
            throw new InvalidOperationException($"Generated sprite '{fileName}' does not exist.");
        return sprite;
    }

    private static void SetObjectReference(
        UnityEngine.Object target,
        string propertyName,
        UnityEngine.Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException($"Missing property '{propertyName}' on {target.name}.");
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetFloat(UnityEngine.Object target, string propertyName, float value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException($"Missing property '{propertyName}' on {target.name}.");
        property.floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetLayerMask(UnityEngine.Object target, string propertyName, int value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException($"Missing property '{propertyName}' on {target.name}.");
        property.intValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder(GeneratedRoot))
            AssetDatabase.CreateFolder("Assets", "CodexGenerated");
        if (!AssetDatabase.IsValidFolder(PrefabRoot))
            AssetDatabase.CreateFolder("Assets", "Prefabs");
    }
}
#endif
