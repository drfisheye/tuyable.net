using System.Threading;

namespace Drfisheye.Tuyable;

public interface ITuyableConnection
{
    Task<bool> Connect(CancellationToken token);

    Task<bool> TryConnect(int attempts, CancellationToken token);

    Task<bool> ExecuteCommandWithResponse(TuyableCommandCode code, byte[] message, Func<TuyableNotification, CancellationToken,Task<bool>> responseHandler, CancellationToken token);

    uint GetNextTuyaCommandNumber();
}
