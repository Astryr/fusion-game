using UnityEngine;
using UnityEngine.UI;

public class PrimateScreamsMinigame : MonoBehaviour, IMiniGame
{
    public MiniGameId Id => MiniGameId.PrimateScreams;

    private const int TargetScore = 5;
    private const float FirstSoundDelay = 2.2f;
    private const float SoundInterval = 3.2f;
    private const float ListenWindow = 1.45f;

    private static readonly string[] AnimalNames =
    {
        "VACA", "PERRO", "PALOMA", "OVEJA", "LOBO", "LEON", "GATO", "GALLO", "ELEFANTE", "CERDO"
    };

    private static readonly string[] ClipNames =
    {
        "VACA", "PERRO", "PALOMA", "OVEJA", "LOBO", "LEON", "GATO", "GALLO", "ELEFANTE", "CERDO"
    };

    private BananaGameManager _director;
    private Canvas _canvas;
    private Text _promptText;
    private AudioSource _audioSource;
    private AudioClip[] _animalClips;
    private bool _playing;
    private System.Random _rng;
    private float _soundTimer;
    private int _previousTarget = -1;
    private int _previousPlayed = -1;
    private int _renderedSoundId = -1;

    public void Setup(BananaGameManager director)
    {
        _director = director;
        _animalClips = new AudioClip[ClipNames.Length];
        for (int i = 0; i < ClipNames.Length; i++)
        {
            _animalClips[i] = Resources.Load<AudioClip>("Sounds/" + ClipNames[i]);
        }
    }

    public void OnCountdownStarted() { }

    public void OnMatchStarted()
    {
        _playing = true;
        GameAudio.SetVolumeScale(0.35f);
        _rng = new System.Random(_director.RoundSeed + 1907);
        _renderedSoundId = -1;
        BuildUI();

        foreach (var player in _director.GetPlayers())
        {
            player.SetControlMode(PlayerControlMode.PrimateScreams);
            player.PrimateRoundDisqualified = false;
            player.SetWorldBounds(-BananaRushConfig.PlayerClampX, BananaRushConfig.PlayerClampX,
                BananaRushConfig.GroundTopY, -8f);
            player.SetPushEnabled(false);
            if (_director.Object.HasStateAuthority)
            {
                float x = (player.Object.InputAuthority.PlayerId % 4 - 1.5f) * 1.35f;
                player.Teleport(new Vector3(x, BananaRushConfig.GroundTopY, 0f));
            }
        }

        if (_director.Object.HasStateAuthority)
        {
            foreach (var player in _director.GetPlayers())
            {
                player.RPC_SetScore(0);
            }

            _director.PrimateAnimalIndex = -1;
            _director.PrimateSoundId = 0;
            _director.PrimateRoundId = 0;
            _director.PrimatePointWinner = default;
            _director.PrimatePointUntil = 0f;
            _director.PrimateResultUntil = 0f;
            _director.PrimateCountdownUntil = _director.MatchTime + 3f;
            _director.PrimateCountdownActive = true;
            _director.PrimateRoundActive = false;
            _director.PrimateRoundDisqualifiedMask = 0;
        }
    }

    public void Tick(float deltaTime, bool hasAuthority)
    {
        if (!_playing)
        {
            return;
        }

        RefreshSound();
        RefreshUI();

        if (!hasAuthority)
        {
            return;
        }

        foreach (var player in _director.GetPlayers())
        {
            if (!player.IsEliminated && player.Score >= TargetScore)
            {
                _director.DeclareWinner(player, "grito mas fuerte");
                return;
            }
        }

        if (_director.PrimateCountdownActive)
        {
            if (_director.MatchTime >= _director.PrimateCountdownUntil)
            {
                _director.PrimateCountdownActive = false;
                StartRound();
            }
            return;
        }

        if (_director.PrimateResultUntil > 0f)
        {
            if (_director.MatchTime >= _director.PrimateResultUntil)
            {
                _director.PrimateResultUntil = 0f;
                _director.PrimatePointWinner = default;
                _director.PrimateCountdownActive = true;
                _director.PrimateCountdownUntil = _director.MatchTime + 3f;
                foreach (var player in _director.GetPlayers())
                {
                    player.PrimateRoundDisqualified = false;
                }
            }
            return;
        }

        if (_director.PrimateRoundActive)
        {
            _soundTimer -= deltaTime;
            if (_soundTimer <= 0f)
            {
                PlayNextAnimal();
                _soundTimer = SoundInterval;
            }
        }
        else if (_director.PrimatePointUntil > 0f && _director.MatchTime >= _director.PrimatePointUntil)
        {
            StartRound();
        }
    }

    public void OnMatchEnded()
    {
        Cleanup();
    }

    public void Cleanup()
    {
        _playing = false;
        GameAudio.SetVolumeScale(1f);
        if (_canvas != null)
        {
            Destroy(_canvas.gameObject);
            _canvas = null;
        }
        if (_audioSource != null)
        {
            Destroy(_audioSource);
            _audioSource = null;
        }
    }

