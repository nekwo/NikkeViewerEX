using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using NikkeViewerEX.Components;
using NikkeViewerEX.Serialization;
using NikkeViewerEX.Utils;
using UnityEngine;
using UnityEngine.UIElements;

namespace NikkeViewerEX.UI
{
    public partial class NikkeBrowserPanel
    {
        #region Public API
        public void SwapVariation(string characterId, int variationIndex = -1)
        {
            if (!activeViewers.TryGetValue(characterId, out NikkeViewerBase viewer))
            {
                Debug.LogWarning($"No active viewer for {characterId}");
                return;
            }
            if (!resolvedAssets.TryGetValue(characterId, out CharacterAssetInfo assetInfo))
            {
                Debug.LogWarning($"No resolved assets for {characterId}");
                return;
            }
            if (assetInfo.VariationCount <= 1)
            {
                Debug.Log($"{characterId} has no texture variations to swap");
                return;
            }

            int current = currentVariation.GetValueOrDefault(characterId, 0);
            int target = variationIndex < 0
                ? (current + 1) % assetInfo.VariationCount
                : variationIndex % assetInfo.VariationCount;

            currentVariation[characterId] = target;
            List<string> textures = assetInfo.GetTextures(target);
            SwapViewerTexture(viewer, textures).Forget();
            viewer.NikkeData.TexturesPath = textures;
            settingsManager.SaveSettings().Forget();

            Debug.Log($"Swapped {characterId} to variation {target}/{assetInfo.VariationCount} ({Path.GetFileName(textures[0])})");
        }

        public int GetVariationCount(string characterId) =>
            resolvedAssets.TryGetValue(characterId, out CharacterAssetInfo info) ? info.VariationCount : 0;

        public int GetCurrentVariation(string characterId) =>
            currentVariation.GetValueOrDefault(characterId, 0);
        #endregion

