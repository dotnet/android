package net.dot.jni.test;

public class ObjectHelper {
	private ObjectHelper()
	{
	}

	public static int getHashCodeHelper (Object o) {
		return o.hashCode();
	}
}
