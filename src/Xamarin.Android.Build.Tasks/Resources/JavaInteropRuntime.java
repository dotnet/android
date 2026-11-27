package net.dot.jni.nativeaot;

import java.time.OffsetDateTime;
import java.util.Calendar;
import java.util.Locale;
import android.content.Context;
import android.content.pm.ApplicationInfo;
import android.util.Log;
import net.dot.android.NativeLibraryHelper;
import net.dot.android.ApplicationRegistration;
import net.dot.android.RuntimeDirectories;

public class JavaInteropRuntime {
    private static final Object lock = new Object();
    private static boolean initialized;

    public static void loadLibrary(ApplicationInfo applicationInfo) {
        Log.d("JavaInteropRuntime", "Loading @MAIN_ASSEMBLY_NAME@.so...");
        NativeLibraryHelper.loadLibrary("@MAIN_ASSEMBLY_NAME@", applicationInfo);
    }

    // Called by both NativeAotAppComponentFactory and NativeAotRuntimeProvider; only the first call initializes.
    public static void initialize(ApplicationInfo applicationInfo, ClassLoader loader, Context context) {
        synchronized (lock) {
            if (context instanceof android.app.Application) {
                ApplicationRegistration.Context = context;
            }
            if (initialized) {
                return;
            }

            Locale locale = Locale.getDefault();
            String language = locale.getLanguage() + "-" + locale.getCountry();
            int localDateTimeOffset;
            if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.O) {
                localDateTimeOffset = OffsetDateTime.now().getOffset().getTotalSeconds();
            } else {
                Calendar calendar = Calendar.getInstance();
                localDateTimeOffset = (calendar.get(Calendar.ZONE_OFFSET) + calendar.get(Calendar.DST_OFFSET)) / 1000;
            }

            loadLibrary(applicationInfo);
            init(
                loader,
                language,
                RuntimeDirectories.getFilesDir(applicationInfo, context),
                RuntimeDirectories.getCacheDir(applicationInfo, context),
                localDateTimeOffset);
            // NOTE: only required for custom applications
            ApplicationRegistration.registerApplications();
            initialized = true;
        }
    }

    private JavaInteropRuntime() {
    }

    public static native void init(ClassLoader classLoader, String language, String filesDir, String cacheDir, int localDateTimeOffset);
}
