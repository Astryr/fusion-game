using Fusion;
using UnityEngine;

/// <summary>
/// Personaje jugable en red. El movimiento se calcula en
/// <see cref="FixedUpdateNetwork"/> (obligatorio para que Fusion pueda
/// re-simular) y se replica mediante el componente Network Transform que
/// se agrega al mismo prefab. El color es una propiedad <c>[Networked]</c>
/// simple para mostrar de forma visual que el estado se sincroniza entre
/// todos los clientes (JR6 - Sincronizacion e interaccion entre jugadores),
/// mientras no haya sprites definitivos del GDD.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerController : NetworkBehaviour
{
    [Header("Movimiento")]
    [SerializeField] private float _moveSpeed = 5f;

    [Networked] private int PlayerColorIndex { get; set; }

    private SpriteRenderer _spriteRenderer;

    private static readonly Color[] Palette =
    {
        new Color(0.94f, 0.32f, 0.32f),
        new Color(0.32f, 0.60f, 0.94f),
        new Color(0.36f, 0.82f, 0.45f),
        new Color(0.95f, 0.78f, 0.25f),
        new Color(0.72f, 0.40f, 0.90f),
        new Color(0.98f, 0.55f, 0.22f),
        new Color(0.30f, 0.85f, 0.85f),
        new Color(0.90f, 0.45f, 0.68f),
    };

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer.sprite == null)
        {
            _spriteRenderer.sprite = CreatePlaceholderSprite();
        }
    }

    public override void Spawned()
    {
        gameObject.name = $"Player_{Object.InputAuthority.PlayerId}";

        if (Object.HasStateAuthority)
        {
            PlayerColorIndex = Object.InputAuthority.PlayerId % Palette.Length;
        }

        ApplyColor();
    }

    public override void Render()
    {
        ApplyColor();
    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput(out NetworkInputData input) && input.MoveDirection.sqrMagnitude > 0.0001f)
        {
            transform.position += (Vector3)input.MoveDirection * (_moveSpeed * Runner.DeltaTime);
        }
    }

    private void ApplyColor()
    {
        if (_spriteRenderer == null)
        {
            return;
        }

        int index = ((PlayerColorIndex % Palette.Length) + Palette.Length) % Palette.Length;
        _spriteRenderer.color = Palette[index];
    }

    /// <summary>
    /// Genera un cuadrado blanco de relleno para poder ver y probar el
    /// movimiento en red antes de tener el arte final del juego.
    /// </summary>
    private static Sprite CreatePlaceholderSprite()
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        var pixels = new Color32[size * size];
        var white = new Color32(255, 255, 255, 255);
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = white;
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
