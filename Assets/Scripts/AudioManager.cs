using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

[System.Serializable]
public class Sound
{
    public string name;
    public AudioClip clip;

    [Range(0f, 1f)]
    public float volume = 1f;

    [Range(0.1f, 3f)]
    public float pitch = 1f;

    public bool loop = false;

    [HideInInspector]
    public AudioSource source;
}

[System.Serializable]
public class SceneMusic
{
    public string sceneName;
    public AudioClip musicClip;
    [Range(0f, 1f)]
    public float volume = 0.5f;
    [Range(0.1f, 3f)]
    public float pitch = 1f;

    public bool loop = false;
}

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance;

    [Header("Audio Mixer")]
    public AudioMixerGroup musicMixerGroup;
    public AudioMixerGroup sfxMixerGroup;

    [Header("Sound Effects")]
    public Sound[] soundEffects;

    [Header("Background Music")]
    public SceneMusic[] sceneMusics;

    [Header("Default Settings (Inspector)")]
    [Range(0f, 1f)]
    public float defaultMasterVolume = 1f;

    [Range(0f, 1f)]
    public float defaultMusicVolume = 0.7f;

    [Range(0f, 1f)]
    public float defaultSfxVolume = 1f;

    [Header("Fade Settings")]
    public float fadeTime = 1f;

    [System.NonSerialized]
    public float masterVolume = 1f;
    [System.NonSerialized]
    public float musicVolume = 0.7f;
    [System.NonSerialized]
    public float sfxVolume = 1f;

    private const string MASTER_VOLUME_KEY = "MasterVolume";
    private const string MUSIC_VOLUME_KEY = "MusicVolume";
    private const string SFX_VOLUME_KEY = "SFXVolume";

    // Audio Sources per la musica
    private AudioSource musicSource;
    private AudioSource musicSourceSecondary; // Per il crossfade

    // Dizionari per accesso rapido
    private Dictionary<string, Sound> soundDictionary;
    private Dictionary<string, SceneMusic> musicDictionary;

    // Stato corrente
    private string currentMusicName;
    private bool isFading;

    void Awake()
    {
        // Singleton pattern
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAudioManager();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void InitializeAudioManager()
    {
        LoadSavedVolumes();

        // Crea dizionari per accesso rapido
        soundDictionary = new Dictionary<string, Sound>();
        musicDictionary = new Dictionary<string, SceneMusic>();

        // Inizializza sound effects
        foreach (Sound sound in soundEffects)
        {
            GameObject soundObject = new GameObject($"SFX_{sound.name}");
            soundObject.transform.SetParent(transform);

            sound.source = soundObject.AddComponent<AudioSource>();
            sound.source.clip = sound.clip;
            sound.source.volume = sound.volume;
            sound.source.pitch = sound.pitch;
            sound.source.loop = sound.loop;
            sound.source.outputAudioMixerGroup = sfxMixerGroup;

            soundDictionary[sound.name] = sound;
        }

        // Inizializza musica di background
        foreach (SceneMusic music in sceneMusics)
        {
            musicDictionary[music.sceneName] = music;
        }

        // Crea AudioSources per la musica
        CreateMusicSources();

        // Applica volumi iniziali
        UpdateVolumes();

        Debug.Log($"AudioManager inizializzato con volumi: Master={masterVolume:F2}, Music={musicVolume:F2}, SFX={sfxVolume:F2}");
    }

    private void LoadSavedVolumes()
    {

        float defaultMasterPercent = defaultMasterVolume * 100f;
        float defaultMusicPercent = defaultMusicVolume * 100f;
        float defaultSfxPercent = defaultSfxVolume * 100f;

        masterVolume = PlayerPrefs.GetFloat(MASTER_VOLUME_KEY, defaultMasterPercent) / 100f;
        musicVolume = PlayerPrefs.GetFloat(MUSIC_VOLUME_KEY, defaultMusicPercent) / 100f;
        sfxVolume = PlayerPrefs.GetFloat(SFX_VOLUME_KEY, defaultSfxPercent) / 100f;

        masterVolume = Mathf.Clamp01(masterVolume);
        musicVolume = Mathf.Clamp01(musicVolume);
        sfxVolume = Mathf.Clamp01(sfxVolume);

        Debug.Log($"Volumi caricati da PlayerPrefs: Master={masterVolume:F2} ({masterVolume * 100:F0}%), Music={musicVolume:F2} ({musicVolume * 100:F0}%), SFX={sfxVolume:F2} ({sfxVolume * 100:F0}%)");
    }

    void CreateMusicSources()
    {
        // Primary music source
        GameObject musicObject = new GameObject("Music_Primary");
        musicObject.transform.SetParent(transform);
        musicSource = musicObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.outputAudioMixerGroup = musicMixerGroup;

        // Secondary music source per crossfade
        GameObject musicObjectSecondary = new GameObject("Music_Secondary");
        musicObjectSecondary.transform.SetParent(transform);
        musicSourceSecondary = musicObjectSecondary.AddComponent<AudioSource>();
        musicSourceSecondary.loop = true;
        musicSourceSecondary.outputAudioMixerGroup = musicMixerGroup;
    }

    #region Sound Effects Methods

    public void PlaySFX(string soundName)
    {
        if (soundDictionary.ContainsKey(soundName))
        {
            Sound sound = soundDictionary[soundName];
            sound.source.Play();
        }
        else
        {
            Debug.LogWarning($"Sound effect '{soundName}' non trovato!");
        }
    }

    public void PlaySFXOneShot(string soundName)
    {
        if (soundDictionary.ContainsKey(soundName))
        {
            Sound sound = soundDictionary[soundName];
            sound.source.PlayOneShot(sound.clip);
        }
        else
        {
            Debug.LogWarning($"Sound effect '{soundName}' non trovato!");
        }
    }

    public void StopSFX(string soundName)
    {
        if (soundDictionary.ContainsKey(soundName))
        {
            soundDictionary[soundName].source.Stop();
        }
    }

    public void StopAllSFX()
    {
        foreach (Sound sound in soundEffects)
        {
            sound.source.Stop();
        }
    }

    public void SetSFXVolume(string soundName, float volume)
    {
        if (soundDictionary.ContainsKey(soundName))
        {
            soundDictionary[soundName].source.volume = volume;
        }
    }

    #endregion

    #region Background Music Methods

    public void PlayMusicForScene(string sceneName)
    {
        if (musicDictionary.ContainsKey(sceneName))
        {
            SceneMusic sceneMusic = musicDictionary[sceneName];
            PlayMusic(sceneMusic.musicClip, sceneMusic.volume, sceneName);
        }
        else
        {
            Debug.LogWarning($"Musica per la scena '{sceneName}' non trovata!");
        }
    }

    public void PlayMusic(AudioClip musicClip, float volume = 0.7f, string musicName = "")
    {
        if (musicClip == null) return;

        // Se è già in riproduzione la stessa musica, non fare nulla
        if (currentMusicName == musicName && musicSource.isPlaying) return;

        if (musicSource.isPlaying)
        {
            StartCoroutine(CrossFadeMusic(musicClip, volume, musicName));
        }
        else
        {
            musicSource.clip = musicClip;
            musicSource.volume = volume * musicVolume * masterVolume;
            musicSource.Play();
            currentMusicName = musicName;
        }
    }

    public void StopMusic(bool fadeOut = true)
    {
        if (fadeOut)
        {
            StartCoroutine(FadeOutMusic());
        }
        else
        {
            musicSource.Stop();
            musicSourceSecondary.Stop();
            currentMusicName = "";
        }
    }

    public void PauseMusic()
    {
        musicSource.Pause();
        musicSourceSecondary.Pause();
    }

    public void UnpauseMusic()
    {
        musicSource.UnPause();
        musicSourceSecondary.UnPause();
    }

    #endregion

    #region Volume Control

    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        UpdateVolumes();
        Debug.Log($"Master Volume impostato a: {masterVolume:F2} ({masterVolume * 100:F0}%)");
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        UpdateVolumes();
        Debug.Log($"Music Volume impostato a: {musicVolume:F2} ({musicVolume * 100:F0}%)");
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        UpdateVolumes();
        Debug.Log($"SFX Volume impostato a: {sfxVolume:F2} ({sfxVolume * 100:F0}%)");
    }

    void UpdateVolumes()
    {
        // Aggiorna volume musica
        if (musicSource != null)
        {
            float currentMusicVolume = 0.7f;
            if (!string.IsNullOrEmpty(currentMusicName) && musicDictionary.ContainsKey(currentMusicName))
            {
                currentMusicVolume = musicDictionary[currentMusicName].volume;
            }
            musicSource.volume = currentMusicVolume * musicVolume * masterVolume;
        }

        // Aggiorna volume secondary music source
        if (musicSourceSecondary != null && musicSourceSecondary.isPlaying)
        {
            float currentMusicVolume = 0.7f;
            if (!string.IsNullOrEmpty(currentMusicName) && musicDictionary.ContainsKey(currentMusicName))
            {
                currentMusicVolume = musicDictionary[currentMusicName].volume;
            }
            musicSourceSecondary.volume = currentMusicVolume * musicVolume * masterVolume;
        }

        // Aggiorna volume SFX
        foreach (Sound sound in soundEffects)
        {
            if (sound.source != null)
            {
                sound.source.volume = sound.volume * sfxVolume * masterVolume;
            }
        }
    }

    #endregion

    #region Fade Effects

    IEnumerator CrossFadeMusic(AudioClip newClip, float targetVolume, string musicName)
    {
        if (isFading) yield break;
        isFading = true;

        // Setup secondary source
        musicSourceSecondary.clip = newClip;
        musicSourceSecondary.volume = 0f;
        musicSourceSecondary.Play();

        float timer = 0f;
        float startVolumeMain = musicSource.volume;
        float startVolumeSecondary = 0f;
        float endVolumeMain = 0f;
        float endVolumeSecondary = targetVolume * musicVolume * masterVolume;

        while (timer < fadeTime)
        {
            timer += Time.deltaTime;
            float progress = timer / fadeTime;

            musicSource.volume = Mathf.Lerp(startVolumeMain, endVolumeMain, progress);
            musicSourceSecondary.volume = Mathf.Lerp(startVolumeSecondary, endVolumeSecondary, progress);

            yield return null;
        }

        // Swap sources
        musicSource.Stop();
        AudioSource temp = musicSource;
        musicSource = musicSourceSecondary;
        musicSourceSecondary = temp;

        currentMusicName = musicName;
        isFading = false;
    }

    IEnumerator FadeOutMusic()
    {
        if (isFading) yield break;
        isFading = true;

        float startVolume = musicSource.volume;
        float timer = 0f;

        while (timer < fadeTime)
        {
            timer += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(startVolume, 0f, timer / fadeTime);
            yield return null;
        }

        musicSource.Stop();
        musicSource.volume = startVolume;
        currentMusicName = "";
        isFading = false;
    }

    IEnumerator FadeInMusic(float targetVolume)
    {
        float timer = 0f;

        while (timer < fadeTime)
        {
            timer += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(0f, targetVolume, timer / fadeTime);
            yield return null;
        }
    }

    #endregion

    #region Utility Methods

    public bool IsSFXPlaying(string soundName)
    {
        if (soundDictionary.ContainsKey(soundName))
        {
            return soundDictionary[soundName].source.isPlaying;
        }
        return false;
    }

    public bool IsMusicPlaying()
    {
        return musicSource != null && musicSource.isPlaying;
    }

    public string GetCurrentMusicName()
    {
        return currentMusicName;
    }

    public float GetMasterVolume()
    {
        return masterVolume;
    }

    public float GetMusicVolume()
    {
        return musicVolume;
    }

    public float GetSFXVolume()
    {
        return sfxVolume;
    }

    public void AddSoundEffect(string name, AudioClip clip, float volume = 1f, float pitch = 1f, bool loop = false)
    {
        if (soundDictionary.ContainsKey(name))
        {
            Debug.LogWarning($"Sound effect '{name}' già esistente!");
            return;
        }

        Sound newSound = new Sound
        {
            name = name,
            clip = clip,
            volume = volume,
            pitch = pitch,
            loop = loop
        };

        GameObject soundObject = new GameObject($"SFX_{name}");
        soundObject.transform.SetParent(transform);

        newSound.source = soundObject.AddComponent<AudioSource>();
        newSound.source.clip = clip;
        newSound.source.volume = volume * sfxVolume * masterVolume;
        newSound.source.pitch = pitch;
        newSound.source.loop = loop;
        newSound.source.outputAudioMixerGroup = sfxMixerGroup;

        soundDictionary[name] = newSound;
    }

    public void RemoveSoundEffect(string name)
    {
        if (soundDictionary.ContainsKey(name))
        {
            Sound sound = soundDictionary[name];
            if (sound.source != null)
            {
                if (sound.source.gameObject != null)
                    Destroy(sound.source.gameObject);
            }
            soundDictionary.Remove(name);
        }
    }

    public void AddSceneMusic(string sceneName, AudioClip musicClip, float volume = 0.7f)
    {
        SceneMusic newMusic = new SceneMusic
        {
            sceneName = sceneName,
            musicClip = musicClip,
            volume = volume
        };

        musicDictionary[sceneName] = newMusic;
    }

    public void RemoveSceneMusic(string sceneName)
    {
        if (musicDictionary.ContainsKey(sceneName))
        {
            musicDictionary.Remove(sceneName);
        }
    }

    #endregion
}