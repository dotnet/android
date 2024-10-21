package net.dot.jni.test;

/* package */ interface InternalInterface {
    String o();
}

public interface PublicInterface extends InternalInterface {
    String p();
}
