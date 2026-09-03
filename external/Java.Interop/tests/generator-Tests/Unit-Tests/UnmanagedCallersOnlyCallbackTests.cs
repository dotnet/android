using System.Linq;
using NUnit.Framework;
using MonoDroid.Generation;
using Xamarin.Android.Binder;

namespace generatortests
{
	/// <summary>
	/// Tests for the experimental direct-<c>[UnmanagedCallersOnly]</c> binding callback shape
	/// enabled by <c>--lang-features=unmanaged-callers-only-callbacks</c>.
	/// </summary>
	[TestFixture]
	class UnmanagedCallersOnlyCallbackTests : CodeGeneratorTestBase
	{
		protected override CodeGenerationTarget Target => CodeGenerationTarget.XAJavaInterop1;

		protected override CodeGenerationOptions CreateOptions () => new CodeGenerationOptions {
			CodeGenerationTarget = Target,
			SupportNullableReferenceTypes = true,
			UseUnmanagedCallersOnlyCallbacks = true,
			UseGlobal = true,
		};

		const string Api = """
			<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.example' jni-name='com/example'>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' final='false' name='Peer' static='false' visibility='public' jni-signature='Lcom/example/Peer;' />
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' final='false' name='Widget' static='false' visibility='public' jni-signature='Lcom/example/Widget;'>
			      <method abstract='false' deprecated='not deprecated' final='false' name='onLayout' jni-signature='(ZIIII)V' return='void' static='false' visibility='public'>
			        <parameter name='changed' type='boolean' jni-type='Z' />
			        <parameter name='left' type='int' jni-type='I' />
			        <parameter name='top' type='int' jni-type='I' />
			        <parameter name='right' type='int' jni-type='I' />
			        <parameter name='bottom' type='int' jni-type='I' />
			      </method>
			      <method abstract='false' deprecated='not deprecated' final='false' name='attach' jni-signature='(Lcom/example/Peer;I)Lcom/example/Peer;' return='com.example.Peer' static='false' visibility='public'>
			        <parameter name='peer' type='com.example.Peer' jni-type='Lcom/example/Peer;' />
			        <parameter name='flags' type='int' jni-type='I' />
			      </method>
			      <method abstract='false' deprecated='not deprecated' final='false' name='describe' jni-signature='(Ljava/lang/String;)Ljava/lang/String;' return='java.lang.String' static='false' visibility='public'>
			        <parameter name='label' type='java.lang.String' jni-type='Ljava/lang/String;' />
			      </method>
			      <method abstract='false' deprecated='not deprecated' final='false' name='count' jni-signature='()I' return='int' static='false' visibility='public' />
			    </class>
			  </package>
			</api>
			""";

		string GenerateWidget ()
		{
			var gens = ParseApiDefinition (Api);
			return GetGeneratedTypeOutput (gens.Single (g => g.Name == "Widget"));
		}

		[Test]
		public void CallbacksAreUnmanagedCallersOnlyAndDropConnectors ()
		{
			var source = GenerateWidget ();

			Assert.True (source.Contains ("[global::System.Runtime.InteropServices.UnmanagedCallersOnly]"), source);

			// No `cb_*` delegate cache fields and no `Get*Handler ()` connector methods.
			Assert.False (source.Contains ("cb_onLayout"), source);
			Assert.False (source.Contains ("static Delegate GetOnLayout_ZIIIIHandler ()"), source);
			Assert.False (source.Contains ("static Delegate Get"), source);

			// The [Register] connector string is deliberately unchanged so that the native callback
			// name and any adapter/DIM declaring type remain recoverable from it.
			Assert.True (source.Contains ("GetOnLayout_ZIIIIHandler"), source);
		}

