package example;

public final class InterfaceMethodBridgeInvoker {
    private InterfaceMethodBridgeInvoker() {
    }

    private static final class CovariantPeer implements CovariantInterfaceMethods.Derived {
    }

    public static String invokeCovariantBridge() {
        CovariantInterfaceMethods.Base peer = new CovariantPeer();
        return (String) peer.getCovariantValue() + ":" + invokeStaticMethods();
    }

    // Keep Java call sites so the R8 retention gap tracked by dotnet/android#11774
    // does not mask the JNI runtime behavior covered here.
    public static int invokeStaticMethods() {
        return InterfaceMethods.getStaticValue()
                + InterfaceMethods.Nested.getNestedStaticValue();
    }
}
