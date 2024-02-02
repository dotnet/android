// This really should be auto-generated, but isn't for test purposes.
using System;

using Java.Interop;
using Java.Interop.GenericMarshaler;

using NUnit.Framework;

namespace Java.InteropTests
{
#if !NO_MARSHAL_MEMBER_BUILDER_SUPPORT
	[JniTypeSignature (TestType.JniTypeName, GenerateJavaPeer=false)]
	public partial class TestType : JavaObject
	{
		internal    const    string         JniTypeName = "net/dot/jni/test/TestType";
		static      readonly JniPeerMembers _members    = new JniPeerMembers (JniTypeName, typeof (TestType));

		[JniAddNativeMethodRegistrationAttribute]
		static void RegisterNativeMembers (JniNativeMethodRegistrationArguments args)
		{
			args.Registrations.Add (new JniNativeMethodRegistration ("equalsThis", "(Ljava/lang/Object;)Z", GetEqualsThisHandler ()));
			args.Registrations.Add (new JniNativeMethodRegistration ("getInt32Value", "()I", GetInt32ValueHandler ()));
		}

		[JniAddNativeMethodRegistrationAttribute]
		static void RegisterNativeMembers2 (JniNativeMethodRegistrationArguments args)
		{
			args.Registrations.Add (new JniNativeMethodRegistration ("getStringValue", "(I)Ljava/lang/String;", _GetStringValueHandler ()));
			args.Registrations.Add (new JniNativeMethodRegistration ("methodThrows", "()V", GetMethodThrowsHandler ()));
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
			var o   = _members.StaticMethods.InvokeObjectMethod ("newTestType.()Lnet/dot/jni/test/TestType;", null);
			return JniEnvironment.Runtime.ValueManager.GetValue<TestType> (ref o, JniObjectReferenceOptions.CopyAndDispose);
		}

		public static unsafe TestType NewTestTypeWithUnboundConstructor ()
		{
			const string id = "newTestTypeWithUnboundConstructor.()Lnet/dot/jni/test/TestType;";
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
			EqualsThisMarshalMethod h = _EqualsThis;
			return JniEnvironment.Runtime.MarshalMemberBuilder.CreateMarshalToManagedDelegate (h);
		}

		delegate bool EqualsThisMarshalMethod (IntPtr jnienv, IntPtr n_self, IntPtr n_value);
		static bool _EqualsThis (IntPtr jnienv, IntPtr n_self, IntPtr n_value)
		{
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
		}

		static Delegate GetInt32ValueHandler ()
		{
			GetInt32ValueMarshalMethod h = _GetInt32Value;
			return JniEnvironment.Runtime.MarshalMemberBuilder.CreateMarshalToManagedDelegate (h);
		}

		delegate int GetInt32ValueMarshalMethod (IntPtr jnienv, IntPtr n_self);
		static int _GetInt32Value (IntPtr jnienv, IntPtr n_self)
		{
			var r_self  = new JniObjectReference (n_self);
			var self    = JniEnvironment.Runtime.ValueManager.GetValue<TestType>(ref r_self, JniObjectReferenceOptions.CopyAndDoNotRegister);
			try {
				return self.GetInt32Value ();
			} finally {
				self.DisposeUnlessReferenced ();
			}
		}

		static Delegate _GetStringValueHandler ()
		{
			GetStringValueMarshalMethod h = GetStringValueHandler;
			return JniEnvironment.Runtime.MarshalMemberBuilder.CreateMarshalToManagedDelegate (h);
		}

		delegate IntPtr GetStringValueMarshalMethod (IntPtr jnienv, IntPtr n_self, int value);
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
			MethodThrowsMarshalMethod h = MethodThrowsHandler;
			return JniEnvironment.Runtime.MarshalMemberBuilder.CreateMarshalToManagedDelegate (h);
		}

		delegate void MethodThrowsMarshalMethod (IntPtr jnienv, IntPtr n_self);
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
#endif  // !NO_MARSHAL_MEMBER_BUILDER_SUPPORT
}

