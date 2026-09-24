using Fusion;
using UnityEngine;

/// <summary>
/// Mono en red. En Shared Mode cada cliente tiene State + Input Authority
/// sobre su propio personaje. El ready, puntaje y eliminacion son
/// [Networked]; el ready se declara con un RPC para que todos vean el aviso.
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
    [SerializeField] private RuntimeAnimatorController _animatorController;

    [Networked] private int PlayerColorIndex { get; set; }
    [Networked] private float NetHorizontal { get; set; }
    [Networked] private NetworkBool NetGrounded { get; set; }
    [Networked] public int Score { get; set; }
    [Networked] public NetworkString<_32> Nickname { get; set; }
    [Networked] public NetworkBool IsReady { get; set; }
    [Networked] public NetworkBool IsEliminated { get; set; }
    [Networked] public NetworkBool IsStunned { get; set; }
    [Networked] public float Heat { get; set; }
    [Networked] public float StunUntil { get; set; }

    public static PlayerController Local { get; private set; }

    public string DisplayName
    {
        get
        {
            string name = Nickname.ToString();
            return string.IsNullOrEmpty(name) ? $"Jugador {Object.InputAuthority.PlayerId + 1}" : name;
        }
    }

    public Color TintColor => Palette[((PlayerColorIndex % Palette.Length) + Palette.Length) % Palette.Length];
    public PlayerControlMode ControlMode { get; private set; } = PlayerControlMode.Disabled;

    private SpriteRenderer _spriteRenderer;
    private Rigidbody2D _rigidbody;
    private TextMesh _nameLabel;
    private float _verticalVelocity;
    private bool _grounded = true;
    private bool _facingRight = true;
    private float _minX = -BananaRushConfig.PlayerClampX;
    private float _maxX = BananaRushConfig.PlayerClampX;
    private float _floorY = BananaRushConfig.GroundTopY;
    private float _deathY = -8f;
    private bool _allowPush;
    private NetworkButtons _previousButtons;

    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly int GroundedParam = Animator.StringToHash("Grounded");

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
        EnsureAnimatorReady();
        EnsureNameLabel();
    }

    private void OnEnable()
    {
        EnsureAnimatorReady();
    }

    public override void Spawned()
    {
        gameObject.name = $"Player_{Object.InputAuthority.PlayerId}";
        EnsureAnimatorReady();
        EnsureNameLabel();

        if (Object.HasInputAuthority)
        {
            Local = this;
        }

        if (Object.HasStateAuthority)
        {
            PlayerColorIndex = Object.InputAuthority.PlayerId % Palette.Length;
            string nickname = NetworkRunnerHandler.Instance != null ? NetworkRunnerHandler.Instance.LocalNickname : null;
            Nickname = string.IsNullOrWhiteSpace(nickname) ? $"Jugador {Object.InputAuthority.PlayerId + 1}" : nickname;
            ResetForLobby();
        }

        ApplyColor();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Local == this)
        {
            Local = null;
        }
    }

    public override void Render()
    {
        ApplyColor();
        UpdateFacingAndAnimator();
        if (_nameLabel != null)
        {
            _nameLabel.text = DisplayName;
            _nameLabel.color = IsReady && BananaGameManager.Instance != null && BananaGameManager.Instance.IsInLobby
                ? new Color(0.45f, 1f, 0.45f)
                : Color.white;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority && IsStunned && Runner.SimulationTime >= StunUntil)
        {
            IsStunned = false;
        }

        if (!GetInput(out NetworkInputData input))
        {
            return;
        }

        bool jumpPressed = input.Buttons.WasPressed(_previousButtons, InputButton.Jump);
        bool actionPressed = input.Buttons.WasPressed(_previousButtons, InputButton.Action);
        _previousButtons = input.Buttons;

        if (ControlMode == PlayerControlMode.ChestBeat)
        {
            TickChestBeat(actionPressed || jumpPressed);
            NetHorizontal = 0f;
            return;
        }

        if (ControlMode != PlayerControlMode.Platformer || IsEliminated || IsStunned)
        {
            NetHorizontal = 0f;
            return;
        }

        float horizontal = Mathf.Clamp(input.Horizontal, -1f, 1f);
        TickPlatformer(horizontal, jumpPressed);
    }

    public void SetControlMode(PlayerControlMode mode)
    {
        ControlMode = mode;
    }

    public void SetWorldBounds(float minX, float maxX, float floorY, float deathY)
    {
        _minX = minX;
        _maxX = maxX;
        _floorY = floorY;
        _deathY = deathY;
    }

    public void SetPushEnabled(bool enabled)
    {
        _allowPush = enabled;
    }

    public void PrepareForMatch(MiniGameId game)
    {
        if (Object.HasStateAuthority)
        {
            ApplyPrepareForMatch();
            return;
        }

        RPC_PrepareForMatch();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_PrepareForMatch()
    {
        ApplyPrepareForMatch();
    }

    private void ApplyPrepareForMatch()
    {
        Score = 0;
        Heat = 0f;
        IsEliminated = false;
        IsStunned = false;
        IsReady = false;
        _grounded = true;
        _verticalVelocity = 0f;
        NetGrounded = true;
    }

    public void ResetForLobby()
    {
        if (!Object.HasStateAuthority)
        {
            RPC_ResetForLobby();
            return;
        }

        Score = 0;
        Heat = 0f;
        IsReady = false;
        IsEliminated = false;
        IsStunned = false;
        _grounded = true;
        _verticalVelocity = 0f;
        NetGrounded = true;
        SetWorldBounds(-BananaRushConfig.PlayerClampX, BananaRushConfig.PlayerClampX, BananaRushConfig.GroundTopY, -8f);
        SetPushEnabled(false);
        SetControlMode(PlayerControlMode.Platformer);
        transform.position = new Vector3(transform.position.x, BananaRushConfig.GroundTopY, 0f);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_ResetForLobby()
    {
        ResetForLobby();
    }

    public void ForceUnready()
    {
        if (Object.HasStateAuthority)
        {
            IsReady = false;
            return;
        }

        RPC_ForceUnready();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_ForceUnready()
    {
        IsReady = false;
    }

    public void Teleport(Vector3 position)
    {
        if (Object.HasStateAuthority)
        {
            ApplyTeleport(position);
            return;
        }

        RPC_Teleport(position);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_Teleport(Vector3 position)
    {
        ApplyTeleport(position);
    }

    private void ApplyTeleport(Vector3 position)
    {
        transform.position = position;
        _verticalVelocity = 0f;
        _grounded = true;
        NetGrounded = true;
    }

    public void Eliminate()
    {
        if (IsEliminated)
        {
            return;
        }

        if (!Object.HasStateAuthority)
        {
            RPC_Eliminate();
            return;
        }

        IsEliminated = true;
        GameFeedback.WorldPopup(transform.position + Vector3.up, "OUT", new Color(1f, 0.4f, 0.35f));
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_Eliminate()
    {
        if (!IsEliminated)
        {
            IsEliminated = true;
            GameFeedback.WorldPopup(transform.position + Vector3.up, "OUT", new Color(1f, 0.4f, 0.35f));
        }
    }

    public void ApplyStun(float seconds)
    {
        if (!Object.HasStateAuthority)
        {
            RPC_ApplyStun(seconds);
            return;
        }

        IsStunned = true;
        StunUntil = Runner.SimulationTime + seconds;
        GameFeedback.WorldPopup(transform.position + Vector3.up, "STUN", new Color(1f, 0.85f, 0.3f));
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_ApplyStun(float seconds)
    {
        IsStunned = true;
        StunUntil = Runner.SimulationTime + seconds;
        GameFeedback.WorldPopup(transform.position + Vector3.up, "STUN", new Color(1f, 0.85f, 0.3f));
    }

    public void ReceiveBananaHit(int points)
    {
        if (!Object.HasStateAuthority || IsEliminated)
        {
            return;
        }

        int previous = Score;
        Score = Mathf.Max(0, Score + points);
        Color color = points >= 0 ? new Color(1f, 0.9f, 0.2f) : new Color(1f, 0.35f, 0.25f);
        string label = points >= 0 ? $"+{points}" : points.ToString();
        GameFeedback.WorldPopup(transform.position + Vector3.up * 1.1f, label, color);

        if (BananaGameManager.Instance != null && BananaGameManager.Instance.SelectedGame == MiniGameId.BananaRain)
        {
            BananaGameManager.Instance.RPC_ShowFeedback($"{DisplayName}: {label} ({Score})", points >= 0 ? 0 : 2);
        }

        if (previous < BananaRushConfig.BananaTargetScore && Score >= BananaRushConfig.BananaTargetScore)
        {
            BananaGameManager.Instance?.DeclareWinner(this, "100 puntos");
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetReady(NetworkBool ready)
    {
        if (BananaGameManager.Instance == null || !BananaGameManager.Instance.IsInLobby)
        {
            return;
        }

        IsReady = ready;
        BananaGameManager.Instance.RPC_ShowFeedback(ready ? $"{DisplayName} esta LISTO" : $"{DisplayName} cancelo el ready", ready ? 0 : 2);
    }

    public void RequestToggleReady()
    {
        if (!Object.HasInputAuthority)
        {
            return;
        }

        RPC_SetReady(!IsReady);
    }

    private void TickPlatformer(float horizontal, bool jumpPressed)
    {
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

        if (_allowPush)
        {
            next.x += ComputePush() * Runner.DeltaTime;
        }

        next.x = Mathf.Clamp(next.x, _minX, _maxX);
        ResolveGround(ref next);

        transform.position = next;
        NetHorizontal = horizontal;
        NetGrounded = _grounded;

        if (next.y <= _deathY)
        {
            Eliminate();
        }
    }

    private void TickChestBeat(bool pressed)
    {
        Heat = Mathf.Max(0f, Heat - Runner.DeltaTime * 0.28f);
        if (pressed)
        {
            Heat = Mathf.Min(1.15f, Heat + 0.11f);
            if (Heat < 0.78f)
            {
                Score += 3;
            }
            else
            {
                Score += 1;
            }

            GameFeedback.WorldPopup(transform.position + Vector3.up, "PUM", new Color(1f, 0.55f, 0.35f));
        }

        if (Heat >= 1f)
        {
            Eliminate();
            BananaGameManager.Instance?.RPC_ShowFeedback($"{DisplayName} se lastimo el pecho", 2);
        }
        else if (Score >= 50)
        {
            BananaGameManager.Instance?.DeclareWinner(this, "el mas fuerte");
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SetScore(int score)
    {
        Score = score;
    }

    private void ResolveGround(ref Vector3 next)
    {
        int groundMask = LayerMask.GetMask("Ground");
        if (groundMask != 0 && _verticalVelocity <= 0f)
        {
            RaycastHit2D hit = Physics2D.BoxCast(
                (Vector2)next + new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.7f),
                0f,
                Vector2.down,
                0.2f,
                groundMask);

            if (hit.collider != null)
            {
                next.y = hit.point.y;
                _verticalVelocity = 0f;
                _grounded = true;
                return;
            }
        }

        if (next.y <= _floorY)
        {
            next.y = _floorY;
            _verticalVelocity = 0f;
            _grounded = true;
            return;
        }

        _grounded = false;
    }

    private float ComputePush()
    {
        float push = 0f;
        Collider2D[] hits = Physics2D.OverlapBoxAll(transform.position + Vector3.up * 0.45f, new Vector2(0.7f, 0.85f), 0f);
        foreach (var hit in hits)
        {
            var other = hit.GetComponent<PlayerController>();
            if (other == null || other == this || other.IsEliminated)
            {
                continue;
            }

            float dir = Mathf.Sign(transform.position.x - other.transform.position.x);
            if (Mathf.Abs(dir) < 0.01f)
            {
                dir = Object.InputAuthority.PlayerId > other.Object.InputAuthority.PlayerId ? 1f : -1f;
            }

            push += dir * 5.5f;
        }

        return push;
    }

    private void ApplyColor()
    {
        if (_spriteRenderer == null)
        {
            return;
        }

        Color color = TintColor;
        if (IsEliminated)
        {
            color *= new Color(0.35f, 0.35f, 0.35f, 0.55f);
        }
        else if (Heat > 0.15f)
        {
            color = Color.Lerp(color, new Color(1f, 0.15f, 0.1f), Mathf.Clamp01((Heat - 0.15f) / 0.85f));
        }
        else if (IsStunned)
        {
            color = Color.Lerp(color, new Color(1f, 0.95f, 0.35f), 0.55f);
        }

        _spriteRenderer.color = color;
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

        if (!CanDriveAnimator())
        {
            EnsureAnimatorReady();
            if (!CanDriveAnimator())
            {
                return;
            }
        }

        _animator.SetFloat(SpeedParam, Mathf.Abs(NetHorizontal));
        _animator.SetBool(GroundedParam, NetGrounded);
    }

    private void EnsureNameLabel()
    {
        if (_nameLabel != null)
        {
            return;
        }

        var labelGO = new GameObject("NameLabel");
        labelGO.transform.SetParent(transform, false);
        labelGO.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        _nameLabel = labelGO.AddComponent<TextMesh>();
        _nameLabel.anchor = TextAnchor.LowerCenter;
        _nameLabel.alignment = TextAlignment.Center;
        _nameLabel.characterSize = 0.085f;
        _nameLabel.fontSize = 42;
        _nameLabel.color = Color.white;
        var meshRenderer = labelGO.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.sortingLayerName = "Player";
            meshRenderer.sortingOrder = 10;
        }
    }

    private void EnsureAnimatorReady()
    {
        if (_animator == null)
        {
            _animator = GetComponent<Animator>();
        }

        if (_animator == null)
        {
            return;
        }

        if (_animator.runtimeAnimatorController == null && _animatorController != null)
        {
            _animator.runtimeAnimatorController = _animatorController;
        }

        _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        _animator.keepAnimatorStateOnDisable = true;
        if (!_animator.enabled)
        {
            _animator.enabled = true;
        }

        if (_animator.runtimeAnimatorController != null && !_animator.isInitialized && _animator.isActiveAndEnabled)
        {
            _animator.Rebind();
            _animator.Update(0f);
        }
    }

    private bool CanDriveAnimator()
    {
        return _animator != null
            && _animator.isActiveAndEnabled
            && _animator.runtimeAnimatorController != null
            && _animator.isInitialized;
    }
}
