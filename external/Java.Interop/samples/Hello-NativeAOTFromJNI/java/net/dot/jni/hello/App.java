package net.dot.jni.hello;

class App {

    public static void main(String[] args) {
        System.out.println("Hello from Java!");
        JavaInteropRuntime.init();
        String s = sayHello();
        System.out.println("String returned to Java: " + s);
    }

    static native String sayHello();
}
