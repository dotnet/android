package com.xamarin.interop;

// When Android Desugaring is enabled -- the default when targeting API-25 and earlier --
// certain Java constructs result in Java bytecode rewriting.
// Interface static methods are *moved* into $-CC types.
public interface AndroidInterface {

	// When Desugaring is enabled, this is moved to `AndroidInterface$-CC.getClassName()`,
	// and the original `AndroidInterface.getClassName()` *no longer exists*.

	// public static String getClassName() {
	// 	return "AndroidInterface";
	// }
}
