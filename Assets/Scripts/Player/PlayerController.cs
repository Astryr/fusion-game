using Fusion;
using UnityEngine;

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
    [Networked] public int CupScore { get; set; }
    [Networked] public NetworkString<_32> Nickname { get; set; }
    [Networked] public NetworkBool IsReady { get; set; }
    [Networked] public NetworkBool IsEliminated { get; set; }
    [Networked] public float Heat { get; set; }

    // Efectos puntuales (popup, destello, sonido): la autoridad sube el contador y cada cliente lo reproduce al verlo cambiar.
    [Networked] private int FxSeq { get; set; }
    [Networked] private PlayerFx FxKind { get; set; }
    [Networked] private int FxValue { get; set; }

    public static PlayerController Local { get; private set; }

    public bool IsSpawned => Object != null && Object.IsValid;

    public string DisplayName
    {
        get
        {
            if (!IsSpawned)
            {
                return "Jugador";
            }

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
    private int _seenFxSeq;
    private float _flashUntil;
    private float _flashLength = 1f;
    private Color _flashColor = Color.white;
    private float _lastRemoteSfxAt;
    private bool _wasGrounded = true;
    private float _groundedY;
    private float _airborneSince = -1f;

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

        _seenFxSeq = FxSeq;
        _wasGrounded = NetGrounded;
        _groundedY = transform.position.y;

        if (Object.HasInputAuthority)
        {
            Local = this;
        }

        if (Object.HasStateAuthority)
        {
            PlayerColorIndex = Object.InputAuthority.PlayerId % Palette.Length;
            string nickname = NetworkRunnerHandler.Instance != null ? NetworkRunnerHandler.Instance.LocalNickname : null;
            Nickname = MakeUniqueName(nickname);
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
        PollFx();
        ApplyColor();
        UpdateJumpSound();
        UpdateFacingAndAnimator();
        if (_nameLabel != null && IsSpawned)
        {
            string display = DisplayName;
            if (_nameLabel.text != display)
            {
                _nameLabel.text = display;
            }

            Color labelColor = IsReady && BananaGameManager.Instance != null && BananaGameManager.Instance.IsInLobby
                ? new Color(0.45f, 1f, 0.45f)
                : Color.white;
            if (_nameLabel.color != labelColor)
            {
                _nameLabel.color = labelColor;
            }
        }
    }

    private string MakeUniqueName(string requested)
    {
        string fallback = $"Jugador {Object.InputAuthority.PlayerId + 1}";
        string trimmed = string.IsNullOrWhiteSpace(requested) ? string.Empty : requested.Trim();
        string baseName = trimmed.Length == 0 || trimmed.Equals("Jugador", System.StringComparison.OrdinalIgnoreCase) ? fallback : trimmed;

        string candidate = baseName;
        int suffix = 2;
        while (IsNameTaken(candidate))
        {
            candidate = $"{baseName} {suffix++}";
        }

        return candidate;
    }

    private bool IsNameTaken(string candidate)
    {
        foreach (PlayerController other in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (other != null && other != this && other.IsSpawned &&
                string.Equals(other.Nickname.ToString(), candidate, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public override void FixedUpdateNetwork()
    {
        if (!GetInput(out NetworkInputData input))
        {
            return;
        }

        bool jumpPressed = input.Buttons.WasPressed(_previousButtons, InputButton.Jump);
        bool actionPressed = input.Buttons.WasPressed(_previousButtons, InputButton.Action);
        _previousButtons = input.Buttons;

        if (ControlMode == PlayerControlMode.ChestBeat)
        {
            if (!IsEliminated)
            {
                TickChestBeat(actionPressed || jumpPressed);
            }

            NetHorizontal = 0f;
            return;
        }

        if (ControlMode != PlayerControlMode.Platformer || IsEliminated)
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
    public void RPC_PrepareForMatch(RpcInfo info = default)
    {
        if (!IsManagerStateAuthority(info.Source) || BananaGameManager.Instance.Phase != MatchPhase.Playing)
        {
            return;
        }

        ApplyPrepareForMatch();
    }

    private void ApplyPrepareForMatch()
    {
        Score = 0;
        Heat = 0f;
        IsEliminated = false;
        IsReady = false;
        _grounded = true;
        _verticalVelocity = 0f;
        NetGrounded = true;

        BananaGameManager manager = BananaGameManager.Instance;
        RankingService.BeginMatch(manager != null ? manager.GetPlayers().Length : 1, manager != null && manager.AllowSoloPractice);
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
        _grounded = true;
        _verticalVelocity = 0f;
        NetGrounded = true;
        SetWorldBounds(-BananaRushConfig.PlayerClampX, BananaRushConfig.PlayerClampX, BananaRushConfig.GroundTopY, -8f);
        SetPushEnabled(false);
        SetControlMode(PlayerControlMode.Platformer);
        // Si no, tras el parkour todos quedan clampeados en el borde derecho.
        transform.position = NetworkRunnerHandler.GetSpawnPosition(Object.InputAuthority.PlayerId);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_ResetForLobby(RpcInfo info = default)
    {
        if (!IsManagerStateAuthority(info.Source) || BananaGameManager.Instance.Phase != MatchPhase.Lobby)
        {
            return;
        }

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
    public void RPC_ForceUnready(RpcInfo info = default)
    {
        if (!IsManagerStateAuthority(info.Source) || BananaGameManager.Instance.Phase != MatchPhase.Lobby)
        {
            return;
        }

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
    public void RPC_Teleport(Vector3 position, RpcInfo info = default)
    {
        if (!IsManagerStateAuthority(info.Source) ||
            BananaGameManager.Instance.Phase == MatchPhase.Results ||
            Mathf.Abs(position.x) > BananaRushConfig.PlayerClampX ||
            position.y < -2f || position.y > 25f)
        {
            return;
        }

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
        EmitFx(PlayerFx.Out);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_Eliminate(RpcInfo info = default)
    {
        if ((info.Source != Object.InputAuthority && !IsManagerStateAuthority(info.Source)) ||
            BananaGameManager.Instance == null || !BananaGameManager.Instance.MatchStarted)
        {
            return;
        }

        if (!IsEliminated)
        {
            IsEliminated = true;
            EmitFx(PlayerFx.Out);
        }
    }

    public void ReceiveBananaHit(int points, PlayerRef effectAuthority)
    {
        if (!IsValidBananaEffectAuthority(effectAuthority) || !IsValidBananaScore(points))
        {
            return;
        }

        if (!Object.HasStateAuthority)
        {
            RPC_ReceiveBananaHit(points, effectAuthority);
            return;
        }

        ApplyBananaHitOnAuthority(points);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ReceiveBananaHit(int points, PlayerRef effectAuthority, RpcInfo info = default)
    {
        if (info.Source != effectAuthority || !IsValidBananaEffectAuthority(effectAuthority) ||
            !IsValidBananaScore(points) || IsEliminated)
        {
            return;
        }

        ApplyBananaHitOnAuthority(points);
    }

    private void ApplyBananaHitOnAuthority(int points)
    {
        if (IsEliminated)
        {
            return;
        }

        MatchConfig rules = LiveConfig.Match;
        int previous = Score;
        Score = Mathf.Max(0, Score + points);
        EmitFx(PlayerFx.BananaPoints, points);

        if (previous < rules.BananaTargetScore && Score >= rules.BananaTargetScore)
        {
            BananaGameManager.Instance?.DeclareWinner(this, $"{rules.BananaTargetScore} puntos");
        }
    }

    private bool IsValidBananaEffectAuthority(PlayerRef source)
    {
        BananaGameManager manager = BananaGameManager.Instance;
        return manager != null && manager.MatchStarted && manager.SelectedGame == MiniGameId.BananaRain &&
               manager.Object.StateAuthority == source;
    }

    private static bool IsValidBananaScore(int points)
    {
        MatchConfig rules = LiveConfig.Match;
        return points == rules.BananaNormalPoints ||
               points == rules.BananaExplosivePoints ||
               points == rules.BananaGreenPoints;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetReady(NetworkBool ready)
    {
        if (BananaGameManager.Instance == null || !BananaGameManager.Instance.IsInLobby)
        {
            return;
        }

        IsReady = ready;
        GameFeedback.Toast(ready ? "Estas LISTO" : "Cancelaste el ready",
            ready ? new Color(0.55f, 1f, 0.6f) : new Color(1f, 0.45f, 0.4f));
        GameSfx.Play(ready ? Sfx.Ready : Sfx.Unready);
    }

    public void RequestToggleReady()
    {
        if (!Object.HasInputAuthority)
        {
            return;
        }

        RPC_SetReady(!IsReady);
    }

    public void SendChat(string text)
    {
        if (!IsSpawned || !Object.HasInputAuthority)
        {
            return;
        }

        string clean = GameChat.Sanitize(text);
        if (clean.Length > 0)
        {
            RPC_Chat(clean);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All, TickAligned = false)]
    private void RPC_Chat(string text, RpcInfo info = default)
    {
        // Source puede venir vacio; solo se descarta si dice ser otro jugador.
        if (info.Source != PlayerRef.None && info.Source != Object.InputAuthority)
        {
            return;
        }

        if (!LiveConfig.Match.ChatEnabled)
        {
            return;
        }

        string clean = GameChat.Sanitize(text);
        if (clean.Length > 0)
        {
            GameChat.Receive(Object.InputAuthority.PlayerId, DisplayName, TintColor, clean);
        }
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

        if (next.y <= _deathY && BananaGameManager.Instance != null && BananaGameManager.Instance.MatchStarted)
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

            EmitFx(PlayerFx.ChestPunch);
        }

        if (Heat >= 1f)
        {
            Eliminate();
            GameFeedback.Toast("Te lastimaste el pecho", new Color(1f, 0.45f, 0.4f));
        }
        else if (Score >= LiveConfig.Match.ChestTarget)
        {
            BananaGameManager.Instance?.DeclareWinner(this, "el mas fuerte");
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SetScore(int score, RpcInfo info = default)
    {
        BananaGameManager manager = BananaGameManager.Instance;
        if (manager == null || manager.Phase != MatchPhase.Playing || score < 0)
        {
            return;
        }

        bool managerParkourScore = manager.SelectedGame == MiniGameId.Parkour &&
                                   info.Source == manager.Object.StateAuthority;
        if (managerParkourScore)
        {
            if (score < 10 || score > 110)
            {
                return;
            }
        }
        else if (info.Source != Object.InputAuthority || !IsValidPlayerScoreChange(manager, score))
        {
            return;
        }

        bool progress = score > Score && (manager.SelectedGame == MiniGameId.TreeChop || manager.SelectedGame == MiniGameId.MemoryPuzzle);
        Score = score;
        if (progress)
        {
            EmitFx(PlayerFx.Progress, score);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_AddCupScore(int points, RpcInfo info = default)
    {
        if (!IsManagerStateAuthority(info.Source) || points < 1 || points > 4 * LiveConfig.Match.CupMultiplier)
        {
            return;
        }

        CupScore = Mathf.Max(0, CupScore + points);
        RankingService.OnCupPointsAwarded(points);
    }

    private bool IsValidPlayerScoreChange(BananaGameManager manager, int score)
    {
        return manager.SelectedGame switch
        {
            MiniGameId.MemoryPuzzle => score >= Score && score <= Score + 1 && score <= MemoryPuzzleMinigame.PairCount,
            MiniGameId.TreeChop => score <= LiveConfig.Match.TreeChopTarget && Mathf.Abs(score - Score) <= 1,
            _ => false,
        };
    }

    private static bool IsManagerStateAuthority(PlayerRef source)
    {
        BananaGameManager manager = BananaGameManager.Instance;
        return manager != null && manager.Object != null && source == manager.Object.StateAuthority;
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

        float flashLeft = _flashUntil - Time.time;
        if (flashLeft > 0f)
        {
            var flash = new Color(_flashColor.r, _flashColor.g, _flashColor.b, color.a);
            color = Color.Lerp(color, flash, 0.7f * Mathf.Clamp01(flashLeft / _flashLength));
        }

        _spriteRenderer.color = color;
    }

    // Solo la autoridad de estado llama a esto.
    private void EmitFx(PlayerFx kind, int value = 0)
    {
        FxKind = kind;
        FxValue = value;
        FxSeq++;
    }

    private void PollFx()
    {
        if (FxSeq == _seenFxSeq)
        {
            return;
        }

        _seenFxSeq = FxSeq;
        PlayFx(FxKind, FxValue);
    }

    private void PlayFx(PlayerFx kind, int value)
    {
        bool mine = Object.HasInputAuthority;
        Vector3 head = transform.position + Vector3.up;

        switch (kind)
        {
            case PlayerFx.BananaPoints:
            {
                bool green = value >= LiveConfig.Match.BananaGreenPoints;
                Color color = green
                    ? new Color(0.35f, 1f, 0.4f)
                    : value >= 0 ? new Color(1f, 0.9f, 0.2f) : new Color(1f, 0.35f, 0.25f);
                GameFeedback.WorldPopup(head + Vector3.up * 0.1f, value >= 0 ? $"+{value}" : value.ToString(), color);
                Flash(color, 0.22f);
                GameSfx.Play(value < 0 ? Sfx.Explosion : green ? Sfx.PickupGreen : Sfx.Pickup, mine ? 1f : 0.4f);
                break;
            }
            case PlayerFx.Out:
                GameFeedback.WorldPopup(head, "OUT", new Color(1f, 0.4f, 0.35f));
                Flash(new Color(1f, 0.25f, 0.2f), 0.4f);
                GameSfx.Play(Sfx.Out, mine ? 1f : 0.6f);
                break;
            case PlayerFx.ChestPunch:
                Flash(new Color(1f, 0.55f, 0.35f), 0.12f);
                if (mine)
                {
                    GameFeedback.WorldPopup(head, "PUM", new Color(1f, 0.55f, 0.35f));
                }

                PlayThrottled(Sfx.Thump, mine, mine ? 0.9f : 0.3f);
                break;
            case PlayerFx.Progress:
            {
                Flash(Color.white, 0.12f);
                if (mine)
                {
                    GameFeedback.WorldPopup(head, "+1", new Color(0.6f, 1f, 0.5f));
                }

                BananaGameManager manager = BananaGameManager.Instance;
                bool memory = manager != null && manager.SelectedGame == MiniGameId.MemoryPuzzle;
                PlayThrottled(memory ? Sfx.Match : Sfx.Chop, mine, mine ? 0.8f : 0.3f);
                break;
            }
        }
    }

    private void Flash(Color color, float seconds)
    {
        _flashColor = color;
        _flashLength = seconds;
        _flashUntil = Time.time + seconds;
    }

    // Los golpes de otros jugadores pueden ser decenas por segundo; sin limite tapan todo.
    private void PlayThrottled(Sfx sfx, bool mine, float gain)
    {
        if (!mine)
        {
            if (Time.unscaledTime - _lastRemoteSfxAt < 0.12f)
            {
                return;
            }

            _lastRemoteSfxAt = Time.unscaledTime;
        }

        GameSfx.Play(sfx, gain);
    }

    // Saltar y caerse del borde ponen NetGrounded en falso; solo suena si ademas se despega hacia arriba.
    private void UpdateJumpSound()
    {
        bool grounded = NetGrounded;
        float y = transform.position.y;

        if (grounded)
        {
            _groundedY = y;
            _airborneSince = -1f;
        }
        else if (_wasGrounded)
        {
            _airborneSince = Time.time;
        }

        if (_airborneSince >= 0f)
        {
            if (y > _groundedY + 0.03f && !IsEliminated)
            {
                GameSfx.Play(Sfx.Jump, Object.HasInputAuthority ? 0.7f : 0.25f);
                _airborneSince = -1f;
            }
            else if (Time.time - _airborneSince > 0.15f)
            {
                _airborneSince = -1f;
            }
        }

        _wasGrounded = grounded;
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
