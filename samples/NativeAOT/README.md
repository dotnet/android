# NativeAOT Sample

This is the sample for using NativeAOT with .net Android

## Debugging

In order to debug we need to use `lldb`. The following commands will install the `lldb-server` for the application.

```dotnetcli
adb push $(ANDROID_NDK_HOME)/toolchains/llvm/prebuilt/darwin-x86_64/lib/clang/18/lib/linux/aarch64/lldb-server /data/local/tmp/lldb-server
adb shell run-as net.dot.hellonativeaot cp /data/local/tmp/lldb-server .
adb forward tcp:5039 tcp:5039
adb shell run-as net.dot.hellonativeaot killall -9 lldb-server
adb shell run-as net.dot.hellonativeaot ./lldb-server platform --listen "*:5039" --server
```

The above commands are setup in a MSBuild Target which will run after the `Install` target has run. So there
is no need to do this manually if you are using `NativeAOT.csproj`.

Once `lldb-server` is up and running, you will want to run up the app using the following

```dotnetcli
adb shell am start -S --user "0" -a "android.intent.action.MAIN" -c "android.intent.category.LAUNCHER" -n "net.dot.hellonativeaot/my.MainActivity" -D
```

The `-D` is important as it stops the app from running until the java debugger is attached.
Now that the app is running we need to get the process id.

```dotnetcli
adb shell ps | grep net.dot.hellonativeaot
```

Grab the process id, then start VSCode `Debug NativeAOT` Task. And drop in the process id.

Set your breakpoints as usual in the VSCode.

if you are using lldb from the command line use the following

```dotnetcli
lldb
> platform select remote-android
> platform connect connect://localhost:5039
> settings set target.process.thread.step-in-avoid-nodebug true
> settings set target.process.thread.step-out-avoid-nodebug true
> target create samples/NativeAOT/bin/Debug/net10.0-android/android-arm64/native/NativeAOT.so
> target symbols add samples/NativeAOT/bin/Debug/net10.0-android/android-arm64/native/NativeAOT.so.dbg
> target select 0
> attach --pid <processid>
```

Next you need to attach the java debugger to clear the dialog which is currently blocking the
application execution.

```dotnetcli
adb forward --remove tcp:8700
adb forward tcp:8700 jdwp:<pid>
jdb -attach localhost:8700 
```

You will want to type `quit` to exit the `jdb` terminal once it has connected.
