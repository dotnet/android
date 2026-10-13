package net.dot.jni.nativeaot;

import java.nio.charset.StandardCharsets;
import java.util.Base64;

public class BootstrapProbe {
    private static native byte[] lookup(byte[] name);

    public static void main(String[] args) {
        System.load(args[0]);
        byte[] result = lookup(args[1].getBytes(StandardCharsets.UTF_8));
        System.out.print(result == null ? "missing" : Base64.getEncoder().encodeToString(result));
    }
}
