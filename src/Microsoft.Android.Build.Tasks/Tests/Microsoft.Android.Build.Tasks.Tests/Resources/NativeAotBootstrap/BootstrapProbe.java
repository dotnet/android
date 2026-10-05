package net.dot.jni.nativeaot;

import java.nio.charset.StandardCharsets;
import java.util.Base64;

public class BootstrapProbe {
    private static native byte[] lookup(byte[] name);
    private static native void reinitialize();

    public static void main(String[] args) {
        System.load(args[0]);
        if (args.length > 2 && args[2].equals("mutate")) {
            NativeAotEnvironmentVars.systemProperties[1] = "changed after initialization";
            reinitialize();
        }
        byte[] result = lookup(args[1].getBytes(StandardCharsets.UTF_8));
        System.out.print(result == null ? "missing" : Base64.getEncoder().encodeToString(result));
    }
}
