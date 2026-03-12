using DofusOrganizer.Services.Interfaces;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace DofusOrganizer.Services;

public sealed class NotificationListenerService : INotificationListenerService
{
    public event EventHandler<string>? NotificationTextReceived;

    private bool _isRunning;

    public async Task<bool> RequestAccessAsync()
    {
        var status = await UserNotificationListener.Current.RequestAccessAsync();
        return status == UserNotificationListenerAccessStatus.Allowed;
    }

    public void Start()
    {
        if (_isRunning) return;
        UserNotificationListener.Current.NotificationChanged += OnNotificationChanged;
        _isRunning = true;
    }

    public void Stop()
    {
        if (!_isRunning) return;
        UserNotificationListener.Current.NotificationChanged -= OnNotificationChanged;
        _isRunning = false;
    }

    private void OnNotificationChanged(UserNotificationListener sender, UserNotificationChangedEventArgs args)
    {
        // On ne s'intéresse qu'aux nouvelles notifications
        if (args.ChangeKind != UserNotificationChangedKind.Added) return;

        var notification = sender.GetNotification(args.UserNotificationId);
        if (notification == null) return;

        var text = ExtractText(notification);

        if (!string.IsNullOrWhiteSpace(text))
            NotificationTextReceived?.Invoke(this, text);
    }

    /// <summary>
    /// Extrait uniquement le titre (premier élément de texte) de la notification toast.
    /// Pour Dofus Rétro, le titre est toujours "[CharacterName] - Dofus Retro v..."
    /// — c'est le personnage DESTINATAIRE de la notification, celui à focus.
    /// </summary>
    private static string ExtractText(UserNotification notification)
    {
        try
        {
            var binding = notification.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric);
            if (binding == null) return string.Empty;

            // On ne retourne que le premier élément (titre) qui contient le nom du perso destinataire.
            // Le corps (invite de groupe, échange, tour...) n'est pas utilisé pour le matching.
            var title = binding.GetTextElements().FirstOrDefault()?.Text ?? string.Empty;
            return title;
        }
        catch
        {
            return string.Empty;
        }
    }

    public void Dispose() => Stop();
}
