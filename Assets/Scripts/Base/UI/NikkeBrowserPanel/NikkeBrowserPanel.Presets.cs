using System;
using System.Collections.Generic;
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
        TextField presetNameInput;
        ScrollView presetList;
        VisualElement presetEmpty;

        void QueryPresetElements()
        {
            presetNameInput = root.Q<TextField>("preset-name-input");
            presetList = root.Q<ScrollView>("preset-list");
            presetEmpty = root.Q("preset-empty");
        }

        void BindPresetEvents()
        {
            root.Q<Button>("preset-save-button").clicked += SaveCurrentPreset;
        }

        void SaveCurrentPreset()
        {
            string name = presetNameInput.value?.Trim();
            if (string.IsNullOrEmpty(name)) return;

            var nikkeList = settingsManager.NikkeSettings.NikkeList;
            if (nikkeList.Count == 0) return;

            var snapshot = new List<Nikke>();
            foreach (var nikke in nikkeList)
                snapshot.Add(JsonUtility.FromJson<Nikke>(JsonUtility.ToJson(nikke)));

            settingsManager.NikkeSettings.Presets.Add(new NikkePreset { Name = name, NikkeList = snapshot });
            settingsManager.SaveSettings().Forget();

            presetNameInput.value = "";
            RefreshPresetList();
        }

        void RefreshPresetList()
        {
            presetList.Clear();
            var presets = settingsManager.NikkeSettings.Presets;

            if (presets.Count == 0)
            {
                presetEmpty.style.display = DisplayStyle.Flex;
                presetList.style.display = DisplayStyle.None;
                return;
            }

            presetEmpty.style.display = DisplayStyle.None;
            presetList.style.display = DisplayStyle.Flex;

            foreach (var preset in presets)
            {
                var item = new VisualElement();
                item.AddToClassList("preset-item");

                var info = new VisualElement();
                info.AddToClassList("preset-info");

                var nameLabel = new Label(preset.Name);
                nameLabel.AddToClassList("preset-name");

                int count = preset.NikkeList.Count;
                var countLabel = new Label($"{count} character{(count != 1 ? "s" : "")}");
                countLabel.AddToClassList("preset-count");

                info.Add(nameLabel);
                info.Add(countLabel);

                var actions = new VisualElement();
                actions.AddToClassList("preset-actions");

                var loadBtn = new Button { text = "Load" };
                loadBtn.AddToClassList("preset-load-button");

                var deleteBtn = new Button { text = "✕" };
                deleteBtn.AddToClassList("preset-delete-button");

                NikkePreset captured = preset;
                loadBtn.clicked += () => LoadPreset(captured).Forget();
                deleteBtn.clicked += () =>
                {
                    settingsManager.NikkeSettings.Presets.Remove(captured);
                    settingsManager.SaveSettings().Forget();
                    RefreshPresetList();
                };

                actions.Add(loadBtn);
                actions.Add(deleteBtn);

                item.Add(info);
                item.Add(actions);
                presetList.Add(item);
            }
        }

        async UniTask LoadPreset(NikkePreset preset)
        {
            // Destroy all active viewers
            var allViewers = FindObjectsByType<NikkeViewerBase>(FindObjectsSortMode.None);
            foreach (var viewer in allViewers)
            {
                if (viewer.NikkeNameText != null)
                    Destroy(viewer.NikkeNameText.gameObject);
                Destroy(viewer.gameObject);
            }
            activeViewers.Clear();
            currentVariation.Clear();
            settingsManager.NikkeSettings.NikkeList.Clear();

            await UniTask.NextFrame();

            var newList = new List<Nikke>();

            foreach (var savedNikke in preset.NikkeList)
            {
                try
                {
                    NikkeViewerBase viewer = await mainControl.InstantiateViewer(savedNikke.SkelPath);
                    if (viewer == null) continue;

                    Nikke nikkeData = JsonUtility.FromJson<Nikke>(JsonUtility.ToJson(savedNikke));
                    viewer.NikkeData = nikkeData;
                    viewer.gameObject.transform.position = nikkeData.Position;
                    viewer.gameObject.transform.localScale = nikkeData.Scale;
                    viewer.name = string.IsNullOrEmpty(nikkeData.NikkeName)
                        ? nikkeData.AssetName
                        : nikkeData.NikkeName;

                    if (nikkeData.VoicesPath.Count > 0)
                    {
                        var clips = new List<AudioClip>();
                        foreach (string path in nikkeData.VoicesPath)
                        {
                            var clip = await WebRequestHelper.GetAudioClip(path);
                            if (clip != null) clips.Add(clip);
                        }
                        viewer.TouchVoices = clips;
                    }

                    viewer.TriggerSpawn();
                    activeViewers[nikkeData.AssetName] = viewer;
                    newList.Add(nikkeData);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to load '{savedNikke.AssetName}' from preset '{preset.Name}': {ex.Message}");
                }
            }

            settingsManager.NikkeSettings.NikkeList = newList;
            await settingsManager.SaveSettings();

            UpdateBrowserCount();
            PopulateBrowserList();
        }
    }
}
