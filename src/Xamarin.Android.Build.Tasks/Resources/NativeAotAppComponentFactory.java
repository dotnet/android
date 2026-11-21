package net.dot.jni.nativeaot;

import android.content.Context;
import android.content.pm.ApplicationInfo;
import net.dot.android.RuntimeAppComponentFactory;

public class NativeAotAppComponentFactory extends RuntimeAppComponentFactory {
    @Override
    protected void initializeRuntime(ApplicationInfo applicationInfo, ClassLoader loader, Context context) {
        JavaInteropRuntime.initialize(applicationInfo, loader, context);
    }
}
