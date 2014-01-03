using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Java.Interop;

namespace Java.Interop {

	public class JniType : IDisposable {

		public JniGlobalReference SafeHandle {get; private set;}

		public JniType (string classname)
		{
			using (var t = JniTypes.FindClass (classname))
				SafeHandle = JniHandles.NewGlobalRef (t);
		}

		public void Dispose ()
		{
			if (SafeHandle == null)
				return;
			SafeHandle.Dispose ();
			SafeHandle = null;
		}

		public JniInstanceMethodID GetConstructor (string signature)
		{
			return JniMembers.GetMethodID (SafeHandle, "<init>", signature);
		}

		public JniLocalReference NewObject (JniInstanceMethodID constructor, params JValue[] @params)
		{
			return JniActivator.NewObject (SafeHandle, constructor, @params);
		}

		public JniInstanceMethodID GetInstanceMethod (string name, string signature)
		{
			return JniMembers.GetMethodID (SafeHandle, name, signature);
		}
	}
}
