namespace Drfisheye.Tuyable;

public class TuyableDevice
{
    public TuyableDeviceType DeviceType { get; private set; }

    public string Mac { get; private set; }

    public string Id { get; private set; }

    public string Key { get; private set; }

    public TuyableDevice(TuyableDeviceType deviceType, string mac, string id, string key)
    {
        DeviceType = deviceType;
        Mac = mac;
        Id = id;
        Key = key;
    }

    
}
