using System.Security.Cryptography;
using System.Text;
using InTheHand.Bluetooth;
using static Drfisheye.Tuyable.TuyableUtil;

namespace Drfisheye.Tuyable;

public class BluetoothTuyableConnection : ITuyableConnection, IDisposable
{
    private BluetoothDevice? _btDevice;
    private byte[]? _localKey = null;
    private byte[]? _loginKey = null;
    private byte[]? _sessionKey = null;
    private byte[]? _authKey = null;
    private GattCharacteristic? _characteristic;
    private readonly Object _syncObject = new Object();
    private Dictionary<uint, TuyableTask> _runningTasks = new Dictionary<uint, TuyableTask>();
    private uint _tuyaCommandNum = 0;
    private uint _seqNum = 1;
    private uint _protocolVersion = 2;

    private bool _isDisposed = false;

    public ITuyableLogger Logger { get; private set; }
    public TuyableDevice Device { get; private set; }


    public BluetoothTuyableConnection(ITuyableLogger Logger, TuyableDevice device)
    {
        this.Logger = Logger;
        this.Device = device;
        _localKey = Encoding.UTF8.GetBytes(Device.Key.Substring(0, 6));
        _loginKey = MD5.HashData(_localKey);
    }

    public uint GetNextTuyaCommandNumber()
    {
        lock (_syncObject)
        {
            return ++_tuyaCommandNum;
        }
    }

    public async Task<bool> Connect(CancellationToken cancelationToken)
    {
        CheckDisposed();
        if (_btDevice != null && _btDevice.Gatt.IsConnected && _sessionKey != null)
        {
            return true;
        }

        Logger.LogInformation("Connect");

        if (_btDevice != null && !_btDevice.Gatt.IsConnected)
        {
            _btDevice = null;
        }

        if (_btDevice != null && _btDevice.Gatt.IsConnected)
        {
            _btDevice.Gatt.Disconnect();
            _btDevice = null;
        }

        for (int i = 0; i < 3 && _btDevice == null; i++)
        {
            if (i > 0)
            {
                await Bluetooth.RequestLEScanAsync(new BluetoothLEScanOptions() { AcceptAllAdvertisements = true });
            }

            _btDevice = await BluetoothDevice.FromIdAsync("DC23504648CC");
        }

        if (_btDevice == null)
        {
            Logger.LogError("No device found");
            return false;
        }

        GattCharacteristic? notifyCharacteristic = null;
        await _btDevice.Gatt.ConnectAsync();

        if (!_btDevice.Gatt.IsConnected)
        {
            return false;
        }

        var service = await _btDevice.Gatt.GetPrimaryServiceAsync(BluetoothUuid.FromShortId((ushort)0xfd50));
        if (service == null)
        {
            return false;
        }
        notifyCharacteristic = await service.GetCharacteristicAsync(BluetoothUuid.FromGuid(new Guid("0000000200001001800100805f9b07d0")));

        notifyCharacteristic.CharacteristicValueChanged += async (Object? source, GattCharacteristicValueChangedEventArgs e) =>
        {
            if (e.Value == null)
            {
                return;
            }
            Logger.LogVerbose("Notification: " + Convert.ToHexString(e.Value).Replace("-", ""));
            var result = await ProcessNotification(e.Value);
            lock (_syncObject)
            {
                if (result.responseTo > 0 &&_runningTasks.TryGetValue(result.responseTo, out var task))
                {
                    _runningTasks.Remove(result.responseTo);
                    task.SetResult(!task.CancellationToken.IsCancellationRequested
                        && !cancelationToken.IsCancellationRequested
                        && result.result);
                }
            }
        };
        await notifyCharacteristic.StartNotificationsAsync();
        _characteristic = await service.GetCharacteristicAsync(BluetoothUuid.FromGuid(new Guid("0000000100001001800100805f9b07d0")));

        _sessionKey = null;
        _seqNum = 1;
        _tuyaCommandNum = 1;
        _runningTasks = new Dictionary<uint, TuyableTask>();

        await Task.Delay(250);

        for (int i = 0; i < 3; i++)
        {
            Logger.LogInformation("Sending device info");
            CancellationTokenSource cancelationSource = new CancellationTokenSource(1000);
            TuyableTask TuyableTask = CreateTuyableTask(ConnectionStuffResponseHandler, cancelationSource.Token, null);
            var deviceInfoSendValue = BuildPacket(new byte[] { 0, 243 }, TuyableTask.SeqNum, 0, TuyableCommandCode.FUN_SENDER_DEVICE_INFO, useLocalKey: true);
            var tryAwait = true;
            try
            {
                await WriteToDevice(deviceInfoSendValue);
            }
            catch (ObjectDisposedException)
            {
                tryAwait = false;
                if (!_btDevice.Gatt.IsConnected)
                {
                    return false;
                }
            }
            if (tryAwait)
            {
                await TuyableTask.TaskCompletionSource.Task;
            }
            if (cancelationSource.IsCancellationRequested)
            {
                return false;
            }
            if (_sessionKey != null)
            {
                break;
            }
            await Task.Delay(250);
        }
        if (_sessionKey == null)
        {
            return false;
        }
        Logger.LogInformation("Session key received. Protocol version: " + _protocolVersion);

        Logger.LogInformation("Pairing");
        if (!await SendTuya(TuyableCommandCode.FUN_SENDER_PAIR, CreatePairingMessage()))
        {
            return false;
        }
        if (cancelationToken.IsCancellationRequested)
        {
            return false;
        }
        Logger.LogInformation("Is paired");
        return true;
    }

