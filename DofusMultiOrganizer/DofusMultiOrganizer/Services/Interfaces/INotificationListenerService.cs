namespace DofusOrganizer.Services.Interfaces;

public interface INotificationListenerService : IDisposable
{
    /// <summary>
    /// Déclenché quand une notification Windows est reçue.
    /// Le paramètre string contient le texte complet de la notification (titre + corps).
    /// </summary>
    event EventHandler<string>? NotificationTextReceived;

    /// <summary>Demande la permission d'accès aux notifications (affiche un prompt Windows).</summary>
    Task<bool> RequestAccessAsync();

    /// <summary>Démarre l'écoute des notifications.</summary>
    void Start();

    /// <summary>Arrête l'écoute des notifications.</summary>
    void Stop();
}
