using System;
using System.Runtime.InteropServices;

namespace Java.Interop
{
	partial class JniEnvironment {

		static partial class Handles
		{
			public static void Dispose (ref JniObjectReference reference)
			{
				Dispose (ref reference, JniHandleOwnership.Transfer);
			}

			public static void Dispose (ref JniObjectReference reference, JniHandleOwnership transfer)
			{
				if (!reference.IsValid)
					return;

				switch (transfer) {
				case JniHandleOwnership.DoNotTransfer:
					break;
				case JniHandleOwnership.Transfer:
					switch (reference.Type) {
					case JniObjectReferenceType.Global:
						JniEnvironment.Current.JavaVM.JniHandleManager.DeleteGlobalReference (ref reference);
						break;
					case JniObjectReferenceType.Local:
						JniEnvironment.Current.JavaVM.JniHandleManager.DeleteLocalReference (JniEnvironment.Current, ref reference);
						break;
					case JniObjectReferenceType.WeakGlobal:
						JniEnvironment.Current.JavaVM.JniHandleManager.DeleteWeakGlobalReference (ref reference);
						break;
					default:
						throw new NotImplementedException ("Do not know how to dispose: " + reference.Type + ".");
					}
					reference.Invalidate ();
					break;
				default:
					throw new NotImplementedException ("Do not know how to transfer: " + transfer);
				}
			}

			public static int GetIdentityHashCode (JniObjectReference value)
			{
				return JniSystem.IdentityHashCode (value);
			}

			public static IntPtr NewReturnToJniRef (IJavaObject value)
			{
				if (value == null || !value.PeerReference.IsValid)
					return IntPtr.Zero;
				return NewReturnToJniRef (value.PeerReference);
			}

			public static IntPtr NewReturnToJniRef (JniObjectReference value)
			{
				if (!value.IsValid)
					return IntPtr.Zero;
				var l = value.NewLocalRef ();
				return JniEnvironment.Current.JavaVM.JniHandleManager.ReleaseLocalReference (JniEnvironment.Current, ref l);
			}
		}
	}
}

