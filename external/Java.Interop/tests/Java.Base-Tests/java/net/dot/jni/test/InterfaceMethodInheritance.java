package net.dot.jni.test;

/* package */ interface BaseInterface {
    String m();
}

public interface InterfaceMethodInheritance extends BaseInterface, PublicInterface {
    String n();
}
