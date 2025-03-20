# NativeAOT Sample

This is the sample for using NativeAOT with .NET for Android.

## Debugging

In order to debug we need to use `lldb`. First install the application

```dotnetcli
./dotnet-local.sh build samples/NativeAOT/NativeAOT.csproj -c Release -p:DebugSymbols=true -t:Install
```

If you are using VSCode from the android repo, you can go to the debugging tab, and select "Debug NativeAOT". This will let you debug the NativeAOT sample. Fire up your emulator, click the play button next to "Debug NativeAOT" and it will launch the app. If the Java debugger dialog is not cleared automatically, got to the "Debug Console" which should be showing the lldb REPL, and type `clearjdb`. Your app should then continue.

Alternatively you can then use the `runwithdebugger.sh`/`runwithdebugger.ps1` scripts to run all the steps you need to setup the debugger manaully.
What follows is an explanation of what that script does.

## How `runwithdebugger.*` scripts work

In order to lldb debugging to work we need to make sure we install the `lldb-server` on the device.
The following steps, install the `lldb-server`, stop any existing lldb-server process and they launches
it. It also sets up the required port forwarding so we can connect to the server from the local machine.

```shell
adb push $(ANDROID_NDK_HOME)/toolchains/llvm/prebuilt/darwin-x86_64/lib/clang/18/lib/linux/aarch64/lldb-server /data/local/tmp/lldb-server
adb shell run-as net.dot.hellonativeaot cp /data/local/tmp/lldb-server .
adb forward tcp:5039 tcp:5039
adb shell run-as net.dot.hellonativeaot killall -9 lldb-server
adb shell run-as net.dot.hellonativeaot ./lldb-server platform --listen "*:5039" --server
```

Note: We have to run the `lldb-server` in the context of the app so it has the correct networking permissions. This is why we use the `run-as` command.

Once `lldb-server` is up and running, you will want to run up the app using the following

```shell
adb shell am start -S --user "0" -a "android.intent.action.MAIN" -c "android.intent.category.LAUNCHER" -n "net.dot.hellonativeaot/my.MainActivity" -D
```

The `-D` is important as it stops the app from running until the java debugger is attached. This can be useful if your app is crashing on startup.
If you do not want to the app to pause on startup, you can omit the `-D` argument.

Now that the app is running we need to get the process id. We do that using `adb shell ps`. 
The `grep` is used to filter the results to only our app.

```shell
adb shell ps | grep net.dot.hellonativeaot
```

Grab the process id, then using lldb from the command line use the following commands.
These setup `lldb` and loads the symbols, then attaches to the process. It also sets up
a `clearjdb` command which you can use from within the `lldb` REPL to clear the Java
Debugger dialog you get when you launch your app.

```dotntcli
lldb
> platform select remote-android
> platform connect connect://localhost:5039 
> settings set target.process.thread.step-in-avoid-nodebug true
> settings set target.process.thread.step-out-avoid-nodebug true
> command script import samples/NativeAOT/lldb_commands.py,
> command script add -f lldb_commands.clearjdb clearjdb
> target create samples/NativeAOT/bin/Debug/net10.0-android/android-arm64/native/NativeAOT.so
> target select 0
> process attach --pid <processid>
```

if your symbols are in a separate `.dbg` file, you can use the following.

```
> target symbols add samples/NativeAOT/bin/Debug/net10.0-android/android-arm64/native/NativeAOT.so.dbg
```

Next you need to attach the java debugger to clear the dialog which is currently blocking the application execution. You can skip this step if you omitted the `-D` when
launching the activity.

you can do this via the `lldb` terminal by using the `clearjdb` function which is an
extension function we have.

or use the following from the command line.

`python3 samples/NativeAOT/lldb_commands.py`

you can also clear it manually via

```
adb forward --remove tcp:8700
adb forward tcp:8700 jdwp:<pid>
jdb -attach localhost:8700 
```

You will want to type `quit` to exit the `jdb` terminal once it has connected.

## Breakpoints

Setting breakpoints just need to use the filename and line number, these are just standard `lldb` commands.

```
breakpoint set -f MainActivity.cs -l 34
```

This will place a breakpoint at line 34 of MainActivity.cs.
