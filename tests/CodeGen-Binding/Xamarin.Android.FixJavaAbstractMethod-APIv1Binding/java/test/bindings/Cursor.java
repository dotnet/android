package test.bindings;

public interface Cursor {
    void method();
    int methodWithRT ();
    int methodWithCursor (Cursor cursor);
    int methodWithParams (int number, String text);
    int methodWithParams (int number, String text, float real);
}
