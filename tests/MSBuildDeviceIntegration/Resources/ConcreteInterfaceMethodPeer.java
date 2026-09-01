package example;

public final class ConcreteInterfaceMethodPeer implements InterfaceMethods {
    public ConcreteInterfaceMethodPeer() {
    }

    @Override
    public int getDefaultValue() {
        return InterfaceMethods.super.getDefaultValue() + 1;
    }
}
