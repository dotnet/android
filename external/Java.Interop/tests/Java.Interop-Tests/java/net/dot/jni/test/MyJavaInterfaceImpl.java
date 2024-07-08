package net.dot.jni.test;

public class MyJavaInterfaceImpl
	implements JavaInterface, Cloneable
{
	public String getValue() {
		return "Hello from Java!";
	}

	public Object clone() {
		return this;
	}
}
