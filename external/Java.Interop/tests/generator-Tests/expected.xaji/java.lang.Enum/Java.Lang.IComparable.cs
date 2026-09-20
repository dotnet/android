// Java allows a type to redeclare a member that a base type or base interface already
// declares, including members that are hand-bound instead of generated.
#pragma warning disable 0108, 0114
// Java types may declare a `finalize()` method, which is bound as `Finalize()`.
#pragma warning disable 0465
// Java deprecates types and members independently of the APIs that use, declare or
// override them, so a binding that is not deprecated can still reference one that is.
#pragma warning disable 0618, 0672, 0809
// Java nullness annotations are not required to agree between a member and the member
// it overrides or implements, and are absent from much of the API surface.
#pragma warning disable 8603, 8604, 8625, 8764, 8765, 8766, 8767, 8768

using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Java.Lang {

	// Metadata.xml XPath interface reference: path="/api/package[@name='java.lang']/interface[@name='Comparable']"
	[Register ("java/lang/Comparable", "", "Java.Lang.IComparableInvoker")]
	[global::Java.Interop.JavaTypeParameters (new string [] {"T"})]
	public partial interface IComparable : IJavaObject, IJavaPeerable {
		// Metadata.xml XPath method reference: path="/api/package[@name='java.lang']/interface[@name='Comparable']/method[@name='compareTo' and count(parameter)=1 and parameter[1][@type='T']]"
		[Register ("compareTo", "(Ljava/lang/Object;)I", "GetCompareTo_Ljava_lang_Object_Handler:Java.Lang.IComparableInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")]
		int CompareTo (global::Java.Lang.Object another);

	}

	[global::Android.Runtime.Register ("java/lang/Comparable", DoNotGenerateAcw=true)]
	internal partial class IComparableInvoker : global::Java.Lang.Object, IComparable {
		static IntPtr java_class_ref {
			get { return _members_java_lang_Comparable.JniPeerType.PeerReference.Handle; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members_java_lang_Comparable; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		protected override IntPtr ThresholdClass {
			get { return _members_java_lang_Comparable.JniPeerType.PeerReference.Handle; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		protected override global::System.Type ThresholdType {
			get { return _members_java_lang_Comparable.ManagedPeerType; }
		}

		static readonly JniPeerMembers _members_java_lang_Comparable = new XAPeerMembers ("java/lang/Comparable", typeof (IComparableInvoker));

		public IComparableInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
		{
		}

		static Delegate cb_compareTo_CompareTo_Ljava_lang_Object__I;
#pragma warning disable 0169
		static Delegate GetCompareTo_Ljava_lang_Object_Handler ()
		{
			return cb_compareTo_CompareTo_Ljava_lang_Object__I ??= new _JniMarshal_PPL_I (n_CompareTo_Ljava_lang_Object_);
		}

		static int n_CompareTo_Ljava_lang_Object_ (IntPtr jnienv, IntPtr native__this, IntPtr native_another)
		{
			unsafe {
				return global::Java.Interop.JniMarshal.SafeInvokeFunc (jnienv, native__this, native_another, &__n_CompareTo_Ljava_lang_Object_);
			}
		}
		private static int __n_CompareTo_Ljava_lang_Object_ (IntPtr jnienv, IntPtr native__this, IntPtr native_another)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Java.Lang.IComparable> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			var another = global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (native_another, JniHandleOwnership.DoNotTransfer);
			int __ret = __this.CompareTo (another);
			return __ret;
		}
#pragma warning restore 0169

		public unsafe int CompareTo (global::Java.Lang.Object another)
		{
			const string __id = "compareTo.(Ljava/lang/Object;)I";
			IntPtr native_another = JNIEnv.ToLocalJniHandle (another);
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (native_another);
				var __rm = _members_java_lang_Comparable.InstanceMethods.InvokeAbstractInt32Method (__id, this, __args);
				return __rm;
			} finally {
				JNIEnv.DeleteLocalRef (native_another);
				global::System.GC.KeepAlive (another);
			}
		}

	}
}
