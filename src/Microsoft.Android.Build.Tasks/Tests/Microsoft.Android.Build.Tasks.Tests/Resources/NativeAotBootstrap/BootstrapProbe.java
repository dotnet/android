package net.dot.jni.nativeaot;

import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Base64;

public class BootstrapProbe {
    private static native byte[] lookup(byte[] name);
    private static native void initialize(byte[] body);

    public static void main(String[] args) throws Exception {
        System.load(args[0]);
        initialize(Files.readAllBytes(Path.of(args[2])));
        byte[] result = lookup(args[1].getBytes(StandardCharsets.UTF_8));
        System.out.print(result == null ? "missing" : Base64.getEncoder().encodeToString(result));
    }
}
