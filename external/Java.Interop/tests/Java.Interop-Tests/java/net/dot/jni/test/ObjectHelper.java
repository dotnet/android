package net.dot.jni.test;

public class ObjectHelper {
	public static String remappedInheritedStaticField = "inherited target";

	private ObjectHelper()
	{
	}

	public static int getHashCodeHelper (Object o) {
		return o.hashCode();
	}

	public static int remappedInheritedStaticMethod () {
		return 23;
	}
}
