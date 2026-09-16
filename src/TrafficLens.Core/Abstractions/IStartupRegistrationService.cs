namespace TrafficLens.Core.Abstractions;

public interface IStartupRegistrationService
{
    bool IsRegistered();

    void Enable(bool startMinimized);

    void Disable();
}
