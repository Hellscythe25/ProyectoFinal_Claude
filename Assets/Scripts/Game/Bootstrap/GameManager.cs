using System;
using TTTXO.Core.Duel;
using UnityEngine;

namespace TTTXO.Game.Bootstrap
{
    /// <summary>
    /// Local session state: the anonymous player identity, a small coin wallet and the audio
    /// volumes, all persisted in <see cref="PlayerPrefs"/>.
    ///
    /// <see cref="LocalPlayerId"/> is the id every duel document stores today. It is a locally
    /// generated GUID until Phase 4 replaces it with the Unity Authentication player id - the Hub
    /// only reads the value, so that swap does not touch the rules or the screens.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        private const float DefaultVolume = 0.8f;
        private const int DefaultCurrencyBalance = 100;
        private const string DefaultPlayerName = "Invitado";

        private const string PrefCurrencyKey = "RPS.CurrencyBalance";
        private const string PrefSoundVolumeKey = "RPS.SoundVolume";
        private const string PrefMusicVolumeKey = "RPS.MusicVolume";
        private const string PrefPlayerIdKey = "RPS.PlayerId";
        private const string PrefPlayerNameKey = "RPS.PlayerName";
        private const string PrefDuelDemoTimeoutKey = "RPS.DuelDemoTimeout";

        public static GameManager Instance { get; private set; }

        public int HardCurrencyBalance { get; private set; }
        public float SoundVolume { get; private set; }
        public float MusicVolume { get; private set; }

        /// <summary>Stable anonymous id for this device/profile.</summary>
        public string LocalPlayerId { get; private set; }

        /// <summary>Name shown to the rival in the duel document.</summary>
        public string PlayerName { get; private set; }

        /// <summary>When true the abandonment window is 2 minutes instead of 10 (demo mode).</summary>
        public bool DuelDemoTimeout { get; private set; }

        /// <summary>Inactivity window currently in force for duels.</summary>
        public TimeSpan DuelTimeout => TimeSpan.FromMinutes(
            DuelDemoTimeout ? DuelRules.DemoTimeoutMinutes : DuelRules.DefaultTimeoutMinutes);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Load();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void SetSoundVolume(float value)
        {
            SoundVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(PrefSoundVolumeKey, SoundVolume);
            PlayerPrefs.Save();
        }

        public void SetMusicVolume(float value)
        {
            MusicVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(PrefMusicVolumeKey, MusicVolume);
            PlayerPrefs.Save();
        }

        public void SetDuelDemoTimeout(bool enabled)
        {
            DuelDemoTimeout = enabled;
            PlayerPrefs.SetInt(PrefDuelDemoTimeoutKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void SetPlayerName(string playerName)
        {
            PlayerName = string.IsNullOrWhiteSpace(playerName) ? DefaultPlayerName : playerName.Trim();
            PlayerPrefs.SetString(PrefPlayerNameKey, PlayerName);
            PlayerPrefs.Save();
        }

        public void AddCurrency(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            HardCurrencyBalance += amount;
            PlayerPrefs.SetInt(PrefCurrencyKey, HardCurrencyBalance);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Resets the local profile: coins go back to the starting balance, both volumes return to
        /// their defaults and a new anonymous id is generated, so the duels saved under the old id
        /// are no longer attached to this profile.
        /// </summary>
        public void DeleteAllPlayerData()
        {
            HardCurrencyBalance = DefaultCurrencyBalance;
            SoundVolume = DefaultVolume;
            MusicVolume = DefaultVolume;
            DuelDemoTimeout = false;
            PlayerName = DefaultPlayerName;
            LocalPlayerId = Guid.NewGuid().ToString("N");

            PlayerPrefs.SetInt(PrefCurrencyKey, HardCurrencyBalance);
            PlayerPrefs.SetFloat(PrefSoundVolumeKey, SoundVolume);
            PlayerPrefs.SetFloat(PrefMusicVolumeKey, MusicVolume);
            PlayerPrefs.SetInt(PrefDuelDemoTimeoutKey, 0);
            PlayerPrefs.SetString(PrefPlayerNameKey, PlayerName);
            PlayerPrefs.SetString(PrefPlayerIdKey, LocalPlayerId);
            PlayerPrefs.Save();
        }

        private void Load()
        {
            HardCurrencyBalance = PlayerPrefs.GetInt(PrefCurrencyKey, DefaultCurrencyBalance);
            SoundVolume = PlayerPrefs.GetFloat(PrefSoundVolumeKey, DefaultVolume);
            MusicVolume = PlayerPrefs.GetFloat(PrefMusicVolumeKey, DefaultVolume);
            DuelDemoTimeout = PlayerPrefs.GetInt(PrefDuelDemoTimeoutKey, 0) == 1;

            PlayerName = PlayerPrefs.GetString(PrefPlayerNameKey, DefaultPlayerName);
            if (string.IsNullOrWhiteSpace(PlayerName))
            {
                PlayerName = DefaultPlayerName;
            }

            LocalPlayerId = PlayerPrefs.GetString(PrefPlayerIdKey, string.Empty);
            if (string.IsNullOrWhiteSpace(LocalPlayerId))
            {
                LocalPlayerId = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(PrefPlayerIdKey, LocalPlayerId);
                PlayerPrefs.Save();
            }
        }
    }
}
