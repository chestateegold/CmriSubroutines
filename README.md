# CMRI Subroutines

This project was built to address the lack of highly performative CMRI subroutines built on the .NET framework. The idea was to leverage modern object oriented features of .NET, as opposed to simply porting over the original VB6 subroutines. All examples are given in C#, but you can use any .NET language including Visual Basic.

These subroutines support the use of the [SMINI](https://www.jlcenterprises.net/collections/mini-node), [MAXI](https://www.jlcenterprises.net/collections/maxi-node) and [cpNode](http://www.modelrailroadcontrolsystems.com/cpnode-version-2-5/). If you would like another CMRI variant to be supported, feel free to open an issue on our github repo and we can start working on it!

# Getting Started

In order to use these subroutines, clone this respository and compile the project. Then add the resulting compiled DLL as a reference to the project that will use the subroutines.

## Initiating a COMPORT connection

In order to begin communicating with your CMRI node, you must first create a CmriSubroutines object. This initializes the COM PORT that you will be using and also sets up shared resources to be used during all calls made on that port. The first argument is required and chooses the COMPORT that you wish to use. The following paramaters are optional and allow you to manually set the BaudRate, MaxTries, Delay and MaxBuf. 

#### Using default values
```C#
using CmriSubRoutines;

int Port = 5;
SubRoutines subRoutines = new SubRoutines(Port);
```

#### Using explicit values
```C#
using CmriSubRoutines;

int Port = 5;
int Baud100 = 576;
int MaxTries = 5000;
int Delay = 50;
int MaxBuf = 64;
SubRoutines subRoutines = new SubRoutines(Port, Baud100, MaxTries, Delay, MaxBuf);
```

## Initiating a node

The next step is to initiate your node over your newly created COMPORT connection. To do this, simply call the INIT method of your new SubRoutines object. The first argument takes the node address. The second argument is an enumeration that explicitly sets what type of node is being initiated. The third is optional and is the CT array for the node.

#### Initiating node without a CT Array
```C#
int nodeAddress = 0;
subRoutines.Init(nodeAddress, NodeType.SMINI);
```

#### Initiating node with a CT array
```C#
int nodeAddress = 0;

// CT array populated with locations of 2 lead signal outputs
byte[] CT = new byte[]{ 3, 12, 198, 0, 0, 0 }; 

subRoutines.Init(nodeAddress, NodeType.SMINI, CT);
```

## Retreiving inputs from the node

To retreive inputs, call the INPUTS method of your subRoutines object. The argument is the address of your node. Set the results to a byte array. Each byte corresponds to each input card on your node.

```C#
int nodeAddress = 0;
byte[] inputs = subRoutines.Inputs(nodeAddress);
```

## Sending outputs to the node

To retreive outputs, call the OUTPUTS method of your subRoutines object. The first argument is the address of your node and the second argument is the array of bytes you are sending to your node.

```C#
int nodeAddress = 0;
byte[] outputs = new byte []{ 0b00000000, 0b11111111, 0b01010101 };

subRoutines.Outputs(nodeAddress, outputs);
```

## License

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details
