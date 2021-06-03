# VSCode Support

Xamarin.Android itself can be developed within VSCode. There is
a workspace included in the repo `Xamarin.Android.code-workspace`.
The required extensions should be installed when you open the
workspace.

## Building Xamarin.Android

Open the `Xamarin.Android.code-workspace` in vscode. Then use the
Build Command Pallette (Ctrl+Shift+B in Windows, Cmd+Shift+B on Mac)
to list the available build commands.

Select "Build All Xamarin.Android". You will then be presented with a
list of options.

* Prepare - Installs the required Dependencies.
* PrepareExternal - Installs the required Commercial Dependencies (Xamarin Team Members Only)
* Build - Build Xamarin. Android.
* Pack - Create the Nuget Packages.
* Everything - Calls Prepare, Build and Pack.

The normal order is `Prepare`, `Build` then `Pack`. This will result in
a usable copy of Xamarin.Android. You can now use it to build apps
and run the unit tests.

Note: PrepareExternal is for internal Xamarin Team members only, this sets up
the commercial parts of the repository. Trying to use this command when you
do not have access to the required repositories will result in a failure.

## Running/Debugging Unit Tests

Xamarin.Android Legacy uses the `wghats.vscode-nxunit-test-adapter` extension
to allow you to run and debug unit tests. This extension should be installed
automatically.

We also use `derivitec-ltd.vscode-dotnet-adapter` to run the tests under
`dotnet`. These two extensions have pretty much the same functionality.

### wghats.vscode-nxunit-test-adapter

In order to run or debug the tests you will need to set a path
to the `nunit-console.exe` file. This is will be located in your
global nuget cache, usually  in `$HOME/.nuget`.

The setting you need to set is `nxunitExplorer.nunit`. This will need
to be done in your `Preferences->Settings` in VSCode. This will save
the setting globally so it will be available when ever you open
Xamarin.Android.

Alternatively you can add something like the following to the `Xamarin.Android.code-workspace`.

MacOS Setting
```json
"nxunitExplorer.nunit": "%HOME%/.nuget/packages/nunit.consolerunner/3.11.1/tools/nunit3-console.exe"
```

Windows Setting
```json
"nxunitExplorer.nunit": "%USERPROFILE%/.nuget/packages/nunit.consolerunner/3.11.1/tools/nunit3-console.exe"
```

The path will change depending on where your nuget cache is and depending on which
Nunit version is being used. Note you can use environment variables in this path.
However you need to use *Windows* style environment variables (`%var%`) rather
than *nix based ones ($var) because of the way the `nxunit-explorer` parses the
settings.

Once that is setup you can to go the `Testing` tab in VSCode and browse/run/debug
the unit tests.

Note: There is a slight problem with the nxunitExplorer in that it does not escape
the names of the unit tests it runs. As a result the unit tests which contain
arguments do not run within VSCode. However a work around is to run the top
level fixture rather than the single test.

Alternatively you can download the patched executable from [here](https://github.com/dellis1972/vscode-nxunit-test-adapter/releases/tag/Patch2) and replace
your `testrun` files with these. This will allow you to run and debug all the
tests. The upstream pull request is [PR15](prash-wghats/vscode-nxunit-test-adapter#15)

### derivitec-ltd.vscode-dotnet-adapter

This should work out of the box as long as you have `dotnet` installed. This is
a dependency for Xamarin.Android so it should be installed as soon as you
build Xamarin.Android.

### Running the Sample

Xamarin.Android provides a few Debug configurations in VSCode to debug the
sample applications `HelloWorld` and `VSAndroidApp`. You can select the
`Debug Sample` Run and Debug config to run and debug the samples under
`mono` or you can use `Debug Sample Under DotNet` to debug this under the
.net 6 system.

In order to use the `Debug Sample Under DotNet` you will need to have built
Xamarin.Android for .net 6 using the `Pack` command mentioned earlier.
See the [windows documentation](../building/windows/instructions.md) or
[*nix documentation](../building/unix/instructions.md) for additional details
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
    "default": "samples/HelloWorld/HelloWorld.csproj",
    "description": "Pick the Project you want to build.",
    "options": [
        "samples/HelloWorld/HelloWorld.csproj",
        "samples/HelloWorld/HelloWorld.DotNet.csproj",
        "samples/VSAndroidApp/VSAndroidApp.csproj",
    ]
},
```

you can add your own projects here and they will run with the Xamarin.Android
you have built locally.



