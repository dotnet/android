#if INSIDE_MONO_ANDROID_RUNTIME
using System;
using System.Reflection;

namespace Android.Runtime
{
	// The existence of InternalRuntimeTypeHolder and DotNetRuntimeTypeConverter classes looks weird, but
	// we must handle a weird situation.  AndroidRuntimeInternal needs to know in its static constructor
	// what is the current runtime type, but it cannot query JNIEnvInit.RuntimeType, since that type lives
	// in the Mono.Android assembly, while AndroidRuntimeInternal lives in Mono.Android.Runtime and it cannot
	// access JNIEnvInit and Mono.Android.Runtime doesn't reference Mono.Android but Mono.Android **does** reference
	// Mono.Android.Runtime and has access to its internals.
	//
	// Mono.Android.Runtime, also, includes several source files from Mono.Android - **both** assemblies
	// include the same source files.  In case of the DotNetRuntimeType enum, this declares two distinct types - one
	// in Mono.Android and another in Mono.Android.Runtime, and so if JNIEnvInit.Initialize were to try to set the
	// `DotNetRuntimeType RuntimeType;` field/property in either of the classes below, we'd get a compilation error
	// to the effect of it being unable to cast `Android.Runtime.DotNetRuntimeType` to `Android.Runtime.DotNetRuntimeType`,
	// which is usually as clear as mud :)
	//
	// To solve this and not duplicate code, the InternalRuntimeTypeHolder class is introduced which acts as a proxy since
	// the AndroidRuntimeInternal static constructor must know the runtime type and JNIEnvInit.Initialize takes care of it by
	// calling `SetRuntimeType` below long before AndroidRuntimeInternal cctor is invoked.
	public static class InternalRuntimeTypeHolder
	{
		internal static DotNetRuntimeType RuntimeType = DotNetRuntimeType.Unknown;

		internal static void SetRuntimeType (uint runtimeType)
		{
			RuntimeType = DotNetRuntimeTypeConverter.Convert (runtimeType);
		}
	}

	public static class AndroidRuntimeInternal
	{
		internal static readonly Action<Exception> mono_unhandled_exception;

#pragma warning disable CS0649 // Field is never assigned to.  This field is assigned from monodroid-glue.cc.
		internal static volatile bool BridgeProcessing; // = false
#pragma warning restore CS0649 // Field is never assigned to.

		static AndroidRuntimeInternal ()
		{
			mono_unhandled_exception = InternalRuntimeTypeHolder.RuntimeType switch {
				DotNetRuntimeType.MonoVM  => MonoUnhandledException,
				DotNetRuntimeType.CoreCLR => CoreClrUnhandledException,
				_                         => throw new NotSupportedException ($"Internal error: runtime type {InternalRuntimeTypeHolder.RuntimeType} not supported")
			};
		}

		static void CoreClrUnhandledException (Exception ex)
		{
			// TODO: Is this even needed on CoreCLR?
		}

		// Needed when running under CoreCLR, which doesn't allow icalls/ecalls.  Any method which contains any reference to
		// an unregistered icall/ecall method will fail to JIT (even if the method isn't actually called).  In this instance
		// it affected the static constructor which tried to assign `RuntimeNativeMethods.monodroid_debugger_unhandled_exception`
		// to `mono_unhandled_exception` at the top of the class.
		static void MonoUnhandledException (Exception ex)
		{
			RuntimeNativeMethods.monodroid_debugger_unhandled_exception (ex);
		}

		public static void WaitForBridgeProcessing ()
		{
			if (!BridgeProcessing)
				return;
			RuntimeNativeMethods._monodroid_gc_wait_for_bridge_processing ();
		}
	}
}
#endif // INSIDE_MONO_ANDROID_RUNTIME
