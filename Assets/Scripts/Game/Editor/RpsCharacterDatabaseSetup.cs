using System.Collections.Generic;
using System.IO;
using System.Linq;
using TTTXO.Game.UI.Screens;
using UnityEditor;
using UnityEngine;

namespace TTTXO.Game.Editor
{
    /// <summary>
    /// Batch-mode utility to create and populate the RpsCharacterDatabase asset
    /// from the already-sliced spritesheets. Extracts individual frame Texture2Ds
    /// and saves them as assets so they work at runtime (spritesheets are not readable).
    /// Run via:
    ///   Unity -batchmode -executeMethod TTTXO.Game.Editor.RpsCharacterDatabaseSetup.CreateDatabase -quit
    /// </summary>
    public static class RpsCharacterDatabaseSetup
    {
        private const string DatabasePath = "Assets/Resources/RpsCharacterDatabase.asset";
        private const string FramesFolder = "Assets/Resources/RPSFrames";
        private const string CaballeroAtlasPath = "Assets/Art/SpriteSheet/Hoja de sprites de caballero pixelado.png";
        private const string MagaAtlasPath = "Assets/Art/SpriteSheet/Hoja de sprites de maga pixel art.png";

        [MenuItem("TTTXO/Setup/Create RPS Character Database")]
        public static void CreateDatabase()
        {
            Debug.Log("=== RpsCharacterDatabaseSetup.CreateDatabase START ===");
            try
            {
                EnsureFolder("Assets/Resources");
                EnsureFolder(FramesFolder + "/Caballero/Idle");
                EnsureFolder(FramesFolder + "/Caballero/Attack");
                EnsureFolder(FramesFolder + "/Caballero/Hurt");
                EnsureFolder(FramesFolder + "/Maga/Idle");
                EnsureFolder(FramesFolder + "/Maga/Attack");
                EnsureFolder(FramesFolder + "/Maga/Hurt");

                // Make atlases readable and reimport
                MakeReadableAndReimport(CaballeroAtlasPath);
                MakeReadableAndReimport(MagaAtlasPath);

                var database = AssetDatabase.LoadAssetAtPath<RpsCharacterDatabase>(DatabasePath);
                if (database == null)
                {
                    Debug.Log("Creating new RpsCharacterDatabase asset...");
                    database = ScriptableObject.CreateInstance<RpsCharacterDatabase>();
                    AssetDatabase.CreateAsset(database, DatabasePath);
                    Debug.Log($"Asset created at {DatabasePath}");
                }
                else
                {
                    Debug.Log("Existing RpsCharacterDatabase found, updating...");
                }

                // Load all sub-sprites from both atlases
                Debug.Log($"Loading sprites from {CaballeroAtlasPath}");
                var caballeroSprites = LoadAllSpritesFromAtlas(CaballeroAtlasPath);
                Debug.Log($"Caballero atlas: {caballeroSprites.Count} sprites loaded");

                Debug.Log($"Loading sprites from {MagaAtlasPath}");
                var magaSprites = LoadAllSpritesFromAtlas(MagaAtlasPath);
                Debug.Log($"Maga atlas: {magaSprites.Count} sprites loaded");

                if (caballeroSprites.Count == 0)
                {
                    Debug.LogError($"No sprites found in {CaballeroAtlasPath}. Is Sprite Mode = Multiple?");
                    return;
                }
                if (magaSprites.Count == 0)
                {
                    Debug.LogError($"No sprites found in {MagaAtlasPath}. Is Sprite Mode = Multiple?");
                    return;
                }

                // Caballero (Rock) - 8 Idle, 7 Attack, 8 Hurt
                database.Caballero_Idle = ExtractAndSaveFrames(caballeroSprites, "Guerrero_Idle_", 8, FramesFolder + "/Caballero/Idle");
                database.Caballero_Attack = ExtractAndSaveFrames(caballeroSprites, "Guerrero_Atack_", 7, FramesFolder + "/Caballero/Attack");
                database.Caballero_Hurt = ExtractAndSaveFrames(caballeroSprites, "Guerrero_Recibe_Damage_", 8, FramesFolder + "/Caballero/Hurt");

                // Maga (Paper) - 8 Idle, 8 Attack, 8 Hurt
                database.Maga_Idle = ExtractAndSaveFrames(magaSprites, "Maga_Idle_", 8, FramesFolder + "/Maga/Idle");
                database.Maga_Attack = ExtractAndSaveFrames(magaSprites, "Maga_Atack_", 8, FramesFolder + "/Maga/Attack");
                database.Maga_Hurt = ExtractAndSaveFrames(magaSprites, "Maga_Recibe_Damage_", 8, FramesFolder + "/Maga/Hurt");

                // Asesino (Scissors) - leave empty for now

                EditorUtility.SetDirty(database);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"=== RpsCharacterDatabase created at {DatabasePath} ===");
                Debug.Log($"  Caballero: Idle={database.Caballero_Idle?.Length ?? 0}, Attack={database.Caballero_Attack?.Length ?? 0}, Hurt={database.Caballero_Hurt?.Length ?? 0}");
                Debug.Log($"  Maga: Idle={database.Maga_Idle?.Length ?? 0}, Attack={database.Maga_Attack?.Length ?? 0}, Hurt={database.Maga_Hurt?.Length ?? 0}");
                Debug.Log("=== RpsCharacterDatabaseSetup.CreateDatabase END ===");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"RpsCharacterDatabaseSetup failed: {ex}");
                throw;
            }
        }

        private static void MakeReadableAndReimport(string atlasPath)
        {
            var importer = AssetImporter.GetAtPath(atlasPath) as TextureImporter;
            if (importer != null && !importer.isReadable)
            {
                Debug.Log($"Making {atlasPath} readable...");
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
        }

        private static Dictionary<string, Sprite> LoadAllSpritesFromAtlas(string atlasPath)
        {
            var result = new Dictionary<string, Sprite>();
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(atlasPath);
            if (allAssets == null)
            {
                Debug.LogWarning($"LoadAllAssetsAtPath returned null for {atlasPath}");
                return result;
            }

            Debug.Log($"LoadAllAssetsAtPath returned {allAssets.Length} assets for {atlasPath}");
            foreach (var asset in allAssets)
            {
                if (asset is Sprite sprite && sprite != null)
                {
                    result[sprite.name] = sprite;
                }
            }
            return result;
        }

        private static Texture2D[] ExtractAndSaveFrames(Dictionary<string, Sprite> atlas, string prefix, int count, string outputFolder)
        {
            var list = new List<Texture2D>(count);
            for (int i = 1; i <= count; i++)
            {
                string name = $"{prefix}{i}";
                if (atlas.TryGetValue(name, out var sprite))
                {
                    var tex = ExtractFrameAsTexture2D(sprite);
                    if (tex != null)
                    {
                        string assetPath = $"{outputFolder}/{name}.asset";
                        AssetDatabase.CreateAsset(tex, assetPath);
                        list.Add(tex);
                        Debug.Log($"  Extracted & saved: {name} -> {assetPath}");
                    }
                }
                else
                {
                    Debug.LogWarning($"Sprite not found: {name} (atlas has {atlas.Count} sprites)");
                }
            }
            return list.ToArray();
        }

        private static Texture2D ExtractFrameAsTexture2D(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return null;

            var rect = sprite.rect;
            int w = Mathf.RoundToInt(rect.width);
            int h = Mathf.RoundToInt(rect.height);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            // Use the readable atlas texture
            var oldActive = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(sprite.texture, rt, new Vector2(rect.x / sprite.texture.width, rect.y / sprite.texture.height), Vector2.zero);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = oldActive;
            RenderTexture.ReleaseTemporary(rt);

            return tex;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folderName = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}