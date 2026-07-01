package net.dot.jni.test;

public class AnotherJavaInterfaceImpl
	implements JavaInterface, Cloneable
{
	public String getValue() {
		return "Another hello from Java!";
	}

	public Object clone() {
		return this;
	}
}