    private async Task WriteToDevice(byte[] data)
    {
        if (_characteristic == null)
        {
            Logger.LogError("Characteristic not initialized");
            return;
        }
        Logger.LogVerbose("Write to device: " + Convert.ToHexString(data).Replace("-", ""));
        await _characteristic.WriteValueWithoutResponseAsync(data);
    }


    public async Task<bool> TryConnect(int attempts, CancellationToken token)
    {
        for (int i = 0; i < attempts; i++)
        {
            if (await Connect(token))
            {
                return true;
            }
        }
        return false;
    }

    public async Task<bool> ExecuteCommandWithResponse(TuyableCommandCode code, byte[] message, Func<TuyableNotification, CancellationToken, Task<bool>> responseHandler, CancellationToken cancellationToken)
    {
        CheckDisposed();
        if (_characteristic == null)
        {
            Logger.LogError("Characteristic not initialized");
            return false;
        }
        var deviceTuyaCommandNum = _tuyaCommandNum++;
        var context = new TuyaCommandContext(deviceTuyaCommandNum);
        var task = CreateTuyableTask(responseHandler, cancellationToken, context);
        var packet = BuildPacket(message, task.SeqNum, 0, code, useLocalKey: false);
        await WriteToDevice(packet);
        var result = await task.TaskCompletionSource.Task;
        return result;
    }

    private TuyableTask CreateTuyableTask(Func<TuyableNotification, CancellationToken, Task<bool>> responseHandler, CancellationToken cancelationToken, object? context = null)
    {
        lock (_syncObject)
        {
            uint newSeqNum = _seqNum++;
            var task = new TuyableTask(newSeqNum, cancelationToken, responseHandler, context);
            _runningTasks.Add(newSeqNum, task);
            return task;
        }
    }

    private async Task<bool> SendTuya(TuyableCommandCode code, byte[] msg)
    {
        if (_characteristic == null)
        {
            Logger.LogError("Characteristic not initialized");
            return false;
        }
        var task = CreateTuyableTask(ConnectionStuffResponseHandler, new CancellationTokenSource(1000 * 5).Token, null);
        var packetValue = BuildPacket(msg, task.SeqNum, 0, code, useLocalKey: false);
        await WriteToDevice(packetValue);
        return await task.TaskCompletionSource.Task;
    }
    
