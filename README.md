# CMRI Subroutines

This project was built to address the lack of highly performative CMRI subroutines built on .NET. The idea was to leverage modern object oriented features of .NET, as opposed to simply porting over the original VB6 subroutines. All examples are given in C#, but you can use any .NET language including Visual Basic.

Supported target frameworks:
- .NET 10
- .NET Framework 4.8

These subroutines support the use of the [SMINI](https://www.jlcenterprises.net/collections/mini-node), [MAXI](https://www.jlcenterprises.net/collections/maxi-node) and [cpNode](http://www.modelrailroadcontrolsystems.com/cpnode-version-2-7/). If you would like another CMRI variant to be supported, feel free to open an issue on our github repo and we can start working on it!

# Getting Started

In order to use these subroutines, clone this repository and compile the project. Then add the resulting compiled DLL as a reference to the project that will use the subroutines.

## Initiating a COMPORT connection

In order to begin communicating with your CMRI node, you must first create a CmriSubroutines object. This initializes the COM PORT that you will be using and also sets up shared resources to be used during all calls made on that port. The first argument is required and chooses the COMPORT that you wish to use. The following paramaters are optional and allow you to manually set the BaudRate, MaxTries, Delay and MaxBuf. 

#### Using the serial factory with default values
```C#
using CmriSubRoutines;

int Port = 5;
Subroutines subRoutines = await Subroutines.CreateSerial(Port);
```

#### Using the serial factory with explicit values
```C#
using CmriSubRoutines;

int Port = 5;
BaudRate baudRate = BaudRate.B57600;
int timeoutMs = 5000;
int Delay = 50;
int MaxBuf = 64;
Subroutines subRoutines = await Subroutines.CreateSerial(Port, baudRate, timeoutMs, Delay, MaxBuf);
```

## Initiating a node

The next step is to initiate your node over your newly created COMPORT connection. To do this, simply call the INIT method of your new SubRoutines object. The first argument takes the node address. The second argument is an enumeration that explicitly sets what type of node is being initiated. The third is optional and is the CT array for the node.

#### Initiating node without a CT Array
```C#
int nodeAddress = 0;
await subRoutines.Init(nodeAddress, NodeType.SMINI);
```

#### Initiating node with a CT array
```C#
int nodeAddress = 0;

// CT array populated with locations of 2 lead signal outputs
byte[] CT = new byte[]{ 3, 12, 198, 0, 0, 0 }; 

await subRoutines.Init(nodeAddress, NodeType.SMINI, CT);
```

## Retreiving inputs from the node

To retreive inputs, call the INPUTS method of your subRoutines object. The argument is the address of your node. Set the results to a byte array. Each byte corresponds to each input card on your node.

```C#
int nodeAddress = 0;
byte[] inputs = await subRoutines.Inputs(nodeAddress);
```

## Sending outputs to the node

To retreive outputs, call the OUTPUTS method of your subRoutines object. The first argument is the address of your node and the second argument is the array of bytes you are sending to your node.

```C#
int nodeAddress = 0;
byte[] outputs = new byte []{ 0b00000000, 0b11111111, 0b01010101 };

await subRoutines.Outputs(nodeAddress, outputs);
```

## Using the TCP transport (ser2net)

This library includes a TCP transport that connects to a serial device exposed by a server such as `ser2net` on a Raspberry Pi. Use the TCP factory to create `Subroutines`.

Example client usage:

```C#
using CmriSubroutines;

var sub = await Subroutines.CreateTcp("CmriPi", 3333, timeoutMs: 3000, delay: 0, maxBuf: 64);

await sub.Init(0, NodeType.SMINI);
var inputs = await sub.Inputs(0);
await sub.Outputs(0, new byte[] { 0, 0, 0 });

```

Recommended `ser2net` configuration (Pi-side) to expose `/dev/ttyUSB0` on TCP port 3333:

```
connection: &conn1
    accepter: tcp,0.0.0.0,3333
    connector: serialdev,/dev/ttyUSB0,9600n82
```


## Using the in-memory transport (for tests)

A `MemoryTransport` is provided for unit testing and offline development. Use the memory factory to create `Subroutines` and preload bytes that it will read.

Basic usage:

```csharp
using CmriSubroutines;

var sub = Subroutines.CreateMemory(new byte[] { 2, (byte)(0 + 65), (byte)'R', 0, 0, 0, 3 }, timeoutMs: 3000, delay: 0, maxBuf: 64);

// call the async APIs
var inputs = await sub.Inputs(0);

// inspect the state through your test transport setup
```

Notes:
- `CreateMemory` accepts an optional sequence of bytes to be returned by subsequent reads.
- Use the memory transport directly in tests if you need to inspect written bytes.
- SMINI CT array for 2 lead LEDs is untested on hardware. My SMINI initializes with it properly but I don't have any 2 lead LEDs to test with

## Tested node types

| Node type | Inputs tested | Outputs tested | 2 Lead LEDs tested |
| --- | --- | --- | --- |
| SMINI | Yes | Yes | No |
| MAXI 24 with CIN/COUT cards | Yes | Yes | N/A |
| MAXI 24 with DIN/DOUT cards | Yes | No | N/A |
| MAXI 32 with DIN/DOUT cards | No | No | N/A |
| MRCS cpNode | Yes | Yes | No |

## License

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details

## NuGet package

The project is packable as a NuGet package with metadata defined in the SDK-style project file.

- Package ID: `CmriSubroutines`
- Repository: https://github.com/chestateegold/CmriSubroutines
- License: MIT

