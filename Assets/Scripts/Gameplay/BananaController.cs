using Fusion;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(CircleCollider2D))]
public class BananaController : NetworkBehaviour
{
    [Tooltip("Puntos que suma (positivo) o resta (negativo) al jugador que la atrapa.")]
    [SerializeField] private int _pointValue = 5;

    [Tooltip("Velocidad de caida (unidades/seg). La randomiza BananaGameManager al spawnear.")]
    [SerializeField] private float _fallSpeed = 4f;

    [Networked] private NetworkBool Consumed { get; set; }
    [Networked] private NetworkBool IsStunPeel { get; set; }
    [Networked] private float StunSeconds { get; set; }
    [Networked] private int NetPointValue { get; set; }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            NetPointValue = _pointValue;
        }
    }

    public override void Render()
    {
        if (NetPointValue >= BananaRushConfig.BananaGreenPoints)
        {
            var renderer = GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.color = new Color(0.2f, 0.95f, 0.28f, 1f);
            }
        }
    }

    public void Configure(float fallSpeed)
    {
        Configure(fallSpeed, _pointValue);
    }

    public void Configure(float fallSpeed, int points)
    {
        _fallSpeed = fallSpeed;
        _pointValue = points;
        if (Object != null && Object.HasStateAuthority)
        {
            NetPointValue = points;
        }
    }

    public void BecomeStunPeel(float stunSeconds)
    {
        IsStunPeel = true;
        StunSeconds = stunSeconds;
        _pointValue = 0;
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority || Consumed)
        {
            return;
        }

        Vector3 next = transform.position + Vector3.down * (_fallSpeed * Runner.DeltaTime);

        if (next.y <= BananaRushConfig.GroundTopY)
        {
            DespawnSelf();
            return;
        }

        transform.position = next;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (Consumed)
        {
            return;
        }

        var player = other.GetComponent<PlayerController>();
        BananaGameManager manager = BananaGameManager.Instance;
        if (player == null || !player.Object.HasInputAuthority || manager == null ||
            !manager.MatchStarted || manager.SelectedGame != MiniGameId.BananaRain)
        {
            return;
        }

        if (IsStunPeel)
        {
            player.ApplyStun(StunSeconds > 0f ? StunSeconds : 3f);
            GameFeedback.WorldPopup(player.transform.position + Vector3.up, "CASCARA", new Color(1f, 0.85f, 0.25f));
        }
        else
        {
            int points = NetPointValue != 0 ? NetPointValue : _pointValue;
            player.ReceiveBananaHit(points);
        }

        RequestConsume(player.Object.InputAuthority);
    }

    private void RequestConsume(PlayerRef claimant)
    {
        if (Object == null || Consumed)
        {
            return;
        }

        if (Object.HasStateAuthority)
        {
            if (IsValidConsumeClaim(claimant))
            {
                DespawnSelf();
            }
        }
        else
        {
            RPC_RequestConsume(claimant);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestConsume(PlayerRef claimant, RpcInfo info = default)
    {
        if (info.Source == claimant && IsValidConsumeClaim(claimant))
        {
            DespawnSelf();
        }
    }

    private bool IsValidConsumeClaim(PlayerRef claimant)
    {
        BananaGameManager manager = BananaGameManager.Instance;
        if (Consumed || manager == null || !manager.MatchStarted || manager.SelectedGame != MiniGameId.BananaRain)
        {
            return false;
        }

        foreach (var player in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (player != null && player.IsSpawned && !player.IsEliminated &&
                player.Object.InputAuthority == claimant &&
                (player.transform.position - transform.position).sqrMagnitude <= 1.25f * 1.25f)
            {
                return true;
            }
        }

        return false;
    }

    private void DespawnSelf()
    {
        if (Consumed)
        {
            return;
        }

        Consumed = true;
        Runner.Despawn(Object);
    }
}
