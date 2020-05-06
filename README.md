# CMRI Subroutines

This project was built to address the lack of highly performative CMRI subroutines built on the .NET framework. The idea was to leverage modern object oriented features of .NET, as opposed to simply porting over the original VB6 subroutines. All examples are given in C#.

# Currently Unsupported Features
Currently the package only supports SMINI nodes. Also, support for dual lead signals is not yet supported for the SMINI. Support for MAXI nodes, dual lead signals and cpNodes is on the way!

## Getting Started

In order to use these subroutines, clone this respository and compile the project. Then add the resulting compiled DLL as a reference to your project that will use the subroutines.

## Initiating a COMPORT connection

In order to begin communicating with your CMRI node, you must first create a CmriSubroutines object. This initializes the COM PORT that you will be using and sets up shared resources to be used during all calls made on that port. The first argument is required and chooses the COMPORT that you wish to use. The second is optional and sets the baud rate. 

```C#
using CmriSubRoutines;
SubRoutines subRoutines = new SubRoutines(5);
```

## Initiating a node

The next step is to initiate your node over your newly created COMPORT connection. To do this, simply call the INIT method of your new SubRoutines object. This call takes a single argument with the node address.

```C#
subRoutines.INIT(0);
```

## Retreiving inputs from the node

To retreive inputs, call the INPUTS method of your subRoutines object. The argument is the address of your node. Set the results to a byte array. Each byte corresponds to each input card on your node.

```C#
byte[] inputs = subRoutines.INPUTS(0);
```

## Sending outputs to the node

To retreive outputs, call the OUTPUTS method of your subRoutines object. The first argument is the address of your node and the second argument is the array of bytes you are sending to your node.

```C#
byte[] outputs = new byte []{(byte)0b00000000, (byte)0b11111111, (byte)0b01010101};
subRoutines.OUTPUTS(0, outputs);
```

## License

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details
