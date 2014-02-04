using System;
using System.Runtime.InteropServices;

namespace Java.Interop
{
	partial class JniEnvironment {

		static partial class Handles
		{
			internal static void Dispose (JniReferenceSafeHandle value, JniHandleOwnership transfer)
			{
				switch (transfer) {
				case JniHandleOwnership.DoNotTransfer:
					break;
				case JniHandleOwnership.Transfer:
					value.Dispose ();
					break;
				default:
					throw new NotImplementedException ("Do not know how to transfer: " + transfer);
				}
			}

			public static IntPtr NewReturnToJniRef (IJavaObject value)
			{
				if (value == null)
					return IntPtr.Zero;
				return NewReturnToJniRef (value.SafeHandle);
			}

			public static IntPtr NewReturnToJniRef (JniReferenceSafeHandle value)
			{
				if (value == null || value.IsInvalid)
					return IntPtr.Zero;
				return _NewReturnToJniRef (JniEnvironment.Current.SafeHandle, value);
			}

			static JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr __NewReturnToJniRef;
			static JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr _NewReturnToJniRef {
				get {
					if (__NewReturnToJniRef == null)
						__NewReturnToJniRef = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr) Marshal.GetDelegateForFunctionPointer (JniEnvironment.Current.Invoker.env.NewLocalRef, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr));
					return __NewReturnToJniRef;
				}
			}
		}
	}
}

