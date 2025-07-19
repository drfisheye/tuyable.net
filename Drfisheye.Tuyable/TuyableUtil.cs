using System;
using System.Buffers.Binary;

namespace Drfisheye.Tuyable;

public static class TuyableUtil
{

    public static (UInt32 value, int offset) ReadUint32(IEnumerable<byte> bytes, int offset)
    {
        return (BinaryPrimitives.ReadUInt32BigEndian(bytes.Skip(offset).Take(4).ToArray()), offset + 4);
    }

    public static (UInt16 value, int offset) ReadUint16(IEnumerable<byte> bytes, int offset)
    {
        return (BinaryPrimitives.ReadUInt16BigEndian(bytes.Skip(offset).Take(4).ToArray()), offset + 2);
    }

    public static void WriteUint32(List<byte> bytes, UInt32 value)
    {
        var data = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(data, value);
        bytes.AddRange(data);
    }

    public static void WriteUint32(Span<byte> bytes, int offset, UInt32 value)
    {
        BinaryPrimitives.WriteUInt32BigEndian(bytes.Slice(offset), value);
    }

    public static void WriteUint16(List<byte> bytes, UInt16 value)
    {
        var data = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(data, value);
        bytes.AddRange(data);
    }

    public static void WriteUint16(Span<byte> bytes, int offset, UInt16 value)
    {
        BinaryPrimitives.WriteUInt16BigEndian(bytes.Slice(offset), value);
    }

    public static (byte value, int offset) ReadByte(IEnumerable<byte> bytes, int offset)
    {
        return (bytes.Skip(offset).First(), offset + 1);
    }

    public static (int result, int offset) UnpackInt(byte[] data, int startPos)
    {
        int result = 0;
        int offset = 0;
        while (offset < 5)
        {
            int pos = startPos + offset;
            if (pos >= data.Length)
            {
                throw new Exception("Wrong format");
            }
            int curByte = data[pos];
            result |= (curByte & 0x7F) << (offset * 7);
            offset++;
            if ((curByte & 0x80) == 0)
            {
                break;
            }
        }
        if (offset > 4)
        {
            throw new Exception("Wrong format");
        }
        return (result, startPos + offset);
    }

    public static void PackInt(List<byte> result, int value)
    {
        byte currByte;

        while (true)
        {
            currByte = (byte)(value & 0x7F); // Get the lower 7 bits
            value >>= 7; // Shift value right by 7 bits

            if (value != 0)
            {
                currByte |= 0x80; // Set the most significant bit to indicate more bytes follow
            }

            result.Add(currByte); // Add the byte to the result list

            if (value == 0)
            {
                break; // All bits have been processed
            }
        }
    }

    public static Task<bool> TuyableCommandResponseHandler(TuyableNotification notification, CancellationToken token)
    {
        if (notification.Code != TuyableCommandCode.FUN_SENDER_DPS_V4)
        {
            return Task.FromResult(false);
        }
        (uint tuyaCommandSeqNem, _) = ReadUint32(notification.Message, 1);
        return Task.FromResult(tuyaCommandSeqNem == (notification.Context as TuyaCommandContext)?.TuyaCommandNum);
    }

}
