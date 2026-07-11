package net.dot.jni.referenceleaktests;

public final class ActivationProbeFactory
{
	public static Object create (Class<?> type) throws ReflectiveOperationException
	{
		return type.getDeclaredConstructor ().newInstance ();
	}
}