    private byte[] CreatePairingMessage()
    {
        if (_localKey == null)
        {
            Logger.LogError("Local key not initialized");
            return Array.Empty<byte>();
        }
        List<byte> bytes = new List<byte>();
        bytes.AddRange(Encoding.UTF8.GetBytes(Device.DeviceType.Uuid));
        bytes.AddRange(_localKey);
        bytes.AddRange(Encoding.UTF8.GetBytes(Device.Id));
        while (bytes.Count() < 44)
        {
            bytes.Add(0);
        }
        return bytes.ToArray();
    }

    private byte[] BuildPacket(byte[] msg, uint seqNum, uint responseTo, TuyableCommandCode code, bool useLocalKey)
    {
        return BuildPacketInternal(null, msg, seqNum, responseTo, code, useLocalKey);
    }

    private byte[] BuildPacketInternal(byte[]? iv, byte[] msg, uint seqNum, uint responseTo, TuyableCommandCode code, bool useLocalKey)
    {
        List<byte> data = new List<byte>();
        data.Add(useLocalKey ? (byte)0x04 : (byte)0x05);
        var key = useLocalKey ? _loginKey : _sessionKey;
        if (key == null)
        {
            Logger.LogError("Key not initialized");
            return Array.Empty<byte>();
        }
        if (!useLocalKey && _sessionKey == null)
        {
            throw new Exception("session key not set yet");
        }
        if (iv == null)
        {
            iv = new byte[16];
            RandomNumberGenerator.Create().GetBytes(iv, 0, 16);
        }
        data.AddRange(iv);

        var toEncrypt = new List<byte>();
        WriteUint32(toEncrypt, seqNum);
        WriteUint32(toEncrypt, responseTo);
        WriteUint16(toEncrypt, (UInt16)code);
        WriteUint16(toEncrypt, (UInt16)msg.Length);
        toEncrypt.AddRange(msg);
        var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        var calcCrc = CalculateCrc16(toEncrypt.ToArray());
        WriteUint16(toEncrypt, calcCrc);
        var encrypted = aes.EncryptCbc(toEncrypt.ToArray(), iv, PaddingMode.Zeros);
        data.AddRange(encrypted);

        List<byte> bytes = new List<byte>();
        int packNum = 0;
        PackInt(bytes, packNum);
        PackInt(bytes, data.Count());
        bytes.Add((byte)(_protocolVersion << 4));
        bytes.AddRange(data);
        return bytes.ToArray();
    }

    private async Task<(bool result, uint seqNum, uint responseTo)> ProcessNotification(byte[] value)
    {
        var notification = ParseNotificationInternal(value);
        if (!notification.succes)
        {
            return (false, 0, 0);
        }
        if (_localKey == null)
        {
            Logger.LogError("Local key not initialized");
            return (false, 0, 0);
        }
        var result = true;
        var msg = notification.msg;
        TuyableTask? task = null;
        lock (_syncObject)
        {
            if (notification.responseTo > 0)
            {
                _runningTasks.TryGetValue(notification.responseTo, out task);
            }
        }
        if (task != null)
        {
            result = await task.ResponseHandler(new TuyableNotification(notification.code, msg, task.Context), task.CancellationToken);
        }
        return (result, notification.seqNum, notification.responseTo);
    }

    private Task<bool> ConnectionStuffResponseHandler(TuyableNotification notification, CancellationToken token)
    {
        var result = false;
        var msg = notification.Message;
        if (notification.Code == TuyableCommandCode.FUN_SENDER_DEVICE_INFO)
        {
            if (_localKey == null)
            {
                Logger.LogError("Local key not initialized");
                return Task.FromResult(false);
            }
            _protocolVersion = msg[2];
            var srand = msg.Skip(6).Take(6);
            _sessionKey = MD5.HashData(_localKey.Concat(srand).ToArray());
            _authKey = msg.Skip(14).Take(46 - 14).ToArray();
        }
        else if (notification.Code == TuyableCommandCode.FUN_SENDER_DPS_V4)
        {
            (uint tuyaCommandSeqNem, _) = ReadUint32(msg, 1);
            result = tuyaCommandSeqNem == (notification.Context as TuyaCommandContext)?.TuyaCommandNum;
        }
        else if (notification.Code == TuyableCommandCode.FUN_SENDER_PAIR)
        {
            if (msg.Length >= 1)
            {
                result = msg[0] == 0 || msg[0] == 2;
            }
        }
        return Task.FromResult(result);
    }

