package com.xamarin.java_interop;

public /* static */ final class ManagedPeer {
	private ManagedPeer () {
	}

	public static native void registerNativeMembers (
			java.lang.Class<?> nativeClass,
			String assemblyQualifiedName,
			String methods);

	public static native void construct (
			Object self,
			String assemblyQualifiedName,
			String constructorSignature,
			Object... arguments
	);
}
