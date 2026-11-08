using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

public enum CloudStatus
{
    Idle,
    Connecting,
    Online,
    Offline,
}

// Ningun flujo del juego espera a la nube: si algo falla queda en Offline y se reintenta en el proximo uso.
public static class CloudServices
{
    public static CloudStatus Status { get; private set; } = CloudStatus.Idle;
    public static string Nickname { get; private set; } = CloudConfig.DefaultNickname;
    public static string Profile { get; private set; } = "default";
    public static string FailureReason { get; private set; } = string.Empty;

    // Perfil con el que esta iniciada la sesion en este momento (puede ir un paso detras de Profile).
    public static string SignedProfile => _signedProfile;

    // Corta toda llamada de red (pruebas automaticas y demos sin internet).
    public static bool ForceOffline { get; set; }

    public static event Action<CloudStatus> StatusChanged;

    private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
    private static string _signedProfile = string.Empty;

    public static string StatusLabel
    {
        get
        {
            switch (Status)
            {
                case CloudStatus.Online: return "Ranking online: conectado";
                case CloudStatus.Connecting: return "Ranking online: conectando...";
                case CloudStatus.Offline: return "Ranking online: sin conexión";
                default: return "Ranking online: se conecta al jugar";
            }
        }
    }

    // Cada apodo tiene su propio perfil de Authentication: asi dos instancias en la misma PC son jugadores distintos.
    public static void UseIdentity(string nickname)
    {
        Nickname = string.IsNullOrWhiteSpace(nickname) ? CloudConfig.DefaultNickname : nickname.Trim();
        string profile = ProfileFor(Nickname);
        if (profile == Profile)
        {
            return;
        }

        Profile = profile;
        if (Status == CloudStatus.Online)
        {
            SetStatus(CloudStatus.Idle, string.Empty);
        }
    }

    public static void InvalidateSession()
    {
        _signedProfile = string.Empty;
        if (Status == CloudStatus.Online)
        {
            SetStatus(CloudStatus.Idle, string.Empty);
        }
    }

    // Inicializa los servicios sin iniciar sesion: alcanza para Remote Config y no crea ninguna identidad.
    public static async Task InitializeAsync()
    {
        if (ForceOffline)
        {
            throw new InvalidOperationException("Modo sin conexión forzado");
        }

        if (UnityServices.State == ServicesInitializationState.Initialized)
        {
            return;
        }

        await Gate.WaitAsync();
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await WithTimeout(UnityServices.InitializeAsync(), CloudConfig.InitTimeoutSeconds);
            }
        }
        finally
        {
            Gate.Release();
        }
    }

    // Inicia sesion con la identidad pedida al llamar: un UseIdentity posterior no puede cambiar a quien se loguea ni a quien se renombra.
    public static async Task<bool> EnsureOnlineAsync()
    {
        if (ForceOffline)
        {
            SetStatus(CloudStatus.Offline, "Modo sin conexión forzado");
            return false;
        }

        string nickname = Nickname;
        string wanted = Profile;

        await Gate.WaitAsync();
        try
        {
            if (_signedProfile == wanted && IsSignedIn())
            {
                SetStatus(wanted == Profile ? CloudStatus.Online : CloudStatus.Connecting, string.Empty);
                return true;
            }

            SetStatus(CloudStatus.Connecting, string.Empty);
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await WithTimeout(UnityServices.InitializeAsync(), CloudConfig.InitTimeoutSeconds);
            }

            IAuthenticationService auth = AuthenticationService.Instance;
            if (!auth.IsSignedIn || _signedProfile != wanted)
            {
                if (auth.IsSignedIn)
                {
                    auth.SignOut();
                }

                if (auth.Profile != wanted)
                {
                    auth.SwitchProfile(wanted);
                }

                await WithTimeout(auth.SignInAnonymouslyAsync(), CloudConfig.RequestTimeoutSeconds);
                _signedProfile = wanted;
                await SyncPlayerNameAsync(nickname);
            }

            SetStatus(wanted == Profile ? CloudStatus.Online : CloudStatus.Connecting, string.Empty);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CloudServices] {ex.GetType().Name}: {ex.Message}");
            SetStatus(CloudStatus.Offline, Describe(ex));
            return false;
        }
        finally
        {
            Gate.Release();
        }
    }

    public static string Describe(Exception ex)
    {
        switch (ex)
        {
            case TimeoutException _:
                return "Tiempo de espera agotado";
            case ServicesInitializationException _:
                return "No se pudo iniciar Unity Gaming Services (¿proyecto sin vincular o sin internet?)";
            case AuthenticationException _:
                return "No se pudo iniciar sesión";
            case RequestFailedException _:
                return "Sin conexión con Unity Gaming Services";
            default:
                return ex.Message;
        }
    }

    public static async Task<T> WithTimeout<T>(Task<T> task, float seconds)
    {
        Task finished = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(seconds)));
        if (finished != task)
        {
            Observe(task);
            throw new TimeoutException();
        }

        return await task;
    }

    public static async Task WithTimeout(Task task, float seconds)
    {
        Task finished = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(seconds)));
        if (finished != task)
        {
            Observe(task);
            throw new TimeoutException();
        }

        await task;
    }

    public static string ProfileFor(string nickname)
    {
        var builder = new StringBuilder();
        foreach (char c in nickname)
        {
            if (c < 128 && char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }

            if (builder.Length >= 24)
            {
                break;
            }
        }

        return builder.Length > 0 ? builder.ToString() : "default";
    }

    // Authentication no admite espacios y le agrega un sufijo "#1234" propio.
    public static string PlayerNameFor(string nickname)
    {
        var builder = new StringBuilder();
        foreach (char c in nickname.Trim())
        {
            if (char.IsWhiteSpace(c))
            {
                builder.Append('_');
            }
            else if (!char.IsControl(c) && c != '#')
            {
                builder.Append(c);
            }

            if (builder.Length >= 40)
            {
                break;
            }
        }

        return builder.Length > 0 ? builder.ToString() : CloudConfig.DefaultNickname;
    }

    private static bool IsSignedIn()
    {
        return UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn;
    }

    // Solo cosmetico: si falla, el ranking muestra el nombre que genera el servicio.
    private static async Task SyncPlayerNameAsync(string nickname)
    {
        try
        {
            string wanted = PlayerNameFor(nickname);
            IAuthenticationService auth = AuthenticationService.Instance;
            string current = await WithTimeout(auth.GetPlayerNameAsync(), CloudConfig.RequestTimeoutSeconds);
            int hash = current != null ? current.LastIndexOf('#') : -1;
            string currentBase = hash > 0 ? current.Substring(0, hash) : current;
            if (!string.Equals(currentBase, wanted, StringComparison.Ordinal))
            {
                await WithTimeout(auth.UpdatePlayerNameAsync(wanted), CloudConfig.RequestTimeoutSeconds);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CloudServices] No se pudo fijar el nombre del jugador: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void Observe(Task task)
    {
        task.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
    }

    private static void SetStatus(CloudStatus status, string reason)
    {
        FailureReason = status == CloudStatus.Offline ? reason : string.Empty;
        if (Status == status)
        {
            return;
        }

        Status = status;
        StatusChanged?.Invoke(status);
    }
}
