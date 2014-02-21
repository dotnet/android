// This really should be auto-generated, but isn't for test purposes.
using System;

using Java.Interop;

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
					new JniNativeMethodRegistration ("getInt32Value", "()I", GetInt32ValueHandler ()),
					new JniNativeMethodRegistration ("getStringValue", "(I)Ljava/lang/String;", GetStringValueHandler ()));
		}

		public override JniPeerMembers JniPeerMembers {
			get {
				return _members;
			}
		}

		static Delegate GetInt32ValueHandler ()
		{
			Func<IntPtr, IntPtr, int> h = (jnienv, self) => {
				return 54;
			};
			return h;
		}

		static Delegate GetStringValueHandler ()
		{
			Func<IntPtr, IntPtr, int, IntPtr> h = (jnienv, self, value) => {
				return IntPtr.Zero;
			};
			return h;
		}

		public int GetInt32Value ()
		{
			return 42;
		}

		public JniLocalReference GetStringValue ()
		{
			return null;
		}
	}
}

