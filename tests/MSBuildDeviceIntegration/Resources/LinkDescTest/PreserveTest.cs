using System;
using System.Reflection;

public class PreserveTest
{
	// [Test]
	public static string MethodsArePreserved ()
	{
		try {
			// See src/monodroid/jni/timezones.cc for usage
			var androidEnvironment = Type.GetType ("Android.Runtime.AndroidEnvironment, Mono.Android", throwOnError: true);
			var notifyTimeZoneChanged = androidEnvironment.GetMethod ("NotifyTimeZoneChanged", BindingFlags.Static | BindingFlags.NonPublic);
			if (notifyTimeZoneChanged == null) {
				return $"[FAIL] {nameof (PreserveTest)}.{nameof (MethodsArePreserved)} FAILED: {nameof (notifyTimeZoneChanged)} is null)";
			}
			notifyTimeZoneChanged.Invoke (null, null);
			return $"[PASS] {nameof (PreserveTest)}.{nameof (MethodsArePreserved)}";
		} catch (Exception ex) {
			return $"[FAIL] {nameof (PreserveTest)}.{nameof (MethodsArePreserved)} FAILED: {ex}";
		}
	}
}
