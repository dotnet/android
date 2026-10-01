import java.nio.charset.StandardCharsets;
import java.util.Base64;

public class BootstrapProbe {
    private static native byte[] lookup(byte[] name);
    private static native void reinitialize();

    public static void main(String[] args) throws Exception {
        System.load(args[0]);
        if (args.length > 2 && args[2].equals("mutate")) {
            Class<?> config = Class.forName("net.dot.jni.nativeaot.NativeAotEnvironmentVars");
            var field = config.getDeclaredField("systemProperties");
            field.setAccessible(true);
            String[] properties = (String[])field.get(null);
            properties[1] = "changed after initialization";
            reinitialize();
        }
        byte[] result = lookup(args[1].getBytes(StandardCharsets.UTF_8));
        System.out.print(result == null ? "missing" : Base64.getEncoder().encodeToString(result));
    }
}
