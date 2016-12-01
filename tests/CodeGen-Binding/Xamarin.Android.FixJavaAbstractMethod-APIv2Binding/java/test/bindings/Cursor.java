package test.bindings;

public interface Cursor {
    void method ();
    void newMethod();
    int methodWithRT ();
    int newMethodWithRT ();
    int methodWithParams (int number, String text);
    int newMethodWithParams (int number, String text);
    int methodWithParams (int number, String text, float real);
    int newMethodWithParams (int number, String text, float real);
}
