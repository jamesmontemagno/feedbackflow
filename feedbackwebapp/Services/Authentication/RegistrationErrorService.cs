namespace FeedbackWebApp.Services.Authentication;

/// <summary>
/// Service to manage registration error state across components
/// </summary>
public interface IRegistrationErrorService
{
    /// <summary>
    /// Event triggered when a registration error occurs
    /// </summary>
    event EventHandler<string>? RegistrationErrorOccurred;
    
    /// <summary>
    /// Trigger a registration error
    /// </summary>
    /// <param name="errorMessage">The error message to display</param>
    void TriggerRegistrationError(string errorMessage);
}

/// <summary>
/// Implementation of registration error service
/// </summary>
public class RegistrationErrorService : IRegistrationErrorService
{
    public event EventHandler<string>? RegistrationErrorOccurred;
    
    public void TriggerRegistrationError(string errorMessage)
    {
        RegistrationErrorOccurred?.Invoke(this, errorMessage);
    }
}
