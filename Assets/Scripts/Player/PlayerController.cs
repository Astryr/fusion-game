using Fusion;
using UnityEngine;

/// <summary>
/// Personaje jugable en red (Moniko). El movimiento es simple a proposito:
/// solo izquierda/derecha y un salto chico, calculado a mano en
/// <see cref="FixedUpdateNetwork"/> (nada de Rigidbody2D dinamico) para que
/// el piso plano del nivel "bloquee" siempre igual en todos los clientes,
/// sin pelearse con el <c>NetworkTransform</c> que replica la posicion.
///
/// El Rigidbody2D se deja en modo Kinematic solo para que Unity dispare
/// <c>OnTriggerEnter2D</c> cuando una banana lo toca (se necesita al menos
/// un Rigidbody2D en el par para que la fisica 2D genere el evento).
///
/// El tinte de color, el nickname y el puntaje son propiedades
/// <c>[Networked]</c>: las fija el cliente due&#241;o (StateAuthority) y Fusion
/// las replica solas a todos los demas clientes.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class PlayerController : NetworkBehaviour
{
    [Header("Movimiento")]
    [SerializeField] private float _moveSpeed = 8f;
    [SerializeField] private float _jumpForce = 9f;
    [SerializeField] private float _gravity = 20f;
    [SerializeField] private float _maxFallSpeed = 20f;

    [Header("Referencias visuales")]
    [SerializeField] private Animator _animator;

    [Networked] private int PlayerColorIndex { get; set; }
    [Networked] private float NetHorizontal { get; set; }
    [Networked] private NetworkBool NetGrounded { get; set; }

    /// <summary>Puntos acumulados. Solo el due&#241;o de este jugador lo modifica.</summary>
    [Networked] public int Score { get; set; }

    /// <summary>Nombre elegido en el menu principal antes de conectarse.</summary>
    [Networked] public NetworkString<_32> Nickname { get; set; }

    private SpriteRenderer _spriteRenderer;
    private Rigidbody2D _rigidbody;
    private float _verticalVelocity;
    private bool _grounded = true;
    private bool _facingRight = true;

    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly int GroundedParam = Animator.StringToHash("Grounded");

    // Colores pensados para multiplicarse sobre el pelaje marron de Moniko y
    // seguir viendose distintos entre si (el jugador 1 queda con su color
    // original, el resto recibe un tinte).
    private static readonly Color[] Palette =
    {
        Color.white,
        new Color(1.00f, 0.50f, 0.50f),
        new Color(0.55f, 0.75f, 1.00f),
        new Color(0.60f, 1.00f, 0.60f),
        new Color(1.00f, 0.95f, 0.50f),
        new Color(0.80f, 0.55f, 1.00f),
        new Color(0.55f, 1.00f, 1.00f),
        new Color(1.00f, 0.60f, 0.85f),
    };

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();

        _rigidbody = GetComponent<Rigidbody2D>();
        _rigidbody.bodyType = RigidbodyType2D.Kinematic;
        _rigidbody.gravityScale = 0f;
        _rigidbody.freezeRotation = true;
        _rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;

        if (_animator == null)
        {
            _animator = GetComponent<Animator>();
        }
    }

    public override void Spawned()
    {
        gameObject.name = $"Player_{Object.InputAuthority.PlayerId}";

        if (Object.HasStateAuthority)
        {
            PlayerColorIndex = Object.InputAuthority.PlayerId % Palette.Length;

            string nickname = NetworkRunnerHandler.Instance != null ? NetworkRunnerHandler.Instance.LocalNickname : null;
            Nickname = string.IsNullOrWhiteSpace(nickname) ? $"Jugador {Object.InputAuthority.PlayerId + 1}" : nickname;
        }

        ApplyColor();
    }

    public override void Render()
    {
        ApplyColor();
        UpdateFacingAndAnimator();
    }

    public override void FixedUpdateNetwork()
    {
        if (!GetInput(out NetworkInputData input))
        {
            return;
        }

        bool roundEnded = BananaGameManager.Instance != null && BananaGameManager.Instance.IsGameOver;
        float horizontal = roundEnded ? 0f : Mathf.Clamp(input.Horizontal, -1f, 1f);
        bool jumpPressed = !roundEnded && input.JumpPressed;

        if (_grounded && jumpPressed)
        {
            _verticalVelocity = _jumpForce;
            _grounded = false;
        }
        else if (!_grounded)
        {
            _verticalVelocity = Mathf.Max(_verticalVelocity - _gravity * Runner.DeltaTime, -_maxFallSpeed);
        }
        else
        {
            _verticalVelocity = 0f;
        }

        Vector3 next = transform.position + new Vector3(horizontal * _moveSpeed, _verticalVelocity, 0f) * Runner.DeltaTime;
        next.x = Mathf.Clamp(next.x, -BananaRushConfig.PlayerClampX, BananaRushConfig.PlayerClampX);

        if (next.y <= BananaRushConfig.GroundTopY)
        {
            next.y = BananaRushConfig.GroundTopY;
            _verticalVelocity = 0f;
            _grounded = true;
        }

        // Asignacion directa (no rb.MovePosition): al ser Kinematic el motor
        // de fisica no necesita el paso de fisica para "ver" la nueva
        // posicion, y el NetworkTransform que replica el transform no se
        // desfasa un tick esperando el proximo Physics2D.Simulate.
        transform.position = next;

        NetHorizontal = horizontal;
        NetGrounded = _grounded;
    }

    /// <summary>
    /// Llamado por <see cref="BananaController"/> cuando esta banana toco a
    /// este jugador. Se auto-protege con HasStateAuthority: en cualquier
    /// cliente que reciba el aviso, solo el due&#241;o real de este jugador va a
    /// modificarle el puntaje (los demas clientes ven el mismo trigger local
    /// pero no tienen autoridad sobre este objeto, asi que no hacen nada).
    /// </summary>
    public void ReceiveBananaHit(int points)
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        Score = Mathf.Max(0, Score + points);
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

    private void UpdateFacingAndAnimator()
    {
        if (NetHorizontal > 0.05f)
        {
            _facingRight = true;
        }
        else if (NetHorizontal < -0.05f)
        {
            _facingRight = false;
        }

        if (_spriteRenderer != null)
        {
            _spriteRenderer.flipX = !_facingRight;
        }

        if (_animator != null)
        {
            _animator.SetFloat(SpeedParam, Mathf.Abs(NetHorizontal));
            _animator.SetBool(GroundedParam, NetGrounded);
        }
    }
}
