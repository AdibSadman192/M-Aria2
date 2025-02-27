namespace MAria2.Core.Interfaces;

public interface IConfigurationService
{
    EnginePreferences GetEnginePreferences();
    void SaveEnginePreferences(EnginePreferences preferences);
}
