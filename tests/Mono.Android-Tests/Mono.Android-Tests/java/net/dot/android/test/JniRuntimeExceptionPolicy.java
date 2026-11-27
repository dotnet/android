package net.dot.android.test;

public class JniRuntimeExceptionPolicy {
    public static int caught;
    public static int finished;
    public static int continued;

    private static native int callback();
    private static native void action();

    public static int invoke(boolean useAction, boolean swallow) {
        caught = finished = continued = 0;
        try {
            int result = 42;
            if (useAction) {
                action();
            } else {
                result = callback();
            }
            continued++;
            return result;
        } catch (Throwable exception) {
            caught++;
            if (!swallow) {
                throw exception;
            }
            return -1;
        } finally {
            finished++;
        }
    }
}
