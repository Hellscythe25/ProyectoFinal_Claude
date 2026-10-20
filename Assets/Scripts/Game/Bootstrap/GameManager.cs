using UnityEngine;

namespace TTTXO.Game.Bootstrap
{
    /// <summary>
    /// Local session state for the Rock-Paper-Scissors game: a small coin wallet and the audio
    /// volumes, both persisted in <see cref="PlayerPrefs"/>. There are no online services in this
    /// build, so this is the only source of truth for the player's local profile.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        private const float DefaultVolume = 0.8f;
        private const int DefaultCurrencyBalance = 100;

        private const string PrefCurrencyKey = "RPS.CurrencyBalance";
        private const string PrefSoundVolumeKey = "RPS.SoundVolume";
        private const string PrefMusicVolumeKey = "RPS.MusicVolume";

        public static GameManager Instance { get; private set; }

        public int HardCurrencyBalance { get; private set; }
        public float SoundVolume { get; private set; }
        public float MusicVolume { get; private set; }

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
        /// Resets the local profile: coins go back to the starting balance and both volumes return
        /// to their defaults. The settings screen offers this as the "delete account and data"
        /// action because this build has no server-side account.
        /// </summary>
        public void DeleteAllPlayerData()
        {
            HardCurrencyBalance = DefaultCurrencyBalance;
            SoundVolume = DefaultVolume;
            MusicVolume = DefaultVolume;

            PlayerPrefs.SetInt(PrefCurrencyKey, HardCurrencyBalance);
            PlayerPrefs.SetFloat(PrefSoundVolumeKey, SoundVolume);
            PlayerPrefs.SetFloat(PrefMusicVolumeKey, MusicVolume);
            PlayerPrefs.Save();
        }

        private void Load()
        {
            HardCurrencyBalance = PlayerPrefs.GetInt(PrefCurrencyKey, DefaultCurrencyBalance);
            SoundVolume = PlayerPrefs.GetFloat(PrefSoundVolumeKey, DefaultVolume);
            MusicVolume = PlayerPrefs.GetFloat(PrefMusicVolumeKey, DefaultVolume);
        }
    }
}
