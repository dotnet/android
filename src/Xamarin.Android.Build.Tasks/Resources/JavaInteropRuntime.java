package net.dot.jni.nativeaot;

import android.util.Log;
import android.content.Context;
import mono.NativeLibraryHelper;

public class JavaInteropRuntime {
    public static void loadLibrary(Context context) {
        Log.d("JavaInteropRuntime", "Loading @MAIN_ASSEMBLY_NAME@.so...");
        NativeLibraryHelper.loadLibrary("@MAIN_ASSEMBLY_NAME@", context);
    }

    private JavaInteropRuntime() {
    }

    public static native void init(ClassLoader classLoader, String language, String filesDir, String cacheDir);
}
