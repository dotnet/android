// This really should be auto-generated, but isn't for test purposes.
using System;

using Java.Interop;
using Java.Interop.GenericMarshaler;

using NUnit.Framework;

namespace Java.InteropTests
{
	[JniTypeSignature (TestType.JniTypeName)]
	public partial class TestType : JavaObject
	{
		internal    const    string         JniTypeName = "com/xamarin/interop/TestType";
		static      readonly JniPeerMembers _members    = new JniPeerMembers (JniTypeName, typeof (TestType));

		static TestType ()
		{
			_members.JniPeerType.RegisterNativeMethods (
					new JniNativeMethodRegistration ("equalsThis", "(Ljava/lang/Object;)Z", GetEqualsThisHandler ()),
					new JniNativeMethodRegistration ("getInt32Value", "()I", GetInt32ValueHandler ()),
					new JniNativeMethodRegistration ("getStringValue", "(I)Ljava/lang/String;", _GetStringValueHandler ()),
					new JniNativeMethodRegistration ("methodThrows", "()V", GetMethodThrowsHandler ()));
		}

		public override JniPeerMembers JniPeerMembers {
			get {
				return _members;
			}
		}

		public  bool    ExecutedDefaultConstructor;

		public TestType ()
		{
			ExecutedDefaultConstructor  = true;
		}

		public  bool    ExecutedActivationConstructor;

		public TestType (ref JniObjectReference reference, JniObjectReferenceOptions transfer)
			: base (ref reference, transfer)
		{
			ExecutedActivationConstructor   = true;
		}

		public static unsafe TestType NewTestType ()
		{
			var o   = _members.StaticMethods.InvokeObjectMethod ("newTestType.()Lcom/xamarin/interop/TestType;", null);
			return JniEnvironment.Runtime.ValueManager.GetValue<TestType> (ref o, JniObjectReferenceOptions.CopyAndDispose);
		}

		public static unsafe TestType NewTestTypeWithUnboundConstructor ()
		{
			const string id = "newTestTypeWithUnboundConstructor.()Lcom/xamarin/interop/TestType;";
			var o   = _members.StaticMethods.InvokeObjectMethod (id, null);
			return JniEnvironment.Runtime.ValueManager.GetValue<TestType> (ref o, JniObjectReferenceOptions.CopyAndDispose);
		}

		public unsafe void RunTests ()
		{
			_members.InstanceMethods.InvokeVirtualVoidMethod ("runTests.()V", this, null);
		}

		public int UpdateInt32Array (int[] value)
		{
			return _members.InstanceMethods.InvokeGenericVirtualInt32Method ("updateInt32Array.([I)I", this, value);
		}

		public int UpdateInt32ArrayArray (int[][] value)
		{
			return _members.InstanceMethods.InvokeGenericVirtualInt32Method ("updateInt32ArrayArray.([[I)I", this, value);
		}

		public int UpdateInt32ArrayArrayArray (int[][][] value)
		{
			return _members.InstanceMethods.InvokeGenericVirtualInt32Method ("updateInt32ArrayArrayArray.([[[I)I", this, value);
		}

		public int Identity (int value)
		{
			return _members.InstanceMethods.InvokeGenericVirtualInt32Method ("identity.(I)I", this, value);
		}

		public static int StaticIdentity (int value)
		{
			return _members.StaticMethods.InvokeGenericInt32Method ("staticIdentity.(I)I", value);
		}

		public void MethodThrows ()
		{
			throw new InvalidOperationException ("jonp: bye!");
		}

		public unsafe void PropogateException ()
		{
			_members.InstanceMethods.InvokeVirtualVoidMethod ("propogateException.()V", this, null);
		}

		public bool PropogateFinallyBlockExecuted {
			get {return _members.InstanceFields.GetBooleanValue ("propogateFinallyBlockExecuted.Z", this);}
		}

		static Delegate GetEqualsThisHandler ()
		{
			Func<IntPtr, IntPtr, IntPtr, bool> h = (jnienv, n_self, n_value) => {
				var jvm     = JniEnvironment.Runtime;
				var r_self  = new JniObjectReference (n_self);
				var self    = jvm.ValueManager.GetValue<TestType>(ref r_self, JniObjectReferenceOptions.CopyAndDoNotRegister);
				var r_value = new JniObjectReference (n_self);
				var value   = jvm.ValueManager.GetValue<IJavaPeerable> (ref r_value, JniObjectReferenceOptions.CopyAndDoNotRegister);

				try {
					return self.EqualsThis (value);
				} finally {
					self.DisposeUnlessReferenced ();
					value.DisposeUnlessReferenced ();
				}
			};
			return JniEnvironment.Runtime.ExportedMemberBuilder.CreateMarshalToManagedDelegate (h);
		}

		static Delegate GetInt32ValueHandler ()
		{
			Func<IntPtr, IntPtr, int> h = (jnienv, n_self) => {
				var r_self  = new JniObjectReference (n_self);
				var self    = JniEnvironment.Runtime.ValueManager.GetValue<TestType>(ref r_self, JniObjectReferenceOptions.CopyAndDoNotRegister);
				try {
					return self.GetInt32Value ();
				} finally {
					self.DisposeUnlessReferenced ();
				}
			};
			return JniEnvironment.Runtime.ExportedMemberBuilder.CreateMarshalToManagedDelegate (h);
		}

		static Delegate _GetStringValueHandler ()
		{
			Func<IntPtr, IntPtr, int, IntPtr> h = GetStringValueHandler;
			return JniEnvironment.Runtime.ExportedMemberBuilder.CreateMarshalToManagedDelegate (h);
		}

		static IntPtr GetStringValueHandler (IntPtr jnienv, IntPtr n_self, int value)
		{
			var r_self  = new JniObjectReference (n_self);
			var self    = JniEnvironment.Runtime.ValueManager.GetValue<TestType>(ref r_self, JniObjectReferenceOptions.CopyAndDoNotRegister);
			try {
				var s = self.GetStringValue (value);
				var r = JniEnvironment.Strings.NewString (s);
				try {
					return JniEnvironment.References.NewReturnToJniRef (r);
				} finally {
					JniObjectReference.Dispose (ref r);
				}
			} finally {
				self.DisposeUnlessReferenced ();
			}
		}

		static Delegate GetMethodThrowsHandler ()
		{
			Action<IntPtr, IntPtr> h = MethodThrowsHandler;
			return JniEnvironment.Runtime.ExportedMemberBuilder.CreateMarshalToManagedDelegate (h);
		}

		static void MethodThrowsHandler (IntPtr jnienv, IntPtr n_self)
		{
			var r_self  = new JniObjectReference (n_self);
			var self    = JniEnvironment.Runtime.ValueManager.GetValue<TestType>(ref r_self, JniObjectReferenceOptions.CopyAndDoNotRegister);
			try {
				self.MethodThrows ();
			} finally {
				self.DisposeUnlessReferenced ();
			}
		}

		public bool EqualsThis (IJavaPeerable value)
		{
			Assert.IsNotNull (value);
			Assert.AreNotSame (this, value);
			Assert.IsTrue (JniEnvironment.Types.IsSameObject (PeerReference, value.PeerReference));
			return value != null && !object.ReferenceEquals (value, this) &&
				JniEnvironment.Types.IsSameObject (PeerReference, value.PeerReference);
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

