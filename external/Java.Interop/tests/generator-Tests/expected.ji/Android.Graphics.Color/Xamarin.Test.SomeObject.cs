using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Xamarin.Test {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']"
	[global::Android.Runtime.Register ("xamarin/test/SomeObject", DoNotGenerateAcw=true)]
	public abstract partial class SomeObject : global::Java.Lang.Object {



		// Metadata.xml XPath field reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/field[@name='backColor']"
		[Register ("backColor")]
		public global::Android.Graphics.Color BackColor {
			get {
				const string __id = "backColor.I";

				var __v = _members.InstanceFields.GetInt32Value (__id, this);
				return new global::Android.Graphics.Color (__v);
			}
			set {
				const string __id = "backColor.I";

				try {
					_members.InstanceFields.SetValue (__id, this, value.ToArgb ());
				} finally {
				}
			}
		}
		static readonly JniPeerMembers _members = new JniPeerMembers ("xamarin/test/SomeObject", typeof (SomeObject));
		internal static new IntPtr class_ref {
			get {
				return _members.JniPeerType.PeerReference.Handle;
			}
		}

		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		protected override IntPtr ThresholdClass {
			get { return _members.JniPeerType.PeerReference.Handle; }
		}

		protected override global::System.Type ThresholdType {
			get { return _members.ManagedPeerType; }
		}

		protected SomeObject (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		static Delegate cb_getSomeColor;
#pragma warning disable 0169
		static Delegate GetGetSomeColorHandler ()
		{
			if (cb_getSomeColor == null)
				cb_getSomeColor = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, int>) n_GetSomeColor);
			return cb_getSomeColor;
		}

		static int n_GetSomeColor (IntPtr jnienv, IntPtr native__this)
		{
			global::Xamarin.Test.SomeObject __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return __this.SomeColor.ToArgb ();
		}
#pragma warning restore 0169

		static Delegate cb_setSomeColor_I;
#pragma warning disable 0169
		static Delegate GetSetSomeColor_IHandler ()
		{
			if (cb_setSomeColor_I == null)
				cb_setSomeColor_I = JNINativeWrapper.CreateDelegate ((Action<IntPtr, IntPtr, int>) n_SetSomeColor_I);
			return cb_setSomeColor_I;
		}

		static void n_SetSomeColor_I (IntPtr jnienv, IntPtr native__this, int native_newvalue)
		{
			global::Xamarin.Test.SomeObject __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			global::Android.Graphics.Color newvalue = new global::Android.Graphics.Color (native_newvalue);
			__this.SomeColor = newvalue;
		}
#pragma warning restore 0169

		public abstract global::Android.Graphics.Color SomeColor {
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='getSomeColor' and count(parameter)=0]"
			[Register ("getSomeColor", "()I", "GetGetSomeColorHandler")] get;
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='setSomeColor' and count(parameter)=1 and parameter[1][@type='Android.Graphics.Color']]"
			[Register ("setSomeColor", "(I)V", "GetSetSomeColor_IHandler")] set;
		}

	}

	[global::Android.Runtime.Register ("xamarin/test/SomeObject", DoNotGenerateAcw=true)]
	internal partial class SomeObjectInvoker : SomeObject {

		public SomeObjectInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) {}

		static readonly JniPeerMembers _members = new JniPeerMembers ("xamarin/test/SomeObject", typeof (SomeObjectInvoker));

		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		protected override global::System.Type ThresholdType {
			get { return _members.ManagedPeerType; }
		}

		public override unsafe global::Android.Graphics.Color SomeColor {
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='getSomeColor' and count(parameter)=0]"
			[Register ("getSomeColor", "()I", "GetGetSomeColorHandler")]
			get {
				const string __id = "getSomeColor.()I";
				try {
					var __rm = _members.InstanceMethods.InvokeAbstractInt32Method (__id, this, null);
					return new global::Android.Graphics.Color (__rm);
				} finally {
				}
			}
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='setSomeColor' and count(parameter)=1 and parameter[1][@type='Android.Graphics.Color']]"
			[Register ("setSomeColor", "(I)V", "GetSetSomeColor_IHandler")]
			set {
				const string __id = "setSomeColor.(I)V";
				try {
					JniArgumentValue* __args = stackalloc JniArgumentValue [1];
					__args [0] = new JniArgumentValue (value.ToArgb ());
					_members.InstanceMethods.InvokeAbstractVoidMethod (__id, this, __args);
				} finally {
				}
			}
		}

	}

}
