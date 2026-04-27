using FlowForge.Designer.Models;

namespace FlowForge.Designer.Services;

/// <summary>
/// Service for managing toast notifications.
/// </summary>
public class ToastService
{
    private readonly List<ToastModel> _toasts = [];

    /// <summary>Event raised when toasts change.</summary>
    public event Action? OnChange;

    /// <summary>Gets the current list of toasts.</summary>
    public IReadOnlyList<ToastModel> Toasts => _toasts;

    /// <summary>Shows a success toast.</summary>
    /// <param name="message">The message to display.</param>
    /// <param name="title">Optional title.</param>
    /// <param name="dismissTimeout">Auto-dismiss timeout in milliseconds.</param>
    public void ShowSuccess(string message, string? title = null, int dismissTimeout = 5000)
    {
        Show(ToastType.Success, message, title, dismissTimeout);
    }

    /// <summary>Shows an error toast.</summary>
    /// <param name="message">The message to display.</param>
    /// <param name="title">Optional title.</param>
    /// <param name="dismissTimeout">Auto-dismiss timeout in milliseconds. Default is longer for errors.</param>
    public void ShowError(string message, string? title = null, int dismissTimeout = 8000)
    {
        Show(ToastType.Error, message, title, dismissTimeout);
    }

    /// <summary>Shows a warning toast.</summary>
    /// <param name="message">The message to display.</param>
    /// <param name="title">Optional title.</param>
    /// <param name="dismissTimeout">Auto-dismiss timeout in milliseconds.</param>
    public void ShowWarning(string message, string? title = null, int dismissTimeout = 6000)
    {
        Show(ToastType.Warning, message, title, dismissTimeout);
    }

    /// <summary>Shows an info toast.</summary>
    /// <param name="message">The message to display.</param>
    /// <param name="title">Optional title.</param>
    /// <param name="dismissTimeout">Auto-dismiss timeout in milliseconds.</param>
    public void ShowInfo(string message, string? title = null, int dismissTimeout = 5000)
    {
        Show(ToastType.Info, message, title, dismissTimeout);
    }

    /// <summary>Shows a toast notification.</summary>
    /// <param name="type">The toast type.</param>
    /// <param name="message">The message to display.</param>
    /// <param name="title">Optional title.</param>
    /// <param name="dismissTimeout">Auto-dismiss timeout in milliseconds.</param>
    public void Show(ToastType type, string message, string? title = null, int dismissTimeout = 5000)
    {
        var toast = new ToastModel
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = type,
            Message = message,
            Title = title,
            DismissTimeout = dismissTimeout
        };

        _toasts.Add(toast);
        OnChange?.Invoke();
    }

    /// <summary>Removes a toast by ID.</summary>
    /// <param name="id">The toast ID to remove.</param>
    public void Remove(string id)
    {
        var toast = _toasts.FirstOrDefault(t => t.Id == id);
        if (toast is not null)
        {
            _toasts.Remove(toast);
            OnChange?.Invoke();
        }
    }

    /// <summary>Clears all toasts.</summary>
    public void Clear()
    {
        _toasts.Clear();
        OnChange?.Invoke();
    }
}
