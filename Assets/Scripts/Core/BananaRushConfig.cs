public static class BananaRushConfig
{
    public const float GroundTopY = 0f;
    public const float GroundThickness = 8f;
    public const float GroundHalfWidth = 15f;
    public const float BananaSpawnY = 8.5f;
    public const float CameraOrthographicSize = 7.5f;
    public const float CameraCenterY = 3.5f;
    public const float ReferenceAspect = 16f / 9f;
    public const float ScreenHalfWidth = CameraOrthographicSize * ReferenceAspect;
    public const float PlayerHalfWidth = 0.5f;
    public const float PlayerClampX = ScreenHalfWidth - PlayerHalfWidth;
    public const float BananaSpawnMargin = 0.35f;
    public const float BananaSpawnX = ScreenHalfWidth - BananaSpawnMargin;

    public const int BananaTargetScore = 100;
    public const int BananaNormalPoints = 5;
    public const int BananaExplosivePoints = -10;
    public const int BananaGreenPoints = 20;
    public const float BananaGreenChance = 0.035f;
    public const float BananaRegularDuration = 150f;
    public const float BananaOvertimeDuration = 30f;

    public const float ParkourStartX = -4f;
    public const float ParkourFinishX = 150f;
    public const float ParkourMinX = -10f;
    public const float ParkourMaxX = 158f;
    public const float ParkourAvalancheSpeed = 2.55f;
    public const float ParkourAvalancheWidth = 8f;
    public const float ParkourSpawnY = 3.4f;
}
