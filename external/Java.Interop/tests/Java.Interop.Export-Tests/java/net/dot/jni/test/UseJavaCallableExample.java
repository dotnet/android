package net.dot.jni.test;

import net.dot.jni.test.JavaCallableExample;

public class UseJavaCallableExample {

	public static boolean test() {
		JavaCallableExample e = new JavaCallableExample(new int[]{1,2}, new int[]{3, 4});
		return e.getFirstA() == 1;
	}
}
