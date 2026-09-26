package net.dot.jni.test;

public class FieldRemapDerived extends FieldRemapBase
{
	public boolean hiddenInstanceField;
	public static String hiddenStaticField = "derived";
	public int hiddenInstanceMethod () { return 12; }
	public static int hiddenStaticMethod () { return 22; }
}
