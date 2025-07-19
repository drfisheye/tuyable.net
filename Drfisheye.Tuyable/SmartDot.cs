namespace Drfisheye.Tuyable;

public class SmartDot
{
    public TuyableDevice Device { get; private set; }

    public SmartDot(string id, string mac, string key)
    {
        Device = new TuyableDevice(TuyableDeviceType.SmartDot, id, mac, key);
    }

    public SmartDotController GetController(ITuyableConnection connection)
    {
        return new SmartDotController(connection, Device);
    }
}
