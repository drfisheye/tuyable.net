namespace Drfisheye.Tuyable;

using static Drfisheye.Tuyable.TuyableUtil;
public class SmartDotController
{
    public TuyableDevice Device { get; private set; }

    public ITuyableConnection Connection { get; private set; }

    public SmartDotController(ITuyableConnection connection, TuyableDevice device)
    {
        Device = device;
        Connection = connection;
    }

    public Task<bool> On(CancellationToken cancellationToken)
    {
        var message = CreateDeviceOnOffMessage(true);
        return Connection.ExecuteCommandWithResponse(TuyableCommandCode.FUN_SENDER_DPS_V4, message, TuyableUtil.TuyableCommandResponseHandler, cancellationToken);
    }

    public Task<bool> Off(CancellationToken cancellationToken)
    {
        var message = CreateDeviceOnOffMessage(false);
        return Connection.ExecuteCommandWithResponse(TuyableCommandCode.FUN_SENDER_DPS_V4, message, TuyableUtil.TuyableCommandResponseHandler, cancellationToken);
    }

    public Task<bool> Play(SmartDotProgram program, CancellationToken cancellationToken)
    {
        var message = CreateDevicePlayMessage(program);
        return Connection.ExecuteCommandWithResponse(TuyableCommandCode.FUN_SENDER_DPS_V4, message, TuyableUtil.TuyableCommandResponseHandler, cancellationToken);
    }

    private byte[] CreateDeviceOnOffMessage(bool putOn)
    {
        var bytes = new byte[10];
        WriteUint32((Span<byte>)bytes, 1, Connection.GetNextTuyaCommandNumber());
        bytes[5] = 0x69;
        bytes[6] = 0x01;
        bytes[7] = 0x00;
        bytes[8] = 0x01;
        bytes[9] = (byte)(putOn ? 0x01 : 0x00);
        return bytes;
    }

    private byte[] CreateDevicePlayMessage(SmartDotProgram program)
    {
        var bytes = new byte[13];
        WriteUint32((Span<byte>)bytes, 1, Connection.GetNextTuyaCommandNumber());
        bytes[5] = 0x68;
        bytes[6] = 0x02;
        bytes[7] = 0x00;
        bytes[8] = 0x04;
        WriteUint32((Span<byte>)bytes, 9, (uint)program);
        return bytes;
    }
}
