# VSCode Support

.NET for Android itself can be developed within
[Visual Studio Code (VSCode)](https://code.visualstudio.com/).
There is a workspace included in the repo `Xamarin.Android.code-workspace`.
The required extensions should be installed when you open the
workspace.

## Building .NET for Android

Open the `Xamarin.Android.code-workspace` in VSCode. Then use the
Build Command Pallette (Ctrl+Shift+B in Windows, Cmd+Shift+B on Mac)
to list the available build commands.

Select **Build All .NET for Android**. You will then be presented with a
list of options:

* `Prepare` - Installs the required Dependencies.
* `PrepareExternal` - Installs the required Commercial Dependencies (Xamarin Team Members Only)
* `Build` - Build .NET for Android.
* `Pack` - Create the NuGet Packages.
* `Everything` - Calls Prepare, Build and Pack.

The normal order is `Prepare`, `Build` then `Pack`. This will result in
a usable copy of .NET for Android. You can now use it to build apps
and run the unit tests.

Note: `PrepareExternal` is for internal Xamarin Team members only, this sets up
the commercial parts of the repository. Trying to use this command when you
do not have access to the required repositories will result in a failure.

## Running/Debugging Unit Tests

Xamarin.Android Legacy uses the
[wghats.vscode-nxunit-test-adapter](https://marketplace.visualstudio.com/items?itemName=wghats.vscode-nxunit-test-adapter) extension
to allow you to run and debug unit tests. This extension should be installed
automatically.

We also use the
[derivitec-ltd.vscode-dotnet-adapter](https://marketplace.visualstudio.com/items?itemName=derivitec-ltd.vscode-dotnet-adapter) extension
to run the tests under `dotnet`. These two extensions have pretty much the same functionality.

### wghats.vscode-nxunit-test-adapter

In order to run or debug the tests you will need to set a path
to the `nunit-console.exe` file. This is will be located in your
global nuget cache, usually  in `$HOME/.nuget` or `%USERPROFILE%/.nuget`.

The setting you need to set is `nxunitExplorer.nunit`. This will need
to be done in your **Preferences** > **Settings** in VSCode. This will save
the setting globally so it will be available when ever you open
.NET for Android.

Alternatively you can add something like the following to the `Xamarin.Android.code-workspace`:

macOS or Linux Setting:

```json
"nxunitExplorer.nunit": "%HOME%/.nuget/packages/nunit.consolerunner/3.11.1/tools/nunit3-console.exe"
```

Windows Setting:

```json
"nxunitExplorer.nunit": "%USERPROFILE%/.nuget/packages/nunit.consolerunner/3.11.1/tools/nunit3-console.exe"
```

The path will change depending on where your NuGet cache is and depending on which
NUnit version is being used. You can use environment variables in this path.
However you need to use *Windows* style environment variable syntax (`%var%`) rather
than \*nix based syntax (`$var`) because of the way the `nxunit-explorer` parses the
settings.

Once that is setup you can to go the `Testing` tab in VSCode and browse/run/debug
the unit tests.

Note: There is a slight problem with `nxunitExplorer` in that it does not escape
the names of the unit tests it runs. As a result the unit tests which contain
arguments do not run within VSCode. However a work around is to run the top
level fixture rather than the single test.

Alternatively you can download the patched executable from [here](https://github.com/dellis1972/vscode-nxunit-test-adapter/releases/tag/Patch2) and replace
your `testrun` files with these. This will allow you to run and debug all the
tests. The upstream pull request is
[prash-wghats/vscode-nxunit-test-adapter#15](https://github.com/prash-wghats/vscode-nxunit-test-adapter/pull/15).

### derivitec-ltd.vscode-dotnet-adapter

This should work out of the box as long as you have `dotnet` installed. This is
a dependency for .NET for Android so it should be installed as soon as you
build .NET for Android.

### Running the Sample

.NET for Android provides a few Debug configurations in VSCode to debug the
sample applications `HelloWorld` and `VSAndroidApp`. You can select the
`Debug Sample` Run and Debug config to run and debug the samples under
`mono` or you can use `Debug Sample Under DotNet` to debug this under the
.net 6 system.

In order to use the `Debug Sample Under DotNet` you will need to have built
.NET for Android for .net 6 using the `Pack` command mentioned earlier.
See the [windows documentation](../building/windows/instructions.md) or
[\*nix documentation](../building/unix/instructions.md) for additional details
on working with .net 6.

When you select a `Debug` config and click the run button you will be asked
to select the `Configuration` you want to use, followed by the `Project` and
finally if you want to attach the debugger. If you want to alter the list of
projects you can select you can change the list for the `project` input section
in `.vscode/tasks.json`.

```json
{
    // Add additional projects here. They will be available in the drop down
    // in vscode.
    "id": "project",
    "type": "pickString",
    "default": "samples/HelloWorld/HelloWorld/HelloWorld.csproj",
    "description": "Pick the Project you want to build.",
    "options": [
        "samples/HelloWorld/HelloWorld/HelloWorld.csproj",
        "samples/HelloWorld/HelloWorld/HelloWorld.DotNet.csproj",
        "samples/VSAndroidApp/VSAndroidApp.csproj"
    ]
},
```

you can add your own projects here and they will run with the .NET for Android
you have built locally.



