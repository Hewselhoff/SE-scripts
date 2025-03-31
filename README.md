# Setup and Configure VScode for Space Engineers


## Install Dependencies
1. Install Space Engineers via Steam (obviously).
2. Install .NET CLI tools [version 9.0.202](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/sdk-9.0.202-windows-x64-installer)
3. Install .NET Framework Devpack [version 4.6.2](https://dotnet.microsoft.com/en-us/download/dotnet-framework/thank-you/net462-developer-pack-offline-installer)
4. Install [Visual Studio Community](https://visualstudio.microsoft.com/thank-you-downloading-visual-studio/?sku=Community&channel=Release&version=VS2022&source=VSLandingPage&cid=2030&passive=false)

## Install VScode
1. Install [Visual Studio Code](https://code.visualstudio.com/Download)
2. Install the following VScode extensions:
    * *C#* - Base language support extension
    * *C# Dev Kit* - Official C# extensions
    * *NuGet Package Manager* - Add and remove .NET core packages
    * *.NET Install Tool* - Extension to install and manage .NET SDK versions
    * *Space Engineers Helper Extension*

## Create a Script Project
1. Open VScode
2. In VScode, open the folder you want to use for the project
3. Run the Space Engineers Helper extension's `CreateBaseSEProject` command:
    1. Open the command palette (`CTRL+p`)
    2. In the command palette, type `>CreateBaseSEProject` and press `enter`. 

This should create the directory tree and all necessary files for your script project.

### Configure the Project
Before VScode will recognize the SE libraries, you need to set the correct path to the `dll` files in the `.csproj` file. To do this:
1. Open the `SpaceEngineers.csproj` file
2. Use find to search for the tag: `<SpaceEngineers>`
3. Replace the path between the tags with the path to the Space Engineers `Bin64` directory on your machine. on Windows, this is most likely:
    ```
    C:\Program Files (x86)\Steam\steamapps\common\SpaceEngineers\Bin64\
    ```
    > **NOTE:** Make sure to include a trailing `\` at the end of the path, otherwise the extension will fail to find the libraries.

### Create Your Script
Once the project is configured, you can begin writing your script. The script should be placed in the `Scripts` directory of the project directory tree. The SE helper extension will automatically generate a script named `SampleMainFile.cs` in this directory. You can either modify this file or delete it and use the following empty script template:

```c#
#region Prelude
using System;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;

using VRageMath;
using VRage.Game;
using VRage.Collections;
using Sandbox.ModAPI.Ingame;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using Sandbox.Game.EntityComponents;
using SpaceEngineers.CommonLibs;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;

// Change this namespace for each script you create.
namespace SpaceEngineers.UWBlockPrograms.HelloWorld {
    public sealed class Program : MyGridProgram {
    // Your code goes between the next #endregion and #region
#endregion

public Program() {    
}

public void Main(string args) {

}

#region PreludeFooter
    }
}
#endregion
```

> **Note:** Be sure to replace the `HelloWorld` in the `namespace` line with a unique name for your script.
