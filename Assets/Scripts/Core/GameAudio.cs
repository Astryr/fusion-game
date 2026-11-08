using UnityEngine;

public class GameAudio : MonoBehaviour
{
    private const string VolumePref = "br.music";
    private const float DefaultVolume = 0.5f;
    private const float MaxGain = 0.9f;

    private static float _musicVolume = -1f;

    public static GameAudio Instance { get; private set; }

    public static float MusicVolume
    {
        get
        {
            if (_musicVolume < 0f)
            {
                _musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumePref, DefaultVolume));
            }

            return _musicVolume;
        }
        set
        {
            _musicVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolumePref, _musicVolume);
            PlayerPrefs.Save();
            if (Instance != null && Instance._source != null)
            {
                Instance._source.volume = _musicVolume * MaxGain;
            }
        }
    }

    private AudioSource _source;
    private AudioClip _menuClip;
    private AudioClip _minigameClip;
    private string _current;

    public static void PlayMenu()
    {
        Ensure().Play("menu");
    }

    public static void PlayMinigames()
    {
        Ensure().Play("minigames");
    }

    private static GameAudio Ensure()
    {
        if (Instance != null)
        {
            return Instance;
        }

        var go = new GameObject("GameAudio");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<GameAudio>();
        Instance.Build();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Build();
    }

    private void Build()
    {
        if (_source != null)
        {
            return;
        }

        _source = gameObject.AddComponent<AudioSource>();
        _source.loop = true;
        _source.playOnAwake = false;
        _source.spatialBlend = 0f;
        _source.volume = MusicVolume * MaxGain;
        _menuClip = Resources.Load<AudioClip>("Audio/MainMenuSong");
        _minigameClip = Resources.Load<AudioClip>("Audio/MinigamesSong");
    }

    private void Play(string id)
    {
        AudioClip clip = id == "minigames" ? _minigameClip : _menuClip;
        if (clip == null || (_current == id && _source.isPlaying))
        {
            return;
        }

        _current = id;
        _source.clip = clip;
        _source.Play();
    }
}
