package net.dot.jni.nativeaot;

import android.util.Log;

public class JavaInteropRuntime {
    static {
        Log.d("JavaInteropRuntime", "Loading libHello-NativeAOTFromAndroid.so…");
        System.loadLibrary("Hello-NativeAOTFromAndroid");
    }

    private JavaInteropRuntime() {
    }

    public static native void init();
}
