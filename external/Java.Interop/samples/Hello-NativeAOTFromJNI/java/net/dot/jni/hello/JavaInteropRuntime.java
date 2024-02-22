package net.dot.jni.hello;

public class JavaInteropRuntime {
    static {
        System.loadLibrary("Hello-NativeAOTFromJNI");
    }

    private JavaInteropRuntime() {
    }

    public static native void init();
}
