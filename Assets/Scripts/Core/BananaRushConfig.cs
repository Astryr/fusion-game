/// <summary>
/// Numeros compartidos del diseño de nivel de Banana Rush. Viven en un solo
/// lugar para que el gameplay en runtime (<see cref="PlayerController"/>,
/// <see cref="BananaGameManager"/>) y la herramienta de setup del editor
/// (<c>BananaRushSetupTool</c>, en Assets/Editor) arment el mismo mapa.
///
/// El mapa es "cerrado": el ancho del piso (<see cref="GroundHalfWidth"/>)
/// es mayor que lo que llega a mostrar la camara en cualquier resolucion
/// razonable, asi que nunca se ve un hueco a los costados (no hay "espacio
/// para caerse" visualmente, mas alla de que el clamp ya lo impide).
/// </summary>
public static class BananaRushConfig
{
    /// <summary>Altura Y de la superficie del piso (donde pisan los jugadores).</summary>
    public const float GroundTopY = 0f;

    /// <summary>Grosor visual/de colision del piso hacia abajo de <see cref="GroundTopY"/>.</summary>
    public const float GroundThickness = 2f;

    /// <summary>Medio ancho del piso: el nivel va de -GroundHalfWidth a +GroundHalfWidth.</summary>
    public const float GroundHalfWidth = 15f;

    /// <summary>Margen para que los jugadores no queden colgados del borde del piso.</summary>
    public const float PlayerClampMargin = 1f;

    /// <summary>Limite horizontal de movimiento de los jugadores.</summary>
    public const float PlayerClampX = GroundHalfWidth - PlayerClampMargin;

    /// <summary>Altura Y desde donde caen las bananas.</summary>
    public const float BananaSpawnY = 8.5f;

    /// <summary>Margen horizontal para que las bananas no aparezcan pegadas al borde.</summary>
    public const float BananaSpawnMargin = 1.5f;

    /// <summary>Limite horizontal de aparicion de bananas.</summary>
    public const float BananaSpawnX = GroundHalfWidth - BananaSpawnMargin;

    /// <summary>
    /// Tamaño ortografico de la camara principal. Mas chico que antes a
    /// proposito, para ver mejor a los personajes/bananas ("mas cerca").
    /// </summary>
    public const float CameraOrthographicSize = 7.5f;

    /// <summary>Centro vertical de la camara.</summary>
    public const float CameraCenterY = 3.5f;
}
