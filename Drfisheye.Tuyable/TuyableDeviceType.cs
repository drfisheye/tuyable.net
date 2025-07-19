namespace Drfisheye.Tuyable;

public class TuyableDeviceType
{
    public string Uuid { get; private set; }

    public TuyableDeviceType(string uuid)
    {
        Uuid = uuid;
    }

    public static TuyableDeviceType SmartDot = new TuyableDeviceType("2eb53af86bf980eb");
    
}
