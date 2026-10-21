using System.Collections.Generic;
using System.IO;
using System.Linq;
using TTTXO.Game.UI.Screens;
using UnityEditor;
using UnityEngine;

namespace TTTXO.Game.Editor
{
    /// <summary>
    /// Batch-mode utility to create and populate the RpsCharacterDatabase asset from the sliced
    /// spritesheets. Every frame is copied onto a per-character canvas of the same size, anchored
    /// bottom-center, so the character does not jump or change scale when the animation swaps
    /// textures at runtime.
    /// Run via:
    ///   Unity -batchmode -executeMethod TTTXO.Game.Editor.RpsCharacterDatabaseSetup.CreateDatabase -quit
    /// or from the menu TTTXO/Setup/Create RPS Character Database.
    /// </summary>
    public static class RpsCharacterDatabaseSetup
    {
        private const string DatabasePath = "Assets/Resources/RpsCharacterDatabase.asset";
        private const string FramesFolder = "Assets/Resources/RPSFrames";
        private const string CaballeroAtlasPath = "Assets/Art/SpriteSheet/Hoja de sprites de caballero pixelado.png";
        private const string MagaAtlasPath = "Assets/Art/SpriteSheet/Hoja de sprites de maga pixel art.png";
        private const string AsesinoAtlasPath = "Assets/Art/SpriteSheet/Hoja de sprites de asesino encapuchado.png";

        private sealed class ExtractedFrame
        {
            public string Name;
            public Texture2D Texture;
        }

