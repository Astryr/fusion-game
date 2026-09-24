using UnityEngine;

public class ChestBeatMinigame : MonoBehaviour, IMiniGame
{
    public MiniGameId Id => MiniGameId.ChestBeat;

    private BananaGameManager _director;
    private bool _started;

    public void Setup(BananaGameManager director)
    {
        _director = director;
    }

    public void OnMatchStarted()
    {
        _started = true;
        int index = 0;
        var players = _director.GetPlayers();
        float spread = 2.2f;
        foreach (var player in players)
        {
            float x = (index - (players.Length - 1) * 0.5f) * spread;
            if (_director.Object.HasStateAuthority)
            {
                player.Teleport(new Vector3(x, BananaRushConfig.GroundTopY, 0f));
            }
            player.SetControlMode(PlayerControlMode.ChestBeat);
            player.SetPushEnabled(false);
            index++;
        }
    }

    public void Tick(float deltaTime, bool hasAuthority)
    {
        if (!_started || !hasAuthority)
        {
            return;
        }

        int alive = 0;
        PlayerController last = null;
        PlayerController leader = null;
        foreach (var player in _director.GetPlayers())
        {
            if (player.IsEliminated)
            {
                continue;
            }

            alive++;
            last = player;
            if (leader == null || player.Score > leader.Score)
            {
                leader = player;
            }

            if (player.Score >= 50)
            {
                _director.DeclareWinner(player, "el mas fuerte");
                return;
            }
        }

        if (alive <= 1 && _director.GetPlayers().Length > 1)
        {
            _director.DeclareWinner(last, "el unico sin lastimarse");
        }
        else if (_director.MatchTime >= 45f)
        {
            _director.DeclareWinner(leader, "mas atencion");
        }
    }

    public void OnMatchEnded()
    {
        _started = false;
    }

    public void Cleanup()
    {
        _started = false;
    }
}
