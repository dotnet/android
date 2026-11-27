package example;

public interface InterfaceMethods {
    static int getStaticValue() {
        return 11;
    }

    default int getDefaultValue() {
        return 22;
    }

    interface Nested {
        static int getNestedStaticValue() {
            return 33;
        }

        default int getNestedDefaultValue() {
            return 44;
        }
    }
}
