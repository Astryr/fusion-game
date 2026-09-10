/// <summary>
/// Numeros compartidos del diseño de nivel de Banana Rush. Viven en un solo
/// lugar para que el gameplay en runtime (<see cref="PlayerController"/>,
/// <see cref="BananaGameManager"/>) y la herramienta de setup del editor
/// (<c>BananaRushSetupTool</c>, en Assets/Editor) arment el mismo mapa.
/// </summary>
public static class BananaRushConfig
{
    /// <summary>Altura Y de la superficie del piso (donde pisan los jugadores).</summary>
    public const float GroundTopY = 0f;

    /// <summary>Grosor visual/de colision del piso hacia abajo de <see cref="GroundTopY"/>.</summary>
    public const float GroundThickness = 3f;

    /// <summary>Medio ancho del piso: el nivel va de -GroundHalfWidth a +GroundHalfWidth.</summary>
    public const float GroundHalfWidth = 20f;

    /// <summary>Margen para que los jugadores no queden colgados del borde del piso.</summary>
    public const float PlayerClampMargin = 1f;

    /// <summary>Limite horizontal de movimiento de los jugadores.</summary>
    public const float PlayerClampX = GroundHalfWidth - PlayerClampMargin;

    /// <summary>Altura Y desde donde caen las bananas.</summary>
    public const float BananaSpawnY = 11f;

    /// <summary>Margen horizontal para que las bananas no aparezcan pegadas al borde.</summary>
    public const float BananaSpawnMargin = 2f;

    /// <summary>Limite horizontal de aparicion de bananas.</summary>
    public const float BananaSpawnX = GroundHalfWidth - BananaSpawnMargin;

    /// <summary>Tamaño ortografico de la camara principal (ve todo el mapa a la vez).</summary>
    public const float CameraOrthographicSize = 13f;

    /// <summary>Centro vertical de la camara.</summary>
    public const float CameraCenterY = 5.5f;
}
