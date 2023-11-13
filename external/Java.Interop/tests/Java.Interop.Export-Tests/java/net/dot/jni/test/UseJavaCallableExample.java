package net.dot.jni.test;

import net.dot.jni.test.JavaCallableExample;

public class UseJavaCallableExample {

	public static boolean test() {
		JavaCallableExample e = new JavaCallableExample(42);
		return e.getA() == 42;
	}
}
