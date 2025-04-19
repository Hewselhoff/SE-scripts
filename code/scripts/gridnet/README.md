# GridNet
GridNet is a set of Programmable Block scripts that allow you to stand up a simple network for running programmable blocks on other grids remotely.

## Setup
To setup GridNet, perform the following steps on any grid(s) that you want to add to the network:
1. If your grid doesn't already have an antenna, build one and *make sure to enable broadcasting*.
2. Build a new Programmable Block on the grid and name it whatever you want, so long as it includes the tag `[MODEM]`. e.g. `MyModem [MODEM]`.
3. Copy the contents of the file `modem/GridModem.cs` into the Programmable Block's code editor and compile it.
4. Build a second Programmable Block on the grid and name it whatever you want, so long as it includes the tag `[ROUTER]`. e.g. `MyRouter [ROUTER]`.
5. Copy the contents of the file `router/GridRouter.cs` into the Programmable Block's code editor and compile it.

## Usage
### Running Manually from the SE Control Panel
To use GridNet to run PBs on other grids manually, perform the following steps:
1. Ensure that both grids have a modem and a router, and an antenna with broadcasting enabled.
2. Run the modem block on your grid with a URI argument in the following format:
    ```
    grid://<other_grid_name>/<target_pb_name>?<pb_arguments>
    ```
    where:
    - `<other_grid_name>` is the name of the grid that contains the PB you want to run
    - `<target_pb_name>` is the name of the PB you want to run on the other grid
    - `<pb_arguments>` the string you want to pass to the PB on the other grid

That's all there is to it. The target PB will get run on the other grid with the arguments you provided. 

#### Example
Suppose we have a PB named `LightPB` on another grid named `RemoteGrid`that supports the following arguments:
* `on` - instructs the PB to turn all lighting blocks on the grid on
* `off` - instructs the PB to turn all lighting blocks on the grid off

If we would like to turn all lights on the `RemoteGrid` on, we would call the modem block on our grid with the following URI as the argument:
```
grid://RemoteGrid/LightPB?on
```

And to torn the lights off again:
```
grid://RemoteGrid/LightPB?off
```

Simple as that.

### Running from a Script usng `GridNIC`
To run a PB on another grid from a script, you can use the `GridNIC` class:
1. Ensure that both grids have a modem and a router, and an antenna with broadcasting enabled.
2. Add the contents of the file `nic/GridNIC.cs` to the script you want to run the other PB from.
3. Add a global instance of the `GridNIC` class to your and initialize it in your script's `Program` method:
    ```csharp
    public GridNIC gridNic;

    ...

    public Program()
    {
        gridNic = new GridNIC(this);
        ...
    }
    ```
4. Use the `Send` method of the `GridNIC` class in your scripts `Main` method to send a message to a PB on another grid:
    ```csharp
    public void Main(string argument, UpdateType updateSource)
    {
        ...
        gridNic.Send("<other_grid_name>","<target_pb_name>","<pb_arguments>");
        ...
    }
    ```
    where:
    - `<other_grid_name>` is the name of the grid that contains the PB you want to run
    - `<target_pb_name>` is the name of the PB you want to run on the other grid
    - `<pb_arguments>` the string you want to pass to the PB on the other grid