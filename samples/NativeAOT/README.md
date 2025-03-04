# NativeAOT Sample

This is the sample for using NativeAOT with .net Android

## Debugging

In order to debug we need to use `lldb`. First install the application

```dotnetcli
./dotnet-local.sh build samples/NativeAOT/NativeAOT.csproj -c Debug -t:Install
```

Then user the following commands to install the `lldb-server` for the application.

```dotnetcli
adb push $(ANDROID_NDK_HOME)/toolchains/llvm/prebuilt/darwin-x86_64/lib/clang/18/lib/linux/aarch64/lldb-server /data/local/tmp/lldb-server
adb shell run-as net.dot.hellonativeaot cp /data/local/tmp/lldb-server .
adb forward tcp:5039 tcp:5039
adb shell run-as net.dot.hellonativeaot killall -9 lldb-server
adb shell run-as net.dot.hellonativeaot ./lldb-server platform --listen "*:5039" --server
```

Note: We have to run the `lldb-server` in the context of the app so it has the correct networking permissions. This is why we use the `run-as` command.

Once `lldb-server` is up and running, you will want to run up the app using the following

```dotnetcli
adb shell am start -S --user "0" -a "android.intent.action.MAIN" -c "android.intent.category.LAUNCHER" -n "net.dot.hellonativeaot/my.MainActivity" -D
```

The `-D` is important as it stops the app from running until the java debugger is attached. This can be useful if your app is crashing on startup.
If you do not want to the app to pause on startup, you can omit the `-D` argument.

Now that the app is running we need to get the process id.

```dotnetcli
adb shell ps | grep net.dot.hellonativeaot
```

Grab the process id, then using lldb from the command line use the following

```dotnetcli
lldb
> platform select remote-android
> platform connect connect://localhost:5039 
> settings set target.process.thread.step-in-avoid-nodebug true
> settings set target.process.thread.step-out-avoid-nodebug true
> target create samples/NativeAOT/bin/Debug/net10.0-android/android-arm64/native/NativeAOT.so
> target select 0
> process attach --pid <processid>
```

if your symbols are in a separate `.dbg` file, you can use the following.

```dotnetcli
> target symbols add samples/NativeAOT/bin/Debug/net10.0-android/android-arm64/native/NativeAOT.so.dbg
```

Next you need to attach the java debugger to clear the dialog which is currently blocking the application execution. You can skip this step if you omitted the `-D` when
launching the activity.

```dotnetcli
adb forward --remove tcp:8700
adb forward tcp:8700 jdwp:<pid>
jdb -attach localhost:8700 
```

You will want to type `quit` to exit the `jdb` terminal once it has connected.

## Breakpoints

Setting breakpoints just need to use the filename and line number, these are just standard `lldb` commands.

```dotnetcli
breakpoint set -f MainActivity.cs -l 34
```

This will place a breakpoint at line 34 of MainActivity.cs.
