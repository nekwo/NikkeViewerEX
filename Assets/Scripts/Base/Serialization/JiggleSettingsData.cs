using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NikkeViewerEX.Serialization
{
    [Serializable]
    public class JiggleBoneSettings
    {
        public string BoneName;
        public float Stiffness = 30f;
        public float Damping = 4f;
        public float ForceFactor = 3f;
        public float MaxRotDisplacement = 12f;
        public float PosStiffness = 25f;
        public float PosDamping = 3.5f;
        public float PosForceFactor = 2f;
        public float MaxPosDisplacement = 4f;
    }

    [Serializable]
    public class JigglePattern
    {
        public string Keyword;
        public float Stiffness;
        public float Damping;
        public float ForceFactor;
        public float MaxRotDisplacement;
        public float PosStiffness;
        public float PosDamping;
        public float PosForceFactor;
        public float MaxPosDisplacement;
    }

    [Serializable]
    public class JiggleCharacterSettings
    {
        public string AssetName;
        public bool Enabled = true;
        public List<JiggleBoneSettings> Bones = new();
        public List<JigglePattern> AutoPatterns = new();
    }

    [Serializable]
    public class JiggleSettingsFile
    {
        public bool GlobalEnabled = true;
        public List<JigglePattern> DefaultPatterns = new()
        {
            new() { Keyword = "thigh", Stiffness = 18f, Damping = 0.5f, ForceFactor = 10f, MaxRotDisplacement = 30f, PosStiffness = 60f, PosDamping = 0.1f, PosForceFactor = 4f, MaxPosDisplacement = 5f },
            new() { Keyword = "hip", Stiffness = 20f, Damping = 0.6f, ForceFactor = 9f, MaxRotDisplacement = 25f, PosStiffness = 65f, PosDamping = 0.1f, PosForceFactor = 3.5f, MaxPosDisplacement = 4f },
            new() { Keyword = "skirt", Stiffness = 25f, Damping = 2.5f, ForceFactor = 5f, MaxRotDisplacement = 8f, PosStiffness = 50f, PosDamping = 4f, PosForceFactor = 1.5f, MaxPosDisplacement = 1f },
        };
        public List<JiggleCharacterSettings> Characters = new();
    }

    public static class JiggleSettingsManager
    {
        const string FileName = "jiggle.json";
        static string FilePath => Path.Combine(Application.dataPath, "..", FileName);
        static JiggleSettingsFile cached;

        public static JiggleSettingsFile Load()
        {
            if (cached != null) return cached;
            try
            {
                string path = FilePath;
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    cached = JsonUtility.FromJson<JiggleSettingsFile>(json);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Jiggle] Failed to load {FileName}: {ex.Message}");
            }
            cached ??= new JiggleSettingsFile();
            return cached;
        }

        public static void Save()
        {
            try
            {
                cached ??= new JiggleSettingsFile();
                string json = JsonUtility.ToJson(cached, true);
                File.WriteAllText(FilePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Jiggle] Failed to save {FileName}: {ex.Message}");
            }
        }

        public static JiggleCharacterSettings GetForCharacter(string assetName)
        {
            var file = Load();
            return file.Characters.Find(c => c.AssetName == assetName);
        }

        public static List<JigglePattern> GetPatterns(JiggleCharacterSettings character)
        {
            var file = Load();
            if (character != null && character.AutoPatterns.Count > 0)
                return character.AutoPatterns;
            return file.DefaultPatterns;
        }
    }
}