    internal (bool succes, byte[] iv, byte[] msg, UInt16 seqNum, UInt16 responseTo, TuyableCommandCode code) ParseNotificationInternal(byte[] bytes)
    {
        var (packNum, offset) = UnpackInt(bytes, 0);
        int expectedLength = 0;
        (expectedLength, offset) = UnpackInt(bytes, offset);
        var protocol = bytes[offset];
        var data = bytes.Skip(offset + 1).ToArray();
        if (data.Length > expectedLength)
        {
            throw new Exception("not so much data expected");
        }
        if (data.Length != expectedLength)
        {
            throw new Exception("wrong length");
        }
        offset = 0;
        (var securityFlag, offset) = ReadByte(data, offset);
        byte[]? key = null;
        if (securityFlag == 4)
        {
            key = _loginKey;
        }
        else if (securityFlag == 5)
        {
            key = _sessionKey;
        }
        else
        {
            throw new Exception($"securityflag {securityFlag} not supported yet");
        }
        if (key == null)
        {
            throw new Exception("Encryption key is not initialized.");
        }
        var iv = data.Skip(1).Take(16).ToArray();
        var encrypted = data.Skip(17).ToArray();
        var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        var raw = aes.DecryptCbc(encrypted, iv, PaddingMode.Zeros);

        offset = 0;
        (var seqNum, offset) = ReadUint32(raw, offset);
        (var responseTo, offset) = ReadUint32(raw, offset);
        (var code, offset) = ReadUint16(raw, offset);
        (var len, offset) = ReadUint16(raw, offset);
        var dataEndPos = len + 12;

        if (raw.Length < dataEndPos)
        {
            throw new Exception("TuyaBLEDataLengthError");
        }
        if (raw.Length > dataEndPos)
        {
            (var crc, _) = ReadUint16(raw, dataEndPos);
            var calcCrc = CalculateCrc16(raw.Take(dataEndPos).ToArray());
            if (crc != calcCrc)
            {
                throw new Exception("CRC is invalid");
            }
        }

        if (Enum.IsDefined(typeof(TuyableCommandCode), (int)code))
        {
            var bleCode = (TuyableCommandCode)(int)code;
            var msg = raw.Skip(12).Take(dataEndPos - 12).ToArray();
            return (true, iv, msg, (ushort)seqNum, (ushort)responseTo, bleCode);
        }

        return (false, iv, new byte[0], 0, 0, TuyableCommandCode.FUN_SENDER_DEVICE_INFO);
    }

    public static ushort CalculateCrc16(byte[] data)
    {
        ushort crc = 0xFFFF;
        foreach (byte b in data)
        {
            crc ^= (ushort)(b & 0xFF); // Ensure byte is treated as unsigned short for XOR
            for (int i = 0; i < 8; i++)
            {
                ushort tmp = (ushort)(crc & 1);
                crc >>= 1;
                if (tmp != 0)
                {
                    crc ^= 0xA001;
                }
            }
        }
        return crc;
    }

    public void Dispose()
    {
        if (_btDevice != null)
        {
            _btDevice.Gatt.Disconnect();
            _btDevice = null;
        }
        _sessionKey = null;
        _localKey = null;
        _loginKey = null;
        _authKey = null;
        _isDisposed = true;
    }

    private void CheckDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(BluetoothTuyableConnection), "This instance has already been disposed.");
        }
    }   
}
