package net.dot.jni.nativeaot;

import android.util.Log;

public class JavaInteropRuntime {
    static {
        Log.d("JavaInteropRuntime", "Loading NativeAOT.so...");
        System.loadLibrary("NativeAOT");
    }

    private JavaInteropRuntime() {
    }

    public static native void init();
}
