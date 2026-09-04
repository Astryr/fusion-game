using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// HUD minimo de la escena de juego: muestra el nombre de la sala y la
/// cantidad de jugadores conectados, y permite volver al menu principal.
/// </summary>
public class GameHUDController : MonoBehaviour
{
    private Text _infoText;

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
            handler.OnPlayerCountChangedEvent += HandlePlayerCountChanged;
            handler.OnDisconnectedEvent += HandleDisconnected;

            int currentCount = handler.Runner != null && handler.Runner.SessionInfo != null
                ? handler.Runner.SessionInfo.PlayerCount
                : 1;
            UpdateInfoText(currentCount);
        }
    }

    private void OnDisable()
    {
        var handler = NetworkRunnerHandler.Instance;
        if (handler != null)
        {
            handler.OnPlayerCountChangedEvent -= HandlePlayerCountChanged;
            handler.OnDisconnectedEvent -= HandleDisconnected;
        }
    }

    private void BuildUI()
    {
        var canvas = UIFactory.CreateCanvas("GameHUDCanvas");

        var topBar = UIFactory.CreatePanel(canvas.transform, "TopBar", new Color(0, 0, 0, 0.45f),
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -50), Vector2.zero);

        _infoText = UIFactory.CreateText(topBar, "Conectado", 18, TextAnchor.MiddleLeft, Color.white);
        UIFactory.SetRect(_infoText.rectTransform, Vector2.zero, Vector2.one, new Vector2(20, 0), new Vector2(-150, 0));

        var leaveButton = UIFactory.CreateButton(topBar, "Salir", new Color(0.8f, 0.25f, 0.25f));
        UIFactory.SetRect(leaveButton.GetComponent<RectTransform>(),
            new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-140, -18), new Vector2(-10, 18));
        leaveButton.onClick.AddListener(HandleLeaveClicked);
    }

    private void HandlePlayerCountChanged(int count)
    {
        UpdateInfoText(count);
    }

    private void UpdateInfoText(int count)
    {
        var handler = NetworkRunnerHandler.Instance;
        string sessionName = handler != null && !string.IsNullOrEmpty(handler.CurrentSessionName)
            ? handler.CurrentSessionName
            : "-";
        _infoText.text = $"Sala: {sessionName}    |    Jugadores conectados: {count}";
    }

    private void HandleLeaveClicked()
    {
        NetworkRunnerHandler.Instance?.Disconnect();
    }

    private void HandleDisconnected()
    {
        SceneManager.LoadScene(0);
    }
}
