package mono.android;

public class TypeManager {

	public static void Activate (String typeName, String sig, Object instance, Object[] parameterList)
	{
		n_activate (typeName, sig, instance, parameterList);
	}

	private static native void n_activate (String typeName, String sig, Object instance, Object[] parameterList);

	static {
		String methods = 
			"n_activate:(Ljava/lang/String;Ljava/lang/String;Ljava/lang/Object;[Ljava/lang/Object;)V:GetActivateHandler\n" +
			"";
		mono.android.Runtime.register ("Java.Interop.TypeManager+JavaTypeManager, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null", TypeManager.class, methods);
	}
}
