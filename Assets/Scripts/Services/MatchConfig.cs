using System;
using Fusion;

public struct MatchConfig : INetworkStruct
{
    public const int AllMinigamesMask = (1 << 6) - 1;

    public NetworkBool Valid;
    public int Revision;

    public int BananaTargetScore;
    public int BananaNormalPoints;
    public int BananaGreenPoints;
    public int BananaExplosivePoints;
    public float BananaGreenChance;
    public float BananaSpawnIntervalScale;
    public float BananaTimeLimit;

    public float ParkourAvalancheSpeed;
    public float LogMatchSeconds;
    public int ChestTarget;
    public float MemotestPeekSeconds;
    public int TreeChopTarget;

    public float CountdownSeconds;
    public int CupMultiplier;
    public NetworkBool SoloPracticeEnabled;
    public NetworkBool ChatEnabled;
    public int DisabledMinigames;

    public static MatchConfig Defaults()
    {
        return new MatchConfig
        {
            Revision = 0,
            BananaTargetScore = BananaRushConfig.BananaTargetScore,
            BananaNormalPoints = BananaRushConfig.BananaNormalPoints,
            BananaGreenPoints = BananaRushConfig.BananaGreenPoints,
            BananaExplosivePoints = BananaRushConfig.BananaExplosivePoints,
            BananaGreenChance = BananaRushConfig.BananaGreenChance,
            BananaSpawnIntervalScale = 1f,
            BananaTimeLimit = BananaRushConfig.BananaRegularDuration,
            ParkourAvalancheSpeed = BananaRushConfig.ParkourAvalancheSpeed,
            LogMatchSeconds = 95f,
            ChestTarget = 50,
            MemotestPeekSeconds = 7f,
            TreeChopTarget = 30,
            CountdownSeconds = 5f,
            CupMultiplier = 1,
            SoloPracticeEnabled = true,
            ChatEnabled = true,
            DisabledMinigames = 0,
        };
    }

    public bool IsDisabled(MiniGameId id)
    {
        return (DisabledMinigames & (1 << (int)id)) != 0;
    }

    public MiniGameId FirstEnabled(MiniGameId preferred)
    {
        if (!IsDisabled(preferred))
        {
            return preferred;
        }

        for (int i = 0; i < MiniGameNames.DisplayNames.Length; i++)
        {
            if (!IsDisabled((MiniGameId)i))
            {
                return (MiniGameId)i;
            }
        }

        return preferred;
    }

    public int ComputeHash()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + Revision;
            hash = hash * 31 + BananaTargetScore;
            hash = hash * 31 + BananaNormalPoints;
            hash = hash * 31 + BananaGreenPoints;
            hash = hash * 31 + BananaExplosivePoints;
            hash = hash * 31 + BitConverter.SingleToInt32Bits(BananaGreenChance);
            hash = hash * 31 + BitConverter.SingleToInt32Bits(BananaSpawnIntervalScale);
            hash = hash * 31 + BitConverter.SingleToInt32Bits(BananaTimeLimit);
            hash = hash * 31 + BitConverter.SingleToInt32Bits(ParkourAvalancheSpeed);
            hash = hash * 31 + BitConverter.SingleToInt32Bits(LogMatchSeconds);
            hash = hash * 31 + ChestTarget;
            hash = hash * 31 + BitConverter.SingleToInt32Bits(MemotestPeekSeconds);
            hash = hash * 31 + TreeChopTarget;
            hash = hash * 31 + BitConverter.SingleToInt32Bits(CountdownSeconds);
            hash = hash * 31 + CupMultiplier;
            hash = hash * 31 + (SoloPracticeEnabled ? 1 : 0);
            hash = hash * 31 + (ChatEnabled ? 1 : 0);
            hash = hash * 31 + DisabledMinigames;
            return hash;
        }
    }
}
