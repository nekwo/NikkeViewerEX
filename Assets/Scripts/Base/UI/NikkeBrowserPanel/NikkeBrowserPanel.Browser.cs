using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using NikkeViewerEX.Serialization;
using UnityEngine;
using UnityEngine.UIElements;

namespace NikkeViewerEX.UI
{
    public partial class NikkeBrowserPanel
    {
        // Browser tab elements
        TextField searchInput;
        Label browserCount;
        ScrollView browserList;
        VisualElement browserEmpty;

        readonly List<(VisualElement element, NikkeDatabaseEntry entry)> browserItems = new();

        void QueryBrowserElements()
        {
            searchInput = root.Q<TextField>("search-input");
            browserCount = root.Q<Label>("browser-count");
            browserList = root.Q<ScrollView>("browser-list");
            browserEmpty = root.Q("browser-empty");
        }

        void BindBrowserEvents()
        {
            searchInput.RegisterValueChangedCallback(evt => FilterBrowserList(evt.newValue));
        }

        void UnbindBrowserEvents()
        {
            searchInput.UnregisterValueChangedCallback(evt => FilterBrowserList(evt.newValue));
        }

        void PopulateBrowserList()
        {
            browserList.Clear();
            browserItems.Clear();

            if (database == null || database.Length == 0)
            {
                browserEmpty.style.display = DisplayStyle.Flex;
                browserList.style.display = DisplayStyle.None;
                browserCount.text = "0 characters available";
                return;
            }

            browserEmpty.style.display = DisplayStyle.None;
            browserList.style.display = DisplayStyle.Flex;

            string thumbnailsFolder = thumbnailsFolderInput.value;

            foreach (var entry in database)
            {
                VisualElement item = m_BrowserItemTemplate.Instantiate();
                VisualElement itemRoot = item.Q("character-item");

                item.Q<Label>("character-name").text = entry.name;
                item.Q<Label>("character-id").text = entry.id;

                var assetInfo = resolvedAssets.GetValueOrDefault(entry.id);
                string versionText = entry.VersionLabel;
                if (assetInfo != null && assetInfo.VariationCount > 1)
                    versionText += $" | {assetInfo.VariationCount} textures";
                if (assetInfo != null && assetInfo.Poses.Count > 1)
                    versionText += $" | {assetInfo.Poses.Count} poses";
                item.Q<Label>("character-version").text = versionText;

                bool hasAssets = assetInfo is { IsValid: true };
                Label noAssetsLabel = item.Q<Label>("no-assets-label");
                Button addBtn = item.Q<Button>("add-button");
                Label addedLabel = item.Q<Label>("added-label");

                noAssetsLabel.text = hasAssets ? "" : "missing assets";
                addBtn.SetEnabled(hasAssets);

                bool isActive = IsCharacterActive(entry.id);
                addBtn.style.display = isActive ? DisplayStyle.None : DisplayStyle.Flex;
                addedLabel.style.display = isActive ? DisplayStyle.Flex : DisplayStyle.None;
                if (isActive)
                    itemRoot.AddToClassList("character-added");

                addBtn.clicked += () => AddCharacter(entry, addBtn, addedLabel, itemRoot);

                VisualElement thumbnailEl = item.Q("character-thumbnail");
                if (!string.IsNullOrEmpty(thumbnailsFolder))
                    LoadThumbnail(thumbnailEl, thumbnailsFolder, entry.id).Forget();
                else
                    thumbnailEl.AddToClassList("thumbnail-missing");

                browserList.Add(item);
                browserItems.Add((item, entry));
            }

            UpdateBrowserCount();
        }

        async UniTask LoadThumbnail(VisualElement thumbnailEl, string thumbnailsFolder, string characterId)
        {
            string path = NikkeViewerEX.Utils.CharacterAssetResolver.FindThumbnail(thumbnailsFolder, characterId);
            if (path == null)
            {
                thumbnailEl.AddToClassList("thumbnail-missing");
                return;
            }

            try
            {
                byte[] data = await File.ReadAllBytesAsync(path);
                Texture2D tex = new(2, 2);
                tex.LoadImage(data);
                tex.name = characterId;
                thumbnailEl.style.backgroundImage = new StyleBackground(tex);
                thumbnailEl.RemoveFromClassList("thumbnail-missing");
            }
            catch (Exception ex)
            {
                thumbnailEl.AddToClassList("thumbnail-missing");
                Debug.LogWarning($"Could not load thumbnail for {characterId}: {ex.Message}");
            }
        }

        void FilterBrowserList(string search)
        {
            if (browserItems.Count == 0) return;

            string filter = search?.ToLowerInvariant() ?? "";
            int visible = 0;

            foreach (var (element, entry) in browserItems)
            {
                bool match = string.IsNullOrEmpty(filter)
                    || entry.name.ToLowerInvariant().Contains(filter)
                    || entry.id.ToLowerInvariant().Contains(filter);

                element.style.display = match ? DisplayStyle.Flex : DisplayStyle.None;
                if (match) visible++;
            }

            browserCount.text = string.IsNullOrEmpty(filter)
                ? $"{browserItems.Count} characters available"
                : $"{visible} of {browserItems.Count} characters";
        }

        void UpdateBrowserCount()
        {
            int active = settingsManager.NikkeSettings.NikkeList.Count;
            browserCount.text = $"{browserItems.Count} characters available, {active} active";
            tabActiveBtn.text = $"Active ({active})";
        }
    }
}
