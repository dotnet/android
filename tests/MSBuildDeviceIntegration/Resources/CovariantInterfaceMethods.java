package example;

public interface CovariantInterfaceMethods {
    interface Base {
        Object getCovariantValue();
    }

    interface Derived extends Base {
        @Override
        default String getCovariantValue() {
            return "bridge";
        }
    }
}
