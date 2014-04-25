package com.xamarin.android;

public /* static */ class ManagedPeer {
	private ManagedPeer () {
	}

	// public static native void registerNativeMethods (java.lang.Class<?> nativeClass, String managedType, String methods);

	public static void runConstructor (
			Class<?> declaringClass,
			Object self,
			String assemblyQualifiedName,
			String constructorSignature,
			Object... arguments) {
		if (self.getClass() != declaringClass)
			return;
		runConstructor (self, assemblyQualifiedName, constructorSignature, arguments);
	}

	static native void runConstructor (
			Object self,
			String assemblyQualifiedName,
			String constructorSignature,
			Object... arguments
	);
}
