using System.Collections.Generic;
using UnityEngine;

public class MenuAmbience : MonoBehaviour
{
    private sealed class Faller
    {
        public Transform Body;
        public float X;
        public float Y;
        public float Speed;
        public float SwayAmplitude;
        public float SwaySpeed;
        public float Phase;
        public float Spin;
    }

    private sealed class Walker
    {
        public Transform Body;
        public SpriteRenderer Renderer;
        public Animator Animator;
        public float X;
        public float Direction;
        public float Speed;
        public float PauseLeft;
        public float JumpHeight;
        public float JumpVelocity;
        public bool Airborne;
    }

    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly int GroundedParam = Animator.StringToHash("Grounded");

    private static readonly Color[] MonkeyTints =
    {
        new Color(1.00f, 0.55f, 0.55f),
        new Color(0.55f, 0.75f, 1.00f),
        new Color(0.60f, 1.00f, 0.60f),
        new Color(1.00f, 0.95f, 0.50f),
    };

    private readonly List<Faller> _fallers = new List<Faller>();
    private readonly List<Walker> _walkers = new List<Walker>();
    private Camera _camera;
    private SpriteRenderer _background;
    private Sprite _backgroundSprite;

    public void Build(Camera camera)
    {
        _camera = camera;
        BuildBackground();
        BuildFallers();
        BuildWalkers();
        FitBackground();
    }

    private void Update()
    {
        if (_camera == null)
        {
            return;
        }

        float dt = Time.unscaledDeltaTime;
        FitBackground();
        TickFallers(dt);
        TickWalkers(dt);
    }

    private float ViewHeight => _camera.orthographicSize * 2f;
    private float ViewWidth => ViewHeight * _camera.aspect;

    private void BuildBackground()
    {
        _backgroundSprite = Resources.Load<Sprite>("Menu/MenuBackground");
        if (_backgroundSprite == null)
        {
            return;
        }

        var go = new GameObject("MenuBackground");
        go.transform.SetParent(transform, false);
        _background = go.AddComponent<SpriteRenderer>();
        _background.sprite = _backgroundSprite;
        _background.sortingOrder = -100;
    }

    private void FitBackground()
    {
        if (_background == null)
        {
            return;
        }

        Vector2 size = _backgroundSprite.bounds.size;
        float scale = Mathf.Max(ViewWidth / size.x, ViewHeight / size.y);
        _background.transform.localScale = new Vector3(scale, scale, 1f);
        Vector3 center = _camera.transform.position;
        _background.transform.position = new Vector3(center.x, center.y, 0f);
    }

