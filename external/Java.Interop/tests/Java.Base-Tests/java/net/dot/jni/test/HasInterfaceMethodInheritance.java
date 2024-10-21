package net.dot.jni.test;

public class HasInterfaceMethodInheritance implements InterfaceMethodInheritance {
    private HasInterfaceMethodInheritance() {
    }

    public static InterfaceMethodInheritance create() {
        return new HasInterfaceMethodInheritance();
    }

    public String m() {
        return "HasInterfaceMethodInheritance.m";
    }

    public String n() {
        return "HasInterfaceMethodInheritance.n";
    }

    public String o() {
        return "HasInterfaceMethodInheritance.o";
    }

    public String p() {
        return "HasInterfaceMethodInheritance.p";
    }
}
