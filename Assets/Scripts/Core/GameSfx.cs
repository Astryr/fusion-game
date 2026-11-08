using System;
using UnityEngine;

public enum Sfx
{
    Click,
    Pickup,
    PickupGreen,
    Explosion,
    Jump,
    Out,
    Tick,
    Go,
    Win,
    Lose,
    Chat,
    Ready,
    Unready,
    Chop,
    Thump,
    Flip,
    Match,
    Mismatch,
    Whoosh,
    Crash,
    Join,
    Leave,
}

public static class GameSfx
{
    private const string VolumePref = "br.sfx";
    private const float DefaultVolume = 0.8f;
    private const int SampleRate = 44100;

    private static AudioSource _source;
    private static AudioClip[] _clips;
    private static float _volume = -1f;

    public static float Volume
    {
        get
        {
            if (_volume < 0f)
            {
                _volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumePref, DefaultVolume));
            }

            return _volume;
        }
        set
        {
            _volume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolumePref, _volume);
            PlayerPrefs.Save();
        }
    }

    public static void Warmup()
    {
        Ensure();
    }

    public static void Play(Sfx sfx, float gain = 1f)
    {
        float level = Volume * gain;
        if (level <= 0.001f || !Ensure())
        {
            return;
        }

        AudioClip clip = _clips[(int)sfx];
        if (clip != null)
        {
            _source.PlayOneShot(clip, level);
        }
    }

    private static bool Ensure()
    {
        if (_source != null)
        {
            return true;
        }

        var go = new GameObject("GameSfx");
        UnityEngine.Object.DontDestroyOnLoad(go);
        _source = go.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.spatialBlend = 0f;

        if (_clips == null)
        {
            _clips = BuildClips();
        }

        return true;
    }

    private static AudioClip[] BuildClips()
    {
        var clips = new AudioClip[Enum.GetValues(typeof(Sfx)).Length];

        clips[(int)Sfx.Click] = Make("Click", 0.06f,
            t => 0.55f * Note(t, 0f, 0.06f, 1100f, 700f, 55f));

        clips[(int)Sfx.Pickup] = Make("Pickup", 0.26f,
            t => 0.42f * (Note(t, 0f, 0.09f, 880f, 880f, 22f) + Note(t, 0.07f, 0.19f, 1320f, 1320f, 16f)));

        clips[(int)Sfx.PickupGreen] = Make("PickupGreen", 0.5f,
            t => 0.3f * (Note(t, 0f, 0.2f, 1046f, 1046f, 10f) + Note(t, 0.06f, 0.2f, 1318f, 1318f, 10f)
                         + Note(t, 0.12f, 0.2f, 1568f, 1568f, 10f) + Note(t, 0.18f, 0.3f, 2093f, 2093f, 8f)));

        Func<float, float> rumble = NoiseBurst(7, 7f, 0.12f, 1.1f);
        clips[(int)Sfx.Explosion] = Make("Explosion", 0.6f,
            t => 0.7f * rumble(t) + 0.55f * Note(t, 0f, 0.45f, 140f, 45f, 6f));

        clips[(int)Sfx.Jump] = Make("Jump", 0.16f,
            t => 0.38f * Note(t, 0f, 0.15f, 330f, 820f, 12f, 0.5f));

        clips[(int)Sfx.Out] = Make("Out", 0.7f,
            t => 0.36f * (Note(t, 0f, 0.3f, 392f, 392f, 7f, 0.25f) + Note(t, 0.15f, 0.3f, 330f, 330f, 7f, 0.25f)
                          + Note(t, 0.3f, 0.4f, 262f, 196f, 6f, 0.25f)));

        clips[(int)Sfx.Tick] = Make("Tick", 0.08f,
            t => 0.45f * Note(t, 0f, 0.08f, 1250f, 1250f, 50f));

        clips[(int)Sfx.Go] = Make("Go", 0.45f,
            t => 0.3f * (Note(t, 0f, 0.45f, 880f, 880f, 6f) + Note(t, 0f, 0.45f, 1320f, 1320f, 6f)
                         + 0.6f * Note(t, 0f, 0.45f, 1760f, 1760f, 8f)));

        clips[(int)Sfx.Win] = Make("Win", 1.1f,
            t => 0.22f * (Note(t, 0f, 0.3f, 523f, 523f, 6f) + Note(t, 0.11f, 0.3f, 659f, 659f, 6f)
                          + Note(t, 0.22f, 0.3f, 784f, 784f, 6f) + Note(t, 0.33f, 0.3f, 1046f, 1046f, 6f)
                          + Note(t, 0.55f, 0.55f, 523f, 523f, 4f) + Note(t, 0.55f, 0.55f, 659f, 659f, 4f)
                          + Note(t, 0.55f, 0.55f, 784f, 784f, 4f) + Note(t, 0.55f, 0.55f, 1046f, 1046f, 4f)));

        clips[(int)Sfx.Lose] = Make("Lose", 1.1f,
            t => 0.32f * (Note(t, 0f, 0.4f, 392f, 392f, 5f, 0.3f) + Note(t, 0.2f, 0.4f, 370f, 370f, 5f, 0.3f)
                          + Note(t, 0.4f, 0.4f, 330f, 330f, 5f, 0.3f) + Note(t, 0.6f, 0.5f, 262f, 196f, 4f, 0.3f)));

        clips[(int)Sfx.Chat] = Make("Chat", 0.12f,
            t => 0.3f * Note(t, 0f, 0.11f, 780f, 540f, 28f));

        clips[(int)Sfx.Ready] = Make("Ready", 0.24f,
            t => 0.36f * (Note(t, 0f, 0.1f, 660f, 660f, 18f) + Note(t, 0.08f, 0.16f, 990f, 990f, 16f)));

        clips[(int)Sfx.Unready] = Make("Unready", 0.24f,
            t => 0.36f * (Note(t, 0f, 0.1f, 600f, 600f, 18f) + Note(t, 0.08f, 0.16f, 400f, 400f, 16f)));

        Func<float, float> knock = NoiseBurst(11, 45f, 0.5f, 1.2f);
        clips[(int)Sfx.Chop] = Make("Chop", 0.13f,
            t => 0.55f * knock(t) + 0.5f * Note(t, 0f, 0.12f, 200f, 110f, 35f));

        Func<float, float> click = NoiseBurst(13, 90f, 0.6f, 1f);
        clips[(int)Sfx.Thump] = Make("Thump", 0.22f,
            t => 0.85f * Note(t, 0f, 0.2f, 150f, 55f, 16f) + 0.25f * click(t));

        clips[(int)Sfx.Flip] = Make("Flip", 0.07f,
            t => 0.35f * Note(t, 0f, 0.065f, 520f, 640f, 45f));

        clips[(int)Sfx.Match] = Make("Match", 0.32f,
            t => 0.36f * (Note(t, 0f, 0.12f, 700f, 700f, 18f) + Note(t, 0.09f, 0.22f, 1050f, 1050f, 12f)));

        clips[(int)Sfx.Mismatch] = Make("Mismatch", 0.27f,
            t => 0.34f * Note(t, 0f, 0.26f, 170f, 130f, 9f, 0.9f));

        const float whooshLength = 0.5f;
        var whooshRng = new System.Random(17);
        float whooshLow = 0f;
        clips[(int)Sfx.Whoosh] = Make("Whoosh", whooshLength, t =>
        {
            float white = (float)(whooshRng.NextDouble() * 2.0 - 1.0);
            whooshLow += (white - whooshLow) * Mathf.Lerp(0.04f, 0.35f, t / whooshLength);
            float envelope = Mathf.Sin(Mathf.PI * t / whooshLength);
            return 0.9f * whooshLow * envelope * envelope;
        });

        Func<float, float> crash = NoiseBurst(19, 5f, 0.3f, 1f);
        clips[(int)Sfx.Crash] = Make("Crash", 0.55f,
            t => 0.7f * crash(t) + 0.5f * Note(t, 0f, 0.35f, 90f, 40f, 8f));

        clips[(int)Sfx.Join] = Make("Join", 0.26f,
            t => 0.3f * (Note(t, 0f, 0.1f, 520f, 520f, 18f) + Note(t, 0.09f, 0.17f, 780f, 780f, 16f)));

        clips[(int)Sfx.Leave] = Make("Leave", 0.26f,
            t => 0.3f * (Note(t, 0f, 0.1f, 780f, 780f, 18f) + Note(t, 0.09f, 0.17f, 520f, 520f, 16f)));

        return clips;
    }

    private static AudioClip Make(string name, float duration, Func<float, float> sample)
    {
        int count = Mathf.CeilToInt(duration * SampleRate);
        var data = new float[count];
        for (int i = 0; i < count; i++)
        {
            data[i] = Mathf.Clamp(sample(i / (float)SampleRate), -1f, 1f);
        }

        int fade = Mathf.Min(count, SampleRate / 200);
        for (int i = 0; i < fade; i++)
        {
            data[count - 1 - i] *= i / (float)fade;
        }

        AudioClip clip = AudioClip.Create("Sfx_" + name, count, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static float Note(float t, float start, float length, float freqStart, float freqEnd, float decay, float squareness = 0f)
    {
        float local = t - start;
        if (local < 0f || local > length)
        {
            return 0f;
        }

        float phase = 2f * Mathf.PI * (freqStart * local + (freqEnd - freqStart) * local * local / (2f * length));
        float wave = Mathf.Sin(phase);
        if (squareness > 0f)
        {
            wave = Mathf.Lerp(wave, Mathf.Sign(wave), squareness);
        }

        float attack = Mathf.Clamp01(local / 0.004f);
        return wave * attack * Mathf.Exp(-local * decay);
    }

    private static Func<float, float> NoiseBurst(int seed, float decay, float smoothing, float gain)
    {
        var rng = new System.Random(seed);
        float low = 0f;
        return t =>
        {
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            low += (white - low) * smoothing;
            return low * gain * Mathf.Exp(-t * decay);
        };
    }
}
