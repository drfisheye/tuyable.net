# Tuyable.NET

A C# library for communicating with Tuya BLE devices, though as for now really only the Smart Dot device by Petoneer (and other brandnames).

## Features

- Connect and pair with a Tuya Smart Dot over Bluetooth LE

## Getting Started

1. Clone the repository.
2. Open the solution in Visual Studio.
3. Build the project.
4. Use the Tuya Cli project to find your device id and key.
5. Find the MAC of your device via the Tuya/Smart Life app.
4. Use code like this
```csharp
	var logger = new MyLogger(); // make your own logger, implement ITuyableLogger
	SmartDot smartDot = new SmartDot("your device id", "your device mac (without colons)", "your device key");
	var cancelationToken = new CancellationTokenSource(1000 * 60 * 10).Token;

	using (BluetoothTuyableConnection connection = new BluetoothTuyableConnection(logger, smartDot.Device))
	{	
		if (!await connection.TryConnect(4, cancelationToken))
		{
			return;
		}

		var smartDotController = smartDot.GetController(connection);
		var shortCancellationToken = new CancellationTokenSource(1000 * 8).Token;
		if (!await smartDotController.On(shortCancellationToken))
		{
			return;
		}
		await smartDotController.Play(SmartDotProgram.Small, shortCancellationToken);
	}
```

## Requirements

- [InTheHand.Bluetooth](https://github.com/inthehand/BTLibrary)

## License

MIT License