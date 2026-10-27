using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TTTXO.Core.Duel;
using UnityEngine;

namespace TTTXO.Game.Duel
{
    /// <summary>
    /// File-backed duel store used while the project runs without internet.
    ///
    /// One <c>&lt;match_id&gt;.json</c> per match under
    /// <c>Application.persistentDataPath/duels</c>, written with the same serializer the cloud
    /// adapter will use - so "subir el JSON" and "guardar el JSON" are the same document, which is
    /// what lets Phase 3 be replaced by Cloud Save without touching the rules or the screens.
    /// </summary>
    public sealed class LocalDuelStore : IDuelStore
    {
        private const string Extension = ".json";

        private readonly string _folder;

        public LocalDuelStore(string folder = null)
        {
            _folder = string.IsNullOrWhiteSpace(folder)
                ? Path.Combine(Application.persistentDataPath, "duels")
                : folder;
        }

        public string Folder => _folder;

        public IReadOnlyList<DuelStateDto> LoadAll()
        {
            var documents = new List<DuelStateDto>();

            if (!Directory.Exists(_folder))
            {
                return documents;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(_folder, "*" + Extension);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"LocalDuelStore: could not list '{_folder}' - {ex.Message}");
                return documents;
            }

            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file, Encoding.UTF8);
                    if (DuelStateSerializer.TryFromJson(json, out var state, out var error))
                    {
                        documents.Add(state);
                    }
                    else
                    {
                        Debug.LogWarning($"LocalDuelStore: skipped '{Path.GetFileName(file)}' - {error}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"LocalDuelStore: could not read '{Path.GetFileName(file)}' - {ex.Message}");
                }
            }

            return documents;
        }

        public void Save(DuelStateDto state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            try
            {
                Directory.CreateDirectory(_folder);
                File.WriteAllText(PathFor(state.match_id), DuelStateSerializer.ToJson(state), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.LogError($"LocalDuelStore: could not save match '{state.match_id}' - {ex.Message}");
            }
        }

        public void Delete(string matchId)
        {
            if (string.IsNullOrWhiteSpace(matchId))
            {
                return;
            }

            try
            {
                var path = PathFor(matchId);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"LocalDuelStore: could not delete match '{matchId}' - {ex.Message}");
            }
        }

        private string PathFor(string matchId)
        {
            var safe = string.IsNullOrEmpty(matchId) ? "duel" : matchId;
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                safe = safe.Replace(invalid, '_');
            }

            return Path.Combine(_folder, safe + Extension);
        }
    }
}
