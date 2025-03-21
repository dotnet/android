# NativeAOT Sample

This is the sample for using NativeAOT with .NET for Android.

## Debugging

If you are using VSCode from the android repo, you can go to the "Run and Debug" Badge, and select "Debug NativeAOT" from the drop down. This will let you debug the NativeAOT sample. Fire up your emulator or connect your device, then click the play button next to "Debug NativeAOT" and it will launch the app. If the Java debugger dialog is not cleared automatically, goto the "Debug Console" which should be showing the lldb REPL, and type `clearjdb`. Your app should then continue.

Note it will take some time to build and install the NativeAOT sample. VSCode will probably
give you an option to cancel or wait, you can just ignore this dialog.

If you want to to use `lldb` directly then you can use the `runwithdebugger.sh`/`runwithdebugger.ps1` scripts to run all the steps you need to setup the debugger manually.
What follows is an explanation of what that script does.

## How `runwithdebugger.*` scripts work

The first this the script does is to install the application. It does this using the
`Install` MSBuild target.

```shell
dotnet-local.sh build samples/NativeAOT/NativeAOT.csproj -c Debug -t:Install -tl:off
```

Next it needs to install `lldb-server` on the device.
The following steps, install the `lldb-server`, stop any existing lldb-server process and then launches it. It also sets up the required `adb` port forwarding so we can connect to the server from the local machine.

```shell
adb push $(ANDROID_NDK_HOME)/toolchains/llvm/prebuilt/darwin-x86_64/lib/clang/19/lib/linux/aarch64/lldb-server /data/local/tmp/lldb-server
adb shell run-as net.dot.hellonativeaot cp /data/local/tmp/lldb-server .
adb forward tcp:5039 tcp:5039
adb shell run-as net.dot.hellonativeaot killall -9 lldb-server
adb shell run-as net.dot.hellonativeaot ./lldb-server platform --listen "*:5039" --server
```

Note: We have to run the `lldb-server` in the context of the app so it has the correct networking permissions. This is why we use the `run-as` command.

Note: the path `clang/*/lib/` might change depending on your version of the NDK.

Once `lldb-server` is up and running, the script with run up the app using the following

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

The script then uses `awk` or and `regex` to extract the process id.
It then adds an `adb` port forward for port `8700` to this process id. This is so we can easily
attach the java debugger (`jdb`) to the app to clear the debugger dialog box.

## What does the launch.json do?

In addition to the script above, there are a number of commands in `launch.json` which are
run when launching via VSCode. If you want to run these manually rather than using VSCode
you will need to use `lldb` from the command line it execute the following commands.

```shell
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

These setup `lldb` and loads the symbols, then attaches to the process. It also sets up
a `clearjdb` command which you can use from within the `lldb` REPL to clear the Java
Debugger dialog you get when you launch your app.

If your symbols are in a separate `.dbg` file, you can use the following.

```shell
> target symbols add samples/NativeAOT/bin/Debug/net10.0-android/android-arm64/native/NativeAOT.so.dbg
```

Next you need to attach the java debugger to clear the dialog which is currently blocking the application execution. You can skip this step if you omitted the `-D` when
launching the activity.

you can do this via the `lldb` terminal by using the `clearjdb` function which is an
extension function we have.

or use the following from the command line.

`python3 samples/NativeAOT/lldb_commands.py`

alternatively, you can also clear it manually via

```shell
adb forward --remove tcp:8700
adb forward tcp:8700 jdwp:<pid>
jdb -attach localhost:8700 
```

You will want to type `quit` to exit the `jdb` terminal once it has connected.
If you used the script you will not need to setup the `adb` port forward.

## Breakpoints

Setting breakpoints just need to use the filename and line number, these are just standard `lldb` commands.

```shell
breakpoint set -f MainActivity.cs -l 34
```

This will place a breakpoint at line 34 of MainActivity.cs.
