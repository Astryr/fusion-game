/// <summary>
/// Numeros compartidos del diseño de nivel de Banana Rush. Viven en un solo
/// lugar para que el gameplay en runtime (<see cref="PlayerController"/>,
/// <see cref="BananaGameManager"/>) y la herramienta de setup del editor
/// (<c>BananaRushSetupTool</c>, en Assets/Editor) arment el mismo mapa.
///
/// El mapa es "cerrado": el ancho visual del piso (<see cref="GroundHalfWidth"/>)
/// es mayor que <see cref="ScreenHalfWidth"/> (lo que llega a mostrar la
/// camara), asi que nunca se ve un hueco a los costados; y el piso se
/// extiende bastante hacia abajo (<see cref="GroundThickness"/>) para que
/// tampoco se vea vacio por debajo. El limite de movimiento de los
/// jugadores (<see cref="PlayerClampX"/>) esta atado a lo que la camara
/// llega a mostrar, no al ancho del piso: asi nunca se van de pantalla.
/// </summary>
public static class BananaRushConfig
{
    /// <summary>Altura Y de la superficie del piso (donde pisan los jugadores).</summary>
    public const float GroundTopY = 0f;

    /// <summary>
    /// Grosor visual/de colision del piso hacia abajo de <see cref="GroundTopY"/>.
    /// Bastante mas que lo que la camara llega a mostrar hacia abajo, para
    /// que nunca se vea el vacio por debajo del piso.
    /// </summary>
    public const float GroundThickness = 8f;

    /// <summary>Medio ancho visual/de colision del piso: el nivel va de -GroundHalfWidth a +GroundHalfWidth.</summary>
    public const float GroundHalfWidth = 15f;

    /// <summary>Altura Y desde donde caen las bananas.</summary>
    public const float BananaSpawnY = 8.5f;

    /// <summary>
    /// Tamaño ortografico de la camara principal. Mas chico que antes a
    /// proposito, para ver mejor a los personajes/bananas ("mas cerca").
    /// </summary>
    public const float CameraOrthographicSize = 7.5f;

    /// <summary>Centro vertical de la camara.</summary>
    public const float CameraCenterY = 3.5f;

    /// <summary>
    /// Aspecto de referencia (16:9) para calcular cuanto ancho llega a ver
    /// la camara. En pantallas mas anchas se ve un poco mas de piso a los
    /// costados (sin problema); en pantallas mas angostas nunca deberia
    /// faltar (16:9 es el piso comun en proyectores/monitores).
    /// </summary>
    public const float ReferenceAspect = 16f / 9f;

    /// <summary>Medio ancho visible por la camara en el aspecto de referencia.</summary>
    public const float ScreenHalfWidth = CameraOrthographicSize * ReferenceAspect;

    /// <summary>Medio ancho aproximado del sprite del jugador (para que no se corte contra el borde).</summary>
    public const float PlayerHalfWidth = 0.5f;

    /// <summary>Limite horizontal de movimiento de los jugadores: el borde de la pantalla, no el del piso.</summary>
    public const float PlayerClampX = ScreenHalfWidth - PlayerHalfWidth;

    /// <summary>Margen horizontal para que las bananas no aparezcan pegadas al borde de la pantalla.</summary>
    public const float BananaSpawnMargin = 0.35f;

    /// <summary>Limite horizontal de aparicion de bananas (tambien atado a la pantalla, no al piso).</summary>
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
    public const float ParkourSpawnY = 1.6f;
}