        [MenuItem("TTTXO/Setup/Create RPS Character Database")]
        public static void CreateDatabase()
        {
            Debug.Log("=== RpsCharacterDatabaseSetup.CreateDatabase START ===");
            try
            {
                EnsureFolder("Assets/Resources");
                EnsureCharacterFolders("Caballero");
                EnsureCharacterFolders("Maga");
                EnsureCharacterFolders("Asesino");

                MakeReadableAndReimport(CaballeroAtlasPath);
                MakeReadableAndReimport(MagaAtlasPath);
                MakeReadableAndReimport(AsesinoAtlasPath);

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

                BuildCharacter(database, "Caballero", CaballeroAtlasPath,
                    "Guerrero_Idle_", 8, "Guerrero_Atack_", 7, "Guerrero_Recibe_Damage_", 8);

                BuildCharacter(database, "Maga", MagaAtlasPath,
                    "Maga_Idle_", 8, "Maga_Atack_", 8, "Maga_Recibe_Damage_", 8);

                BuildCharacter(database, "Asesino", AsesinoAtlasPath,
                    "Asesino_Idle_", 8, "Asesino_Attack_", 8, "Asesino_Recibe_Damage_", 8);

                EditorUtility.SetDirty(database);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"=== RpsCharacterDatabase updated at {DatabasePath} ===");
                Debug.Log($"  Guerrero: Idle={database.Caballero_Idle?.Length ?? 0}, Attack={database.Caballero_Attack?.Length ?? 0}, Hurt={database.Caballero_Hurt?.Length ?? 0}");
                Debug.Log($"  Maga: Idle={database.Maga_Idle?.Length ?? 0}, Attack={database.Maga_Attack?.Length ?? 0}, Hurt={database.Maga_Hurt?.Length ?? 0}");
                Debug.Log($"  Asesino: Idle={database.Asesino_Idle?.Length ?? 0}, Attack={database.Asesino_Attack?.Length ?? 0}, Hurt={database.Asesino_Hurt?.Length ?? 0}");
                Debug.Log("=== RpsCharacterDatabaseSetup.CreateDatabase END ===");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"RpsCharacterDatabaseSetup failed: {ex}");
                throw;
            }
        }

        private static void BuildCharacter(
            RpsCharacterDatabase database,
            string characterFolder,
            string atlasPath,
            string idlePrefix, int idleCount,
            string attackPrefix, int attackCount,
            string hurtPrefix, int hurtCount)
        {
            Debug.Log($"Loading sprites from {atlasPath}");
            var sprites = LoadAllSpritesFromAtlas(atlasPath);
            Debug.Log($"{characterFolder} atlas: {sprites.Count} sprites loaded");
            if (sprites.Count == 0)
            {
                Debug.LogError($"No sprites found in {atlasPath}. Is Sprite Mode = Multiple?");
                return;
            }

            var idle = ExtractFrames(sprites, idlePrefix, idleCount);
            var attack = ExtractFrames(sprites, attackPrefix, attackCount);
            var hurt = ExtractFrames(sprites, hurtPrefix, hurtCount);

            var all = idle.Concat(attack).Concat(hurt).ToList();
            if (all.Count == 0)
            {
                Debug.LogError($"{characterFolder}: no frames matched the expected names.");
                return;
            }

            // One canvas for the whole character: the largest frame in any clip. Every frame is
            // bottom-center aligned inside it, which is what keeps the feet planted across clips.
            int canvasWidth = all.Max(f => f.Texture.width);
            int canvasHeight = all.Max(f => f.Texture.height);

            var idleAssets = SaveFrames(idle, FramesFolder + "/" + characterFolder + "/Idle", canvasWidth, canvasHeight);
            var attackAssets = SaveFrames(attack, FramesFolder + "/" + characterFolder + "/Attack", canvasWidth, canvasHeight);
            var hurtAssets = SaveFrames(hurt, FramesFolder + "/" + characterFolder + "/Hurt", canvasWidth, canvasHeight);

            switch (characterFolder)
            {
                case "Caballero":
                    database.Caballero_Idle = idleAssets;
                    database.Caballero_Attack = attackAssets;
                    database.Caballero_Hurt = hurtAssets;
                    break;
                case "Maga":
                    database.Maga_Idle = idleAssets;
                    database.Maga_Attack = attackAssets;
                    database.Maga_Hurt = hurtAssets;
                    break;
                case "Asesino":
                    database.Asesino_Idle = idleAssets;
                    database.Asesino_Attack = attackAssets;
                    database.Asesino_Hurt = hurtAssets;
                    break;
            }

            foreach (var frame in all)
            {
                Object.DestroyImmediate(frame.Texture);
            }

            Debug.Log($"  {characterFolder}: canvas {canvasWidth}x{canvasHeight}, frames idle={idleAssets.Length}, attack={attackAssets.Length}, hurt={hurtAssets.Length}");
        }

        private static void EnsureCharacterFolders(string character)
        {
            EnsureFolder(FramesFolder + "/" + character + "/Idle");
            EnsureFolder(FramesFolder + "/" + character + "/Attack");
            EnsureFolder(FramesFolder + "/" + character + "/Hurt");
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

            foreach (var asset in allAssets)
            {
                if (asset is Sprite sprite && sprite != null)
                {
                    result[sprite.name] = sprite;
                }
            }

            return result;
        }

        private static List<ExtractedFrame> ExtractFrames(Dictionary<string, Sprite> atlas, string prefix, int count)
        {
            var frames = new List<ExtractedFrame>(count);
            for (int i = 1; i <= count; i++)
            {
                string name = $"{prefix}{i}";
                if (!atlas.TryGetValue(name, out var sprite))
                {
                    Debug.LogWarning($"Sprite not found: {name} (atlas has {atlas.Count} sprites)");
                    continue;
                }

                var texture = ExtractFrameAsTexture2D(sprite);
                if (texture != null)
                {
                    frames.Add(new ExtractedFrame { Name = name, Texture = texture });
                }
            }

            return frames;
        }

        private static Texture2D[] SaveFrames(List<ExtractedFrame> frames, string outputFolder, int canvasWidth, int canvasHeight)
        {
            var saved = new List<Texture2D>(frames.Count);
            foreach (var frame in frames)
            {
                var canvas = new Texture2D(canvasWidth, canvasHeight, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };

                // Transparent background, then paste the frame with its bottom edge on the canvas
                // bottom and its horizontal center on the canvas center.
                canvas.SetPixels32(new Color32[canvasWidth * canvasHeight]);
                int x = (canvasWidth - frame.Texture.width) / 2;
                int y = 0;
                canvas.SetPixels32(x, y, frame.Texture.width, frame.Texture.height, frame.Texture.GetPixels32());
                canvas.Apply();

                string assetPath = $"{outputFolder}/{frame.Name}.asset";
                AssetDatabase.DeleteAsset(assetPath);
                AssetDatabase.CreateAsset(canvas, assetPath);
                saved.Add(canvas);
                Debug.Log($"  Saved: {assetPath}");
            }

            return saved.ToArray();
        }

        private static Texture2D ExtractFrameAsTexture2D(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
            {
                return null;
            }

            var rect = sprite.rect;
            int x = Mathf.RoundToInt(rect.x);
            int y = Mathf.RoundToInt(rect.y);
            int w = Mathf.RoundToInt(rect.width);
            int h = Mathf.RoundToInt(rect.height);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            // Read pixels directly from the readable atlas texture (CPU path - works in batch mode)
            Color[] pixels = sprite.texture.GetPixels(x, y, w, h);
            tex.SetPixels(pixels);
            tex.Apply();

            return tex;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folderName = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
