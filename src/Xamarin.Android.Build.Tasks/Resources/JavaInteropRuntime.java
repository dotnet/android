package net.dot.jni.nativeaot;

import android.util.Log;

public class JavaInteropRuntime {
    static {
        Log.d("JavaInteropRuntime", "Loading @MAIN_ASSEMBLY_NAME@.so...");
        System.loadLibrary("@MAIN_ASSEMBLY_NAME@");
    }

    private JavaInteropRuntime() {
    }

    public static native void init();
}
