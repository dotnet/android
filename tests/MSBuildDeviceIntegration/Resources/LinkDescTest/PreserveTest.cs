using System;
using System.Reflection;

public class PreserveTest
{
	// [Test]
	public static string MethodsArePreserved ()
	{
		try {
			// See src/Microsoft.Android.Sdk.ILLink/PreserveLists/Mono.Android.xml
			var javaLangObject = Type.GetType ("Java.Lang.Object, Mono.Android", throwOnError: true);
			var setHandleOnDeserialized = javaLangObject.GetMethod ("SetHandleOnDeserialized", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			if (setHandleOnDeserialized == null) {
				return $"[FAIL] {nameof (PreserveTest)}.{nameof (MethodsArePreserved)} FAILED: {nameof (setHandleOnDeserialized)} is null)";
			}
			return $"[PASS] {nameof (PreserveTest)}.{nameof (MethodsArePreserved)}";
		} catch (Exception ex) {
			return $"[FAIL] {nameof (PreserveTest)}.{nameof (MethodsArePreserved)} FAILED: {ex}";
		}
	}
}
