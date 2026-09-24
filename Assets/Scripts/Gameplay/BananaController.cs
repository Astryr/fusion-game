using Fusion;
using UnityEngine;

/// <summary>
/// Banana que cae desde arriba del mapa. El puntaje que otorga (positivo
/// para la banana normal, negativo para la explosiva, ver
/// <see cref="_pointValue"/>) queda fijado en el prefab: Banana.prefab y
/// BananaExplosiva.prefab son dos prefabs distintos con ese valor distinto,
/// asi que la autoridad y las copias (proxies) en todos los clientes
/// arrancan siempre con el valor correcto sin necesitar sincronizarlo por
/// red.
///
/// El movimiento de caida solo lo calcula quien tiene StateAuthority (quien
/// la spawneo, ver <see cref="BananaGameManager"/>); el resto de los
/// clientes solo ven la posicion ya replicada por el NetworkTransform del
/// prefab.
/// </summary>
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

    /// <summary>Fija la velocidad de caida de esta instancia (llamado por BananaGameManager al spawnear).</summary>
    public void Configure(float fallSpeed)
    {
        _fallSpeed = fallSpeed;
    }

    public void BecomeStunPeel(float stunSeconds)
    {
        IsStunPeel = true;
        StunSeconds = stunSeconds;
        _pointValue = 0;
    }

    public override void FixedUpdateNetwork()
    {
        // El movimiento solo lo simula quien tiene la autoridad (quien la
        // spawneo); en los demas clientes el NetworkTransform del prefab ya
        // se encarga de mostrar la posicion replicada.
        if (!Object.HasStateAuthority || Consumed)
        {
            return;
        }

        Vector3 next = transform.position + Vector3.down * (_fallSpeed * Runner.DeltaTime);

        if (next.y <= BananaRushConfig.GroundTopY)
        {
            // Toco el piso sin que nadie la atrape: se pierde sin sumar ni restar puntos.
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
        if (player == null)
        {
            return;
        }

        // Se auto-protege adentro: solo el cliente due&#241;o de ESE jugador le va
        // a tocar el puntaje. Este trigger puede disparar en varios clientes
        // a la vez (cada uno simula fisica local de los proxies), por eso el
        // pedido de despawn de abajo tambien esta protegido contra duplicados.
        if (IsStunPeel)
        {
            player.ApplyStun(StunSeconds > 0f ? StunSeconds : 3f);
            GameFeedback.WorldPopup(player.transform.position + Vector3.up, "CASCARA", new Color(1f, 0.85f, 0.25f));
        }
        else
        {
            player.ReceiveBananaHit(_pointValue);
        }

        RequestConsume();
    }

    private void RequestConsume()
    {
        if (Object == null || Consumed)
        {
            return;
        }

        if (Object.HasStateAuthority)
        {
            DespawnSelf();
        }
        else
        {
            RPC_RequestConsume();
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestConsume()
    {
        DespawnSelf();
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