		[Test]
		public void AllScalarCallbackUsesTypedHelper ()
		{
			var source = GenerateWidget ();

			// boolean marshals as a blittable `sbyte`, so the shape is 5 scalars returning void.
			Assert.True (source.Contains (
				"global::Java.Interop.JniMarshalTyped.SafeInvokeMarshaled_SSSSSX<global::Com.Example.Widget, sbyte, int, int, int, int> (jnienv, native__this, native_changed, left, top, right, bottom, &__n_OnLayout_ZIIII);"),
				source);

			// The function-pointer target contains only the projection of the JNI scalars and the
			// managed member invocation -- no JNI transition, no peer lookup, no exception handling.
			Assert.True (source.Contains (
				"private static void __n_OnLayout_ZIIII (global::Com.Example.Widget __this, sbyte native_changed, int left, int top, int right, int bottom)"),
				source);
			Assert.True (source.Contains ("var changed = native_changed != 0;"), source);
			Assert.True (source.Contains ("__this.OnLayout (changed, left, top, right, bottom);"), source);
		}

		[Test]
		public void PeerArgumentAndPeerReturnUseTypedHelper ()
		{
			var source = GenerateWidget ();

			// The peer return is not a type argument: bound methods frequently have covariant
			// returns, so the target is typed as IJavaObject. That also keeps the MethodSpec smaller.
			Assert.True (source.Contains (
				"global::Java.Interop.JniMarshalTyped.SafeInvokeMarshaled_OSO<global::Com.Example.Widget, global::Com.Example.Peer, int> (jnienv, native__this, native_peer, flags, &__n_Attach_Lcom_example_Peer_I);"),
				source);

			// The helper owns both the argument peer lookup and the JNI conversion of the result,
			// so the target neither calls GetObject<T> () nor ToLocalJniHandle ().
			Assert.True (source.Contains (
				"private static global::Android.Runtime.IJavaObject? __n_Attach_Lcom_example_Peer_I (global::Com.Example.Widget __this, global::Com.Example.Peer? peer, int flags)"),
				source);
			Assert.True (source.Contains ("return __this.Attach (peer, flags);"), source);
			Assert.False (source.Contains ("__n_Attach_Lcom_example_Peer_I (IntPtr jnienv"), source);
		}

		[Test]
		public void StringShapesFallBackToRawSafeInvoke ()
		{
			var source = GenerateWidget ();

			// Strings need JNI string marshaling the shared typed helper cannot own, so the callback
			// stays [UnmanagedCallersOnly] but forwards to the raw SafeInvoke helper.
			Assert.True (source.Contains (
				"global::Java.Interop.JniMarshal.SafeInvokeFunc (jnienv, native__this, native_label, &__n_Describe_Ljava_lang_String_);"),
				source);
			Assert.True (source.Contains (
				"private static IntPtr __n_Describe_Ljava_lang_String_ (IntPtr jnienv, IntPtr native__this, IntPtr native_label)"),
				source);
			Assert.True (source.Contains ("global::Java.Lang.Object.GetObject<global::Com.Example.Widget> (jnienv, native__this, JniHandleOwnership.DoNotTransfer)"), source);
		}

		[Test]
		public void ScalarReturnUsesTypedHelper ()
		{
			var source = GenerateWidget ();

			Assert.True (source.Contains (
				"global::Java.Interop.JniMarshalTyped.SafeInvokeMarshaled_S<global::Com.Example.Widget, int> (jnienv, native__this, &__n_Count);"),
				source);
			Assert.True (source.Contains ("private static int __n_Count (global::Com.Example.Widget __this)"), source);
		}

		[Test]
		public void DefaultsAreUnchangedWhenFlagIsOff ()
		{
			options = new CodeGenerationOptions {
				CodeGenerationTarget = Target,
				SupportNullableReferenceTypes = true,
				UseGlobal = true,
			};
			generator = options.CreateCodeGenerator (writer);

			var source = GenerateWidget ();

			Assert.False (source.Contains ("UnmanagedCallersOnly"), source);
			Assert.False (source.Contains ("JniMarshalTyped"), source);
			Assert.True (source.Contains ("cb_onLayout"), source);
			Assert.True (source.Contains ("static Delegate GetOnLayout_ZIIIIHandler ()"), source);
		}
	}
}
