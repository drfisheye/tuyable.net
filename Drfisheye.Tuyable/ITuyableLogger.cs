using System.Threading;

namespace Drfisheye.Tuyable;

public interface ITuyableLogger
{
    void LogWarning(string message);
    void LogError(string message, Exception? exception = null);
    void LogInformation(string message);
    void LogVerbose(string message);
}
