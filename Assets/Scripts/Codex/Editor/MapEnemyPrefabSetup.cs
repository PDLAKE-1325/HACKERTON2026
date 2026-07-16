#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MapEnemyPrefabSetup
{
    private const string PrefabPath = "Assets/Prefabs/Enemy.prefab";

    [MenuItem("Tools/Codex/Create Map Enemy Prefab")]
    public static void CreateMapEnemyPrefab()
    {
        if (EditorSceneManager.GetActiveScene().name != "Map")
            throw new InvalidOperationException("Map scene must be active.");

        GameObject enemy = GameObject.Find("Enemy");
        if (enemy == null)
            throw new InvalidOperationException("Enemy was not found in the Map scene.");

        EnemyBase enemyBase = enemy.GetComponent<EnemyBase>();
        if (enemyBase == null)
            throw new InvalidOperationException("Enemy requires an EnemyBase component.");

        SerializedObject serializedEnemy = new SerializedObject(enemyBase);
        SerializedProperty playerSkill = serializedEnemy.FindProperty("playerSkill");
        if (playerSkill != null)
        {
            playerSkill.objectReferenceValue = null;
            serializedEnemy.ApplyModifiedPropertiesWithoutUndo();
        }

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(
            enemy,
            PrefabPath,
            InteractionMode.UserAction);
        if (prefab == null)
            throw new InvalidOperationException("Failed to create the Enemy prefab.");

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log($"[MapEnemyPrefabSetup] Enemy prefab created: {PrefabPath}");
    }
}
#endif
