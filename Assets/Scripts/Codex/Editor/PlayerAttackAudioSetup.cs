#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class PlayerAttackAudioSetup
{
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";

    [MenuItem("Tools/Codex/Configure Player Attack Audio")]
    public static void ConfigurePlayerAttackAudio()
    {
        AudioClip normalAttack = LoadClip("Assets/Audio/Codex/normal_attack.wav");
        AudioClip stoneAttack = LoadClip("Assets/Audio/Codex/stone_attack.mp3");
        AudioClip slowAttack = LoadClip("Assets/Audio/Codex/slow_attack.mp3");

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            PlayerCombat combat = root.GetComponent<PlayerCombat>();
            PlayerSkill skill = root.GetComponent<PlayerSkill>();
            if (combat == null || skill == null)
                throw new InvalidOperationException("Player.prefab requires PlayerCombat and PlayerSkill.");

            SetAudioClip(combat, "normalAttackSound", normalAttack);
            SetAudioClip(combat, "stoneAttackSound", stoneAttack);
            SetAudioClip(skill, "slowAttackSound", slowAttack);
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[PlayerAttackAudioSetup] Player attack audio configured.");
    }

    private static AudioClip LoadClip(string path)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null)
            throw new InvalidOperationException($"Audio clip was not found: {path}");
        return clip;
    }

    private static void SetAudioClip(UnityEngine.Object target, string propertyName, AudioClip clip)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException($"Missing property '{propertyName}' on {target.name}.");

        property.objectReferenceValue = clip;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
