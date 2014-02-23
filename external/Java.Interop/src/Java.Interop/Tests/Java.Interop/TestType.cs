// This really should be auto-generated, but isn't for test purposes.
using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[JniTypeInfo (TestType.JniTypeName)]
	public partial class TestType : JavaObject
	{
		internal    const    string         JniTypeName = "com/xamarin/interop/TestType";
		static      readonly JniPeerMembers _members    = new JniPeerMembers (JniTypeName, typeof (TestType));

		static TestType ()
		{
			_members.JniPeerType.RegisterNativeMethods (
					new JniNativeMethodRegistration ("equalsThis", "(Ljava/lang/Object;)Z", GetEqualsThisHandler ()),
					new JniNativeMethodRegistration ("getInt32Value", "()I", GetInt32ValueHandler ()),
					new JniNativeMethodRegistration ("getStringValue", "(I)Ljava/lang/String;", GetStringValueHandler ()));
		}

		public override JniPeerMembers JniPeerMembers {
			get {
				return _members;
			}
		}

		public TestType ()
		{
		}

		public TestType (JniReferenceSafeHandle handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		public void RunTests ()
		{
			_members.CallInstanceVoidMethod ("runTests", "()V", "runTests()V", this);
		}

		static Delegate GetEqualsThisHandler ()
		{
			Func<IntPtr, IntPtr, IntPtr, bool> h = (jnienv, n_self, n_value) => {
				JniEnvironment.CheckCurrent (jnienv);

				var jvm     = JniEnvironment.Current.JavaVM;
				var self    = jvm.GetObject<TestType>(n_self);
				var value   = jvm.GetObject (n_value);

				return self.EqualsThis (value);
			};
			return h;
		}

		static Delegate GetInt32ValueHandler ()
		{
			Func<IntPtr, IntPtr, int> h = (jnienv, self) => {
				JniEnvironment.CheckCurrent (jnienv);
				return JniEnvironment.Current.JavaVM.GetObject<TestType>(self).GetInt32Value ();
			};
			return h;
		}

		static Delegate GetStringValueHandler ()
		{
			Func<IntPtr, IntPtr, int, IntPtr> h = (jnienv, self, value) => {
				JniEnvironment.CheckCurrent (jnienv);
				var s = JniEnvironment.Current.JavaVM.GetObject<TestType>(self).GetStringValue (value);
				using (var r = JniEnvironment.Strings.NewString (s))
					return JniEnvironment.Handles.NewReturnToJniRef (r);
			};
			return h;
		}

		public bool EqualsThis (IJavaObject value)
		{
			Assert.IsNotNull (value);
			Assert.AreNotSame (this, value);
			Assert.IsTrue (JniEnvironment.Types.IsSameObject (SafeHandle, value.SafeHandle));
			return value != null && !object.ReferenceEquals (value, this) &&
				JniEnvironment.Types.IsSameObject (SafeHandle, value.SafeHandle);
		}

		public int GetInt32Value ()
		{
			return 42;
		}

		public string GetStringValue (int value)
		{
			return value.ToString ();
		}
	}
}

