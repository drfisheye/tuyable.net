using System;

namespace Drfisheye.Tuyable;

public class TuyaCommandContext
{
	public TuyaCommandContext(uint tuyaCommandContext)
	{
		TuyaCommandNum = tuyaCommandContext;
	}

	public uint TuyaCommandNum { get; set; }
}