        #region Texture Swapping
        async UniTask SwapViewerTexture(NikkeViewerBase viewer, List<string> texturePaths)
        {
            var renderer = viewer.GetComponentInChildren<MeshRenderer>();
            if (renderer == null)
            {
                Debug.LogWarning("No MeshRenderer found on viewer");
                return;
            }
            if (texturePaths.Count == 0) return;

            try
            {
                byte[] data = await File.ReadAllBytesAsync(texturePaths[0]);
                Texture2D tex = new(2, 2);
                tex.LoadImage(data);
                tex.name = Path.GetFileNameWithoutExtension(texturePaths[0]);
                renderer.material.mainTexture = tex;

                if (texturePaths.Count > 1 && renderer.materials.Length > 1)
                {
                    var materials = renderer.materials;
                    for (int i = 1; i < texturePaths.Count && i < materials.Length; i++)
                    {
                        byte[] pageData = await File.ReadAllBytesAsync(texturePaths[i]);
                        Texture2D pageTex = new(2, 2);
                        pageTex.LoadImage(pageData);
                        pageTex.name = Path.GetFileNameWithoutExtension(texturePaths[i]);
                        materials[i].mainTexture = pageTex;
                    }
                    renderer.materials = materials;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to swap texture: {ex.Message}");
            }
        }
        #endregion

        #region Character Management
        void RebuildActiveViewers()
        {
            activeViewers.Clear();
            var viewers = FindObjectsByType<NikkeViewerBase>(FindObjectsSortMode.None);
            foreach (var viewer in viewers)
            {
                string id = viewer.NikkeData.AssetName;
                if (!string.IsNullOrEmpty(id))
                    activeViewers[id] = viewer;
            }
        }

        bool IsCharacterActive(string id) =>
            settingsManager.NikkeSettings.NikkeList.Exists(n => n.AssetName == id);

        async void AddCharacter(
            NikkeDatabaseEntry entry,
            Button addBtn,
            Label addedLabel,
            VisualElement itemRoot
        )
        {
            if (IsCharacterActive(entry.id)) return;

            if (!resolvedAssets.TryGetValue(entry.id, out CharacterAssetInfo assetInfo) || !assetInfo.IsValid)
            {
                Debug.LogError($"No valid assets for {entry.id}");
                return;
            }

            addBtn.SetEnabled(false);
            addBtn.text = "...";

            try
            {
                NikkeViewerBase viewer = await mainControl.InstantiateViewer(assetInfo.SkelPath);
                if (viewer == null)
                {
                    addBtn.SetEnabled(true);
                    addBtn.text = "Add";
                    return;
                }

                List<string> defaultTextures = assetInfo.GetTextures(0);
                List<string> voicesPaths = CharacterAssetResolver.FindTouchSounds(
                    assetsFolderInput.value, entry.id);

                var poses = new List<NikkePose>();
                foreach (NikkePoseType poseType in assetInfo.AvailablePoses)
                {
                    var poseAsset = assetInfo.Poses[poseType];
                    poses.Add(new NikkePose
                    {
                        PoseType = poseType,
                        SkelPath = poseAsset.SkelPath,
                        AtlasPath = poseAsset.AtlasPath,
                        TexturesPath = poseAsset.GetTextures(0),
                    });
                }

                viewer.NikkeData = new Nikke
                {
                    NikkeName = entry.name,
                    AssetName = entry.id,
                    SkelPath = assetInfo.SkelPath,
                    AtlasPath = assetInfo.AtlasPath,
                    TexturesPath = defaultTextures,
                    VoicesSource = new List<string>(),
                    VoicesPath = voicesPaths,
                    Poses = poses,
                    ActivePose = NikkePoseType.Base,
                };

                if (voicesPaths.Count > 0)
                {
                    var clips = new List<UnityEngine.AudioClip>();
                    foreach (string path in voicesPaths)
                    {
                        var clip = await WebRequestHelper.GetAudioClip(path);
                        if (clip != null)
                            clips.Add(clip);
                    }
                    viewer.TouchVoices = clips;
                }
                viewer.name = entry.name;
                viewer.TriggerSpawn();

                activeViewers[entry.id] = viewer;
                currentVariation[entry.id] = 0;
                settingsManager.NikkeSettings.NikkeList.Add(viewer.NikkeData);
                await settingsManager.SaveSettings();

                addBtn.style.display = DisplayStyle.None;
                addedLabel.style.display = DisplayStyle.Flex;
                itemRoot.AddToClassList("character-added");

                UpdateBrowserCount();
                Debug.Log($"Added character: {entry.name} ({entry.id}), skel: {assetInfo.SkelPath}, {assetInfo.VariationCount} texture variations");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to add character {entry.id}: {ex.Message}");
                addBtn.SetEnabled(true);
                addBtn.text = "Add";
            }
        }

        async void RemoveCharacter(string assetName)
        {
            if (activeViewers.TryGetValue(assetName, out NikkeViewerBase viewer))
            {
                if (viewer != null)
                {
                    if (viewer.NikkeNameText != null)
                        Destroy(viewer.NikkeNameText.gameObject);
                    Destroy(viewer.gameObject);
                }
                activeViewers.Remove(assetName);
            }
            else
            {
                var allViewers = FindObjectsByType<NikkeViewerBase>(FindObjectsSortMode.None);
                foreach (var v in allViewers)
                {
                    if (v.NikkeData.AssetName == assetName)
                    {
                        if (v.NikkeNameText != null)
                            Destroy(v.NikkeNameText.gameObject);
                        Destroy(v.gameObject);
                        break;
                    }
                }
            }

            currentVariation.Remove(assetName);
            settingsManager.NikkeSettings.NikkeList.RemoveAll(n => n.AssetName == assetName);
            await settingsManager.SaveSettings();

            foreach (var (element, entry) in browserItems)
            {
                if (entry.id == assetName)
                {
                    VisualElement itemRoot = element.Q("character-item");
                    Button addBtn = element.Q<Button>("add-button");
                    Label addedLabel = element.Q<Label>("added-label");

                    addBtn.style.display = DisplayStyle.Flex;
                    addBtn.SetEnabled(true);
                    addBtn.text = "Add";
                    addedLabel.style.display = DisplayStyle.None;
                    itemRoot.RemoveFromClassList("character-added");
                    break;
                }
            }

            UpdateBrowserCount();
            Debug.Log($"Removed character: {assetName}");
        }
        #endregion
    }
}
