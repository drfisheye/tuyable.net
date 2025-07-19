using System;

namespace Drfisheye.Tuyable;

public class TuyableNotification
{
    public TuyableCommandCode Code { get; private set; }

    public byte[] Message { get; private set; }

    public object? Context { get; private set; }

    public TuyableNotification(TuyableCommandCode code, byte[] message, object? context)
    {
        Code = code;
        Message = message;
        Context = context;
    }
}
