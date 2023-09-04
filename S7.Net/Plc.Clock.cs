﻿using System;
using System.IO;
using System.Linq;
using S7.Net.Helper;
using S7.Net.Types;
using DateTime = System.DateTime;

namespace S7.Net;

partial class Plc
{
    private const byte SzlFunctionGroupTimers = 0x07;
    private const byte SzlSubFunctionReadClock = 0x01;
    private const byte SzlSubFunctionWriteClock = 0x02;
    private const byte TransportSizeOctetString = 0x09;
    private const int PduErrOffset = 20;
    private const int UserDataResultOffset = PduErrOffset + 2;

    private static byte[] BuildClockReadRequest()
    {
        var stream = new MemoryStream();

        WriteUserDataRequest(stream, SzlFunctionGroupTimers, SzlSubFunctionReadClock, 4);
        stream.Write(new byte[] { 0x0a, 0x00, 0x00, 0x00 });

        stream.SetLength(stream.Position);
        return stream.ToArray();
    }

    private static DateTime ParseClockReadResponse(byte[] message)
    {
        const int udLenOffset = UserDataResultOffset + 2;
        const int udValueOffset = udLenOffset + 4;

        AssertPduResult(message);
        AssertUserDataResult(message, 0xff);

        var len = Word.FromByteArray(message.Skip(udLenOffset).Take(2).ToArray());
        if (len != Types.DateTime.Length)
        {
            throw new Exception($"Unexpected response length {len}, expected {Types.DateTime.Length}.");
        }

        return Types.DateTime.FromByteArray(message.Skip(udValueOffset).Take(Types.DateTime.Length).ToArray());
    }

    private static byte[] BuildClockWriteRequest(DateTime value)
    {
        var stream = new MemoryStream();

        WriteUserDataRequest(stream, SzlFunctionGroupTimers, SzlSubFunctionWriteClock, 14);
        stream.Write(new byte[] { 0xff, TransportSizeOctetString, 0x00, Types.DateTime.Length });
        stream.Write(new byte[] { 0x00, 0x19 }); // Start of actual DateTime value, DateTime.ToByteArray is broken
        stream.Write(Types.DateTime.ToByteArray(value));

        stream.SetLength(stream.Position);
        return stream.ToArray();
    }

    private static void ParseClockWriteResponse(byte[] message)
    {
        AssertPduResult(message);
        AssertUserDataResult(message, 0x0a);
    }

    private static void AssertPduResult(byte[] message)
    {
        var pduErr = Word.FromByteArray(message.Skip(PduErrOffset).Take(2).ToArray());
        if (pduErr != 0)
        {
            throw new Exception($"Response from PLC indicates error 0x{pduErr:X4}.");
        }
    }

    private static void AssertUserDataResult(byte[] message, byte expected)
    {
        var dtResult = message[UserDataResultOffset];
        if (dtResult != expected)
        {
            throw new Exception($"Response from PLC was 0x{dtResult:X2}, expected 0x{expected:X2}.");
        }
    }
}