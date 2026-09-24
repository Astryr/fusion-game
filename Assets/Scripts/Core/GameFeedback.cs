using System;
using UnityEngine;

/// <summary>
/// Canal local de feedback visual (toasts, popups). Lo disparan RPCs o
/// el gameplay y lo escucha el HUD.
/// </summary>
public static class GameFeedback
{
    public static event Action<string, Color> ToastRaised;
    public static event Action<Vector3, string, Color> WorldPopupRaised;

    public static void Toast(string message, Color color)
    {
        ToastRaised?.Invoke(message, color);
    }

    public static void WorldPopup(Vector3 worldPosition, string message, Color color)
    {
        WorldPopupRaised?.Invoke(worldPosition, message, color);
    }
}
