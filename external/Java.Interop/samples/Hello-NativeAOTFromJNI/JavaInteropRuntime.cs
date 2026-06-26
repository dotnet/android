using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;

using Java.Interop;

namespace Hello_NativeAOTFromJNI;

static class JavaInteropRuntime
{
	static JniRuntime? runtime;

	[UnmanagedCallersOnly (EntryPoint="JNI_OnLoad")]
	static int JNI_OnLoad (IntPtr vm, IntPtr reserved)
	{
		return (int) JniVersion.v1_6;
	}

	[UnmanagedCallersOnly (EntryPoint="JNI_OnUnload")]
	static void JNI_OnUnload (IntPtr vm, IntPtr reserved)
	{
		runtime?.Dispose ();
		runtime = null;
	}

	[UnmanagedCallersOnly (EntryPoint="Java_net_dot_jni_hello_JavaInteropRuntime_init")]
	static void init (IntPtr jnienv, IntPtr klass)
	{
		if (runtime != null)
			return;

		try {
			runtime = new ExistingJniRuntime (jnienv);
		}
		catch (Exception e) {
			Console.Error.WriteLine ($"JavaInteropRuntime.init: error: {e}");
		}
	}

	sealed class ExistingJniRuntime : JniRuntime {

		public ExistingJniRuntime (IntPtr jnienv)
			: base (new CreationOptions {
				EnvironmentPointer      = jnienv,
				TypeManager             = new JniRuntime.JniTypeManager (),
				ObjectReferenceManager  = new ObjectReferenceManager (),
				ValueManager            = new ValueManager (),
			})
		{
		}

		public override string? GetCurrentManagedThreadName ()
		{
			return Thread.CurrentThread.Name;
		}

		public override string GetCurrentManagedThreadStackTrace (int skipFrames, bool fNeedFileInfo)
		{
			return new StackTrace (skipFrames, fNeedFileInfo).ToString ();
		}
	}

	sealed class ObjectReferenceManager : JniRuntime.JniObjectReferenceManager {
		public override int GlobalReferenceCount => 0;

		public override int WeakGlobalReferenceCount => 0;
	}

	sealed class ValueManager : JniRuntime.JniValueManager {
		public override void WaitForGCBridgeProcessing ()
		{
		}

		public override void CollectPeers ()
		{
		}

		public override void AddPeer (IJavaPeerable value)
		{
		}

		public override void RemovePeer (IJavaPeerable value)
		{
		}

		public override void FinalizePeer (IJavaPeerable value)
		{
		}

		public override List<JniSurfacedPeerInfo> GetSurfacedPeers ()
		{
			return new List<JniSurfacedPeerInfo> ();
		}

		public override IJavaPeerable? PeekPeer (JniObjectReference reference)
		{
			return null;
		}

		public override void ActivatePeer (
			JniObjectReference reference,
			Type type,
			ConstructorInfo cinfo,
			object?[]? argumentValues)
		{
			throw new NotSupportedException ();
		}

		protected override void ConstructPeerCore (IJavaPeerable peer, ref JniObjectReference reference, JniObjectReferenceOptions options)
		{
			throw new NotSupportedException ();
		}

		public override IJavaPeerable? CreatePeer (
			ref JniObjectReference reference,
			JniObjectReferenceOptions transfer,
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)] Type? targetType)
		{
			throw new NotSupportedException ();
		}

		[return: MaybeNull]
		protected override T CreateValueCore<[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)] T> (
			ref JniObjectReference reference,
			JniObjectReferenceOptions options,
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)] Type? targetType = null)
		{
			throw new NotSupportedException ();
		}

		protected override object? CreateValueCore (
			ref JniObjectReference reference,
			JniObjectReferenceOptions options,
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)] Type? targetType = null)
		{
			throw new NotSupportedException ();
		}

		[return: MaybeNull]
		protected override T GetValueCore<[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)] T> (
			ref JniObjectReference reference,
			JniObjectReferenceOptions options,
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)] Type? targetType = null)
		{
			throw new NotSupportedException ();
		}

		protected override object? GetValueCore (
			ref JniObjectReference reference,
			JniObjectReferenceOptions options,
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)] Type? targetType = null)
		{
			throw new NotSupportedException ();
		}

		protected override JniValueMarshaler GetValueMarshalerCore (Type type)
		{
			throw new NotSupportedException ();
		}

		protected override JniValueMarshaler<T> GetValueMarshalerCore<T> ()
		{
			throw new NotSupportedException ();
		}

		protected override JniObjectReference CreateLocalObjectReferenceArgumentCore (
			Type type,
			object? value)
		{
			throw new NotSupportedException ();
		}
	}
}
