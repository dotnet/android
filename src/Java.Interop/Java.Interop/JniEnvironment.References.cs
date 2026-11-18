#nullable enable

using System;
using System.Runtime.ExceptionServices;

namespace Java.Interop
{
	partial class JniEnvironment {

		/// <include file="../Documentation/Java.Interop/JniEnvironment.References.xml" path="/docs/member[@name='T:JniEnvironment.References']/*" />
		static partial class References
		{
			/// <include file="../Documentation/Java.Interop/JniEnvironment.References.xml" path="/docs/member[@name='M:CreatedReference']/*" />
			public static void CreatedReference (JniObjectReference value)
			{
				if (!value.IsValid)
					return;
				switch (value.Type) {
				case JniObjectReferenceType.Local:
					Runtime.ObjectReferenceManager.CreatedLocalReference (CurrentInfo, value);
					break;
				default:
					throw new ArgumentException ("Only JNI Local References are currently supported.", nameof (value));
				}
			}

			public static void GetJavaVM (out IntPtr invocationPointer)
			{
				int r   = _GetJavaVM (out invocationPointer);

				if (r != 0) {
					throw new InvalidOperationException (string.Format ("Could not get JavaVM; JNIEnv::GetJavaVM() returned {0}.", r));
				}
			}

			public static void EnsureLocalCapacity (int capacity)
			{
				int r   = _EnsureLocalCapacity (capacity);
				if (r == 0)
					return;

				var e = JniEnvironment.GetExceptionForLastThrowable ();
				if (e != null)
					ExceptionDispatchInfo.Capture (e).Throw ();

				throw new InvalidOperationException (string.Format ("Could not ensure capacity; JNIEnv::EnsureLocalCapacity() returned {0}.", r));
			}

			public static void PushLocalFrame (int capacity)
			{
				int r   = _PushLocalFrame (capacity);
				if (r == 0)
					return;

				var e = JniEnvironment.GetExceptionForLastThrowable ();
				if (e != null)
					ExceptionDispatchInfo.Capture (e).Throw ();

				throw new InvalidOperationException (string.Format ("Could not push a frame; JNIEnv::PushLocalFrame() returned {0}.", r));
			}

			public static int GetIdentityHashCode (JniObjectReference value)
			{
				return JniSystem.IdentityHashCode (value);
			}

			public static IntPtr NewReturnToJniRef (IJavaPeerable value)
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
				return JniEnvironment.Runtime.ObjectReferenceManager.ReleaseLocalReference (JniEnvironment.CurrentInfo, ref l);
			}
		}
	}
}