    private void StartRound()
    {
        int target = _rng.Next(ClipNames.Length);
        if (target == _previousTarget && ClipNames.Length > 1)
        {
            target = (target + 1 + _rng.Next(ClipNames.Length - 1)) % ClipNames.Length;
        }

        _previousTarget = target;
        _previousPlayed = -1;
        _soundTimer = FirstSoundDelay;
        _director.PrimateTargetIndex = target;
        _director.PrimateAnimalIndex = -1;
        _director.PrimateRoundId++;
        _director.PrimateRoundActive = true;
        _director.PrimateListenUntil = 0f;
        _director.PrimatePointWinner = default;
        _director.PrimateCountdownActive = false;
        _director.PrimateRoundDisqualifiedMask = 0;
        foreach (var player in _director.GetPlayers())
        {
            player.PrimateRoundDisqualified = false;
        }
    }

    private void PlayNextAnimal()
    {
        int animal = _rng.Next(ClipNames.Length);

        if (animal == _previousPlayed)
        {
            animal = (animal + 1) % ClipNames.Length;
            if (animal == _previousPlayed)
            {
                animal = (animal + 1) % ClipNames.Length;
            }
        }

        _previousPlayed = animal;
        _director.PrimateAnimalIndex = animal;
        _director.PrimateSoundId++;
        _director.PrimateListenUntil = _director.MatchTime + ListenWindow;
    }

    private void RefreshSound()
    {
        int soundId = _director.PrimateSoundId;
        if (_renderedSoundId == soundId)
        {
            return;
        }

        _renderedSoundId = soundId;
        if (!_director.PrimateRoundActive || _director.PrimateAnimalIndex < 0)
        {
            if (_audioSource.isPlaying)
            {
                _audioSource.Stop();
            }
            return;
        }

        int index = Mathf.Clamp(_director.PrimateAnimalIndex, 0, _animalClips.Length - 1);
        if (_animalClips[index] != null)
        {
            _audioSource.PlayOneShot(_animalClips[index], 1f);
        }
    }

    private void RefreshUI()
    {
        if (_promptText == null)
        {
            return;
        }

        if (PlayerController.Local != null && PlayerController.Local.PrimateRoundDisqualified)
        {
            if (_director.PrimateCountdownActive)
            {
                int seconds = Mathf.Max(1, Mathf.CeilToInt(_director.PrimateCountdownUntil - _director.MatchTime));
                _promptText.supportRichText = false;
                _promptText.text = seconds.ToString();
                _promptText.color = new Color(1f, 0.86f, 0.30f);
            }
            else
            {
                _promptText.supportRichText = false;
                _promptText.text = "Has sido descalificado";
                _promptText.color = new Color(1f, 0.25f, 0.22f);
            }
            return;
        }

        string pointWinner = _director.PrimatePointWinner.ToString();
        if (!string.IsNullOrEmpty(pointWinner))
        {
            if (_director.PrimateCountdownActive)
            {
                int seconds = Mathf.Max(1, Mathf.CeilToInt(_director.PrimateCountdownUntil - _director.MatchTime));
                _promptText.supportRichText = false;
                _promptText.text = seconds.ToString();
                _promptText.color = new Color(1f, 0.86f, 0.30f);
            }
            else
            {
                _promptText.supportRichText = false;
                _promptText.text = $"¡{pointWinner} ha conseguido un punto!";
                _promptText.color = new Color(0.55f, 1f, 0.55f);
            }
        }
        else if (_director.PrimateCountdownActive)
        {
            int seconds = Mathf.Max(1, Mathf.CeilToInt(_director.PrimateCountdownUntil - _director.MatchTime));
            _promptText.supportRichText = false;
            _promptText.text = seconds.ToString();
            _promptText.color = new Color(1f, 0.86f, 0.30f);
        }
        else
        {
            int target = Mathf.Clamp(_director.PrimateTargetIndex, 0, AnimalNames.Length - 1);
            _promptText.supportRichText = true;
            _promptText.text = $"Grita con la tecla <color=#55FF55>ESPACIO</color> cuando escuches al siguiente animal: <color=#55FF55>{AnimalNames[target]}</color>";
            _promptText.color = new Color(1f, 0.86f, 0.30f);
        }

    }

    private void BuildUI()
    {
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;
        }

        if (_canvas != null)
        {
            Destroy(_canvas.gameObject);
        }

        _canvas = UIFactory.CreateCanvas("PrimateScreamsCanvas");
        var panel = UIFactory.CreatePanel(_canvas.transform, "PrimatePromptPanel",
            new Color(0.08f, 0.09f, 0.12f, 0.88f),
            new Vector2(0.5f, 0.48f), new Vector2(0.5f, 0.48f),
            new Vector2(-390f, -68f), new Vector2(390f, 68f));

        _promptText = UIStyle.CreateHeading(panel, "Escucha la selva...", 28,
            new Color(1f, 0.86f, 0.30f), new Color(0.12f, 0.05f, 0.02f));
        UIFactory.SetRect(_promptText.rectTransform,
            new Vector2(0f, 0f), new Vector2(1f, 1f),
            new Vector2(24f, 16f), new Vector2(-24f, -16f));

    }
}
