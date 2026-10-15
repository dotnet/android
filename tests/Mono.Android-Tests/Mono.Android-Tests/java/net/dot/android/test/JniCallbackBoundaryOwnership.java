package net.dot.android.test;

public class JniCallbackBoundaryOwnership {
    public abstract static class Callback {
        public abstract int invoke();
    }

    public static int caught;
    public static int finished;
    public static int continued;

    public static int invoke(Callback callback, boolean swallow) {
        caught = finished = continued = 0;
        try {
            int result = callback.invoke();
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