    private void BuildFallers()
    {
        Sprite banana = SpriteFromPrefab("Banana");
        Sprite bomb = SpriteFromPrefab("BananaExplosiva");
        if (banana == null)
        {
            return;
        }

        const int count = 9;
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("FallingBanana" + i);
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = (i % 5 == 4 && bomb != null) ? bomb : banana;
            renderer.sortingOrder = -50;
            renderer.color = new Color(1f, 1f, 1f, 0.92f);
            float scale = Random.Range(0.9f, 1.5f);
            go.transform.localScale = new Vector3(scale, scale, 1f);

            _fallers.Add(new Faller
            {
                Body = go.transform,
                X = Mathf.Lerp(-0.5f, 0.5f, (i + Random.value * 0.6f) / count),
                Y = Random.value,
                Speed = Random.Range(0.55f, 1.15f),
                SwayAmplitude = Random.Range(0.25f, 0.6f),
                SwaySpeed = Random.Range(0.6f, 1.4f),
                Phase = Random.value * 6.28f,
                Spin = Random.Range(-40f, 40f),
            });
        }
    }

    private void TickFallers(float dt)
    {
        Vector3 center = _camera.transform.position;
        float width = ViewWidth;
        float height = ViewHeight;
        foreach (Faller faller in _fallers)
        {
            faller.Y -= faller.Speed * dt / height;
            if (faller.Y < -0.12f)
            {
                faller.Y = 1.1f;
                faller.X = Random.Range(-0.5f, 0.5f);
            }

            float x = center.x + faller.X * width + Mathf.Sin(Time.unscaledTime * faller.SwaySpeed + faller.Phase) * faller.SwayAmplitude;
            float y = center.y + (faller.Y - 0.5f) * height;
            faller.Body.position = new Vector3(x, y, 0f);
            faller.Body.Rotate(0f, 0f, faller.Spin * dt);
        }
    }

    private void BuildWalkers()
    {
        var prefab = Resources.Load<GameObject>("Player");
        if (prefab == null)
        {
            return;
        }

        Sprite idle = prefab.GetComponent<SpriteRenderer>() != null ? prefab.GetComponent<SpriteRenderer>().sprite : null;
        RuntimeAnimatorController controller = prefab.GetComponent<Animator>() != null
            ? prefab.GetComponent<Animator>().runtimeAnimatorController
            : null;
        if (idle == null)
        {
            return;
        }

        for (int i = 0; i < MonkeyTints.Length; i++)
        {
            var go = new GameObject("MenuMonkey" + i);
            go.transform.SetParent(transform, false);
            float scale = 1.75f;
            go.transform.localScale = new Vector3(scale, scale, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = idle;
            renderer.color = MonkeyTints[i];
            renderer.sortingOrder = i;

            Animator animator = null;
            if (controller != null)
            {
                animator = go.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            _walkers.Add(new Walker
            {
                Body = go.transform,
                Renderer = renderer,
                Animator = animator,
                X = Mathf.Lerp(-0.38f, 0.38f, i / (MonkeyTints.Length - 1f)),
                Direction = i % 2 == 0 ? 1f : -1f,
                Speed = Random.Range(1.5f, 2.5f),
                PauseLeft = Random.Range(0f, 1.5f),
            });
        }
    }

    private void TickWalkers(float dt)
    {
        Vector3 center = _camera.transform.position;
        float width = ViewWidth;
        float baseY = center.y - ViewHeight * 0.5f + ViewHeight * 0.055f;
        float limit = width * 0.5f - 1.2f;

        foreach (Walker walker in _walkers)
        {
            bool paused = walker.PauseLeft > 0f;
            if (paused)
            {
                walker.PauseLeft -= dt;
            }
            else
            {
                walker.X += walker.Direction * walker.Speed * dt / width;
                float worldX = walker.X * width;
                if (worldX > limit)
                {
                    walker.Direction = -1f;
                }
                else if (worldX < -limit)
                {
                    walker.Direction = 1f;
                }

                if (!walker.Airborne && Random.value < dt * 0.22f)
                {
                    walker.Airborne = true;
                    walker.JumpVelocity = Random.Range(7.5f, 9.5f);
                }
                else if (!walker.Airborne && Random.value < dt * 0.1f)
                {
                    walker.PauseLeft = Random.Range(1.2f, 2.6f);
                }
            }

            if (walker.Airborne)
            {
                walker.JumpHeight += walker.JumpVelocity * dt;
                walker.JumpVelocity -= 22f * dt;
                if (walker.JumpHeight <= 0f)
                {
                    walker.JumpHeight = 0f;
                    walker.Airborne = false;
                }
            }

            walker.Body.position = new Vector3(center.x + walker.X * width, baseY + walker.JumpHeight, 0f);
            walker.Renderer.flipX = walker.Direction < 0f;

            if (walker.Animator != null && walker.Animator.isActiveAndEnabled && walker.Animator.runtimeAnimatorController != null)
            {
                walker.Animator.SetFloat(SpeedParam, paused ? 0f : 1f);
                walker.Animator.SetBool(GroundedParam, !walker.Airborne);
            }
        }
    }

    private static Sprite SpriteFromPrefab(string resourceName)
    {
        var prefab = Resources.Load<GameObject>(resourceName);
        return prefab != null && prefab.GetComponent<SpriteRenderer>() != null ? prefab.GetComponent<SpriteRenderer>().sprite : null;
    }
}
