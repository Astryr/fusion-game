using UnityEngine;

public class GameAudio : MonoBehaviour
{
    public static GameAudio Instance { get; private set; }

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
        _source.volume = 0.45f;
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
