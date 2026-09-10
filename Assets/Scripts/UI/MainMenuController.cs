using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pantalla inicial del juego. Pide un nombre de jugador y un nombre de
/// sala antes de conectar: si la sala no existe la crea, si ya existe se une
/// a ella (Shared Mode). El nombre elegido viaja en
/// <see cref="NetworkRunnerHandler.LocalNickname"/> y lo usa
/// <c>PlayerController</c> para el cartel con el puntaje de cada jugador.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    private InputField _nicknameInput;
    private InputField _sessionNameInput;
    private Text _statusText;
    private Button _connectButton;

    private void Awake()
    {
        UIFactory.EnsureCamera();
        UIFactory.EnsureEventSystem();
        BuildUI();
    }

    private void OnEnable()
    {
        var handler = NetworkRunnerHandler.Instance;
        if (handler != null)
        {
            handler.OnConnectionFailedEvent += HandleConnectionFailed;
        }
    }

    private void OnDisable()
    {
        var handler = NetworkRunnerHandler.Instance;
        if (handler != null)
        {
            handler.OnConnectionFailedEvent -= HandleConnectionFailed;
        }
    }

    private void BuildUI()
    {
        var canvas = UIFactory.CreateCanvas("MainMenuCanvas");

        var panel = UIFactory.CreatePanel(canvas.transform, "Panel", new Color(0.10f, 0.11f, 0.16f, 0.96f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-240, -210), new Vector2(240, 210));

        var title = UIFactory.CreateText(panel, "Banana Rush", 30, TextAnchor.MiddleCenter, Color.white);
        UIFactory.SetRect(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -65), new Vector2(-10, -10));

        var subtitle = UIFactory.CreateText(panel, "Elegi tu nombre y un nombre de sala para crear o unirte a una partida",
            14, TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.85f));
        UIFactory.SetRect(subtitle.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -105), new Vector2(-16, -68));

        _nicknameInput = UIFactory.CreateInputField(panel, "Tu nombre (ej: Tomi)");
        _nicknameInput.characterLimit = 14;
        UIFactory.SetRect(_nicknameInput.GetComponent<RectTransform>(),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-150, -155), new Vector2(150, -113));

        _sessionNameInput = UIFactory.CreateInputField(panel, "Nombre de sala (ej: Sala1)");
        UIFactory.SetRect(_sessionNameInput.GetComponent<RectTransform>(),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-150, -205), new Vector2(150, -163));

        _connectButton = UIFactory.CreateButton(panel, "Conectar", new Color(0.24f, 0.52f, 0.93f));
        UIFactory.SetRect(_connectButton.GetComponent<RectTransform>(),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-100, -260), new Vector2(100, -215));
        _connectButton.onClick.AddListener(HandleConnectClicked);

        _statusText = UIFactory.CreateText(panel, string.Empty, 14, TextAnchor.MiddleCenter, new Color(1f, 0.65f, 0.65f));
        UIFactory.SetRect(_statusText.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(12, 12), new Vector2(-12, 55));
    }

    private async void HandleConnectClicked()
    {
        var handler = NetworkRunnerHandler.Instance;
        if (handler == null)
        {
            _statusText.text = "Falta el objeto NetworkRunnerHandler en la escena.";
            return;
        }

        _connectButton.interactable = false;
        _statusText.color = new Color(0.8f, 0.8f, 0.85f);
        _statusText.text = "Conectando...";

        bool success = await handler.ConnectAsync(_sessionNameInput.text, _nicknameInput.text);

        // Si tuvo exito, Fusion carga la escena Game y este objeto se destruye solo.
        if (!success && _connectButton != null)
        {
            _connectButton.interactable = true;
        }
    }

    private void HandleConnectionFailed(string reason)
    {
        if (_statusText == null)
        {
            return;
        }

        _statusText.color = new Color(1f, 0.5f, 0.5f);
        _statusText.text = $"No se pudo conectar: {reason}";

        if (_connectButton != null)
        {
            _connectButton.interactable = true;
        }
    }
}
