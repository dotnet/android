using System;
using System.Diagnostics.CodeAnalysis;
using Android.Runtime;
using Java.Interop;

namespace Microsoft.Android.Runtime;

static class JavaMarshalValueManagerHelper
{
	const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

	[return: DynamicallyAccessedMembers (Constructors)]
	public static Type? ResolvePeerType ([DynamicallyAccessedMembers (Constructors)] Type? type)
	{
		if (type is null) {
			return null;
		}
		if (type == typeof (object) || type == typeof (IJavaPeerable)) {
			return typeof (global::Java.Interop.JavaObject);
		}
		if (type == typeof (Exception)) {
			return typeof (JavaException);
		}
		return type;
	}

	/// <summary>
	/// Returns true when <paramref name="targetType"/>'s Java class is not assignable from
	/// <paramref name="reference"/>. Throws when <paramref name="targetType"/> has no usable mapping.
	/// </summary>
	public static bool IsIncompatibleCast (
		string targetJniName,
		ref JniObjectReference reference,
		Type targetType)
	{
		var instanceClass = JniEnvironment.Types.GetObjectClass (reference);
		JniObjectReference targetClass = default;
		try {
			targetClass = JniEnvironment.Types.FindClass (targetJniName);

			if (!JniEnvironment.Types.IsAssignableFrom (instanceClass, targetClass)) {
				// Match the legacy cast diagnostic when assembly logging is enabled.
				if (Logger.LogAssembly) {
					var targetSig = JniRuntime.CurrentRuntime.TypeManager.GetTypeSignature (targetType);
					var message = $"Handle 0x{reference.Handle:x} is of type '{JNIEnv.GetClassNameFromInstance (reference.Handle)}' which is not assignable to '{targetSig.SimpleReference}'";
					Logger.Log (LogLevel.Debug, "monodroid-assembly", message);
				}

				if (RuntimeFeature.IsAssignableFromCheck) {
					return true;
				}
			}
		} catch (Java.Lang.ClassNotFoundException e) {
			throw new ArgumentException (
				$"Could not find Java class '{targetJniName}'.",
				nameof (targetType), e);
		} finally {
			JniObjectReference.Dispose (ref instanceClass);
			JniObjectReference.Dispose (ref targetClass);
		}

		// Compatible classes mean a proxy/activation gap.
		return false;
	}
}
