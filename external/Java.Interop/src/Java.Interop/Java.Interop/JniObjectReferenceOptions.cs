#nullable enable

using System;

namespace Java.Interop
{
	[Flags]
	public enum JniObjectReferenceOptions
	{
		None                        = 0,
		Copy                        = 1 << 0,                   // DoNotTransfer
		// DisposeSource            = 1 << 1,                   // See JniObjectReference.DisposeSource
		CopyAndDispose              = (1 << 1) + Copy,          // Transfer
		// DoNotRegisterTarget      = 1 << 2,                   // See JniRuntime.JniValueManager.DoNotRegisterTarget
		CopyAndDoNotRegister        = (1 << 2) + Copy,
	}

	partial struct JniObjectReference {
		const       JniObjectReferenceOptions   DisposeSource       = (JniObjectReferenceOptions) (1 << 1);
	}

	partial class JniRuntime {
		partial class JniValueManager {
			const   JniObjectReferenceOptions   DoNotRegisterTarget = (JniObjectReferenceOptions) (1 << 2);
		}
	}
}
