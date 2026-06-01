using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using LoveAlgo.Modules.Audio;
using LoveAlgo.UI;

namespace LoveAlgo.Editor
{
    public static class SceneSimplifier
    {
        [MenuItem("Tools/LoveAlgorithm/Simplify Scene Managers")]
        public static void SimplifySceneManagers()
        {
            // 1. Find UIManager
            var uiManager = Object.FindAnyObjectByType<UIManager>();
            if (uiManager == null)
            {
                Debug.LogError("[SceneSimplifier] UIManager not found in the scene.");
                EditorUtility.DisplayDialog("Error", "UIManager not found in the scene.", "OK");
                return;
            }

            // 2. Find AudioManager
            var audioManager = Object.FindAnyObjectByType<AudioManager>();
            if (audioManager == null)
            {
                Debug.LogError("[SceneSimplifier] AudioManager not found in the scene.");
                EditorUtility.DisplayDialog("Error", "AudioManager not found in the scene.", "OK");
                return;
            }

            // 3. Find UISoundManager
            var uiSoundManager = Object.FindAnyObjectByType<UISoundManager>();
            bool migratedAudio = false;
            if (uiSoundManager != null)
            {
                Undo.RecordObject(audioManager, "Migrate UISoundManager Settings");
                
                // Copy values using SerializedObject
                SerializedObject destSO = new SerializedObject(audioManager);
                
                // Audio clips
                destSO.FindProperty("hoverClip").objectReferenceValue = uiSoundManager.hoverClip;
                destSO.FindProperty("clickClip").objectReferenceValue = uiSoundManager.clickClip;
                destSO.FindProperty("typingClip").objectReferenceValue = uiSoundManager.typingClip;
                destSO.FindProperty("dialogueNextClip").objectReferenceValue = uiSoundManager.dialogueNextClip;
                destSO.FindProperty("choiceSelectClip").objectReferenceValue = uiSoundManager.choiceSelectClip;
                destSO.FindProperty("choiceAppearClip").objectReferenceValue = uiSoundManager.choiceAppearClip;
                destSO.FindProperty("choiceHoverClip").objectReferenceValue = uiSoundManager.choiceHoverClip;
                destSO.FindProperty("popupOpenClip").objectReferenceValue = uiSoundManager.popupOpenClip;
                destSO.FindProperty("popupCloseClip").objectReferenceValue = uiSoundManager.popupCloseClip;
                destSO.FindProperty("saveCompleteClip").objectReferenceValue = uiSoundManager.saveCompleteClip;
                destSO.FindProperty("loadCompleteClip").objectReferenceValue = uiSoundManager.loadCompleteClip;
                destSO.FindProperty("volumePreviewClip").objectReferenceValue = uiSoundManager.volumePreviewClip;
                
                // Mixer and volumes
                destSO.FindProperty("sfxMixerGroup").objectReferenceValue = uiSoundManager.sfxMixerGroup;
                destSO.FindProperty("hoverVolume").floatValue = uiSoundManager.hoverVolume;
                destSO.FindProperty("clickVolume").floatValue = uiSoundManager.clickVolume;
                
                // Typing and preview
                destSO.FindProperty("minTypingPitch").floatValue = uiSoundManager.minTypingPitch;
                destSO.FindProperty("maxTypingPitch").floatValue = uiSoundManager.maxTypingPitch;
                destSO.FindProperty("minTypingVolume").floatValue = uiSoundManager.minTypingVolume;
                destSO.FindProperty("maxTypingVolume").floatValue = uiSoundManager.maxTypingVolume;
                destSO.FindProperty("typingMinInterval").floatValue = uiSoundManager.typingMinInterval;
                destSO.FindProperty("volumePreviewDebounce").floatValue = uiSoundManager.volumePreviewDebounce;
                destSO.FindProperty("autoBindButtons").boolValue = uiSoundManager.autoBindButtons;

                // Handle AudioSource components
                var srcAudioSource = uiSoundManager.GetComponent<AudioSource>();
                
                // Check if target properties already contain valid AudioSources
                var uiAudioSourceProp = destSO.FindProperty("uiAudioSource");
                var uiTypingSourceProp = destSO.FindProperty("uiTypingSource");
                
                AudioSource uiAudioSource = uiAudioSourceProp.objectReferenceValue as AudioSource;
                AudioSource uiTypingSource = uiTypingSourceProp.objectReferenceValue as AudioSource;

                if (uiAudioSource == null)
                {
                    uiAudioSource = Undo.AddComponent<AudioSource>(audioManager.gameObject);
                    uiAudioSource.playOnAwake = false;
                    uiAudioSource.priority = 0;
                    if (srcAudioSource != null)
                    {
                        uiAudioSource.volume = srcAudioSource.volume;
                        uiAudioSource.pitch = srcAudioSource.pitch;
                        uiAudioSource.outputAudioMixerGroup = srcAudioSource.outputAudioMixerGroup;
                    }
                    else if (uiSoundManager.sfxMixerGroup != null)
                    {
                        uiAudioSource.outputAudioMixerGroup = uiSoundManager.sfxMixerGroup;
                    }
                    uiAudioSourceProp.objectReferenceValue = uiAudioSource;
                }
                
                if (uiTypingSource == null)
                {
                    uiTypingSource = Undo.AddComponent<AudioSource>(audioManager.gameObject);
                    uiTypingSource.playOnAwake = false;
                    uiTypingSource.priority = 0;
                    if (uiSoundManager.sfxMixerGroup != null)
                    {
                        uiTypingSource.outputAudioMixerGroup = uiSoundManager.sfxMixerGroup;
                    }
                    uiTypingSourceProp.objectReferenceValue = uiTypingSource;
                }

                destSO.ApplyModifiedProperties();
                EditorUtility.SetDirty(audioManager);
                
                // Delete UISoundManager GameObject
                var uismGo = uiSoundManager.gameObject;
                Undo.DestroyObjectImmediate(uismGo);
                migratedAudio = true;
                Debug.Log("[SceneSimplifier] Successfully migrated UISoundManager settings to AudioManager.");
            }
            else
            {
                Debug.Log("[SceneSimplifier] UISoundManager not found in scene. Skipping audio migration.");
            }

            // 4. Find PopupSystem
            var popupSystem = Object.FindAnyObjectByType<PopupSystem>();
            bool migratedPopup = false;
            if (popupSystem != null)
            {
                Undo.RecordObject(uiManager, "Assign PopupSystem to UIManager");
                
                // Parent under UIManager
                if (popupSystem.transform.parent != uiManager.transform)
                {
                    Undo.SetTransformParent(popupSystem.transform, uiManager.transform, "Parent PopupSystem to UIManager");
                }

                // Rename GameObject if it is "PopupManager"
                if (popupSystem.gameObject.name == "PopupManager")
                {
                    Undo.RecordObject(popupSystem.gameObject, "Rename PopupSystem GameObject");
                    popupSystem.gameObject.name = "PopupSystem";
                }

                // Assign reference
                SerializedObject uiSO = new SerializedObject(uiManager);
                uiSO.FindProperty("popupSystem").objectReferenceValue = popupSystem;
                uiSO.ApplyModifiedProperties();

                EditorUtility.SetDirty(uiManager);
                EditorUtility.SetDirty(popupSystem.gameObject);
                migratedPopup = true;
                Debug.Log("[SceneSimplifier] Successfully parented PopupSystem to UIManager.");
            }
            else
            {
                Debug.LogWarning("[SceneSimplifier] PopupSystem not found in scene.");
            }

            // Save Scene if changes were made
            if (migratedAudio || migratedPopup)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                EditorUtility.DisplayDialog("Success", "Scene simplified successfully!\n" +
                    $"- UISoundManager migrated: {migratedAudio}\n" +
                    $"- PopupSystem parented/assigned: {migratedPopup}", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Info", "No managers were found to simplify, or scene is already simplified.", "OK");
            }
        }
    }
}
