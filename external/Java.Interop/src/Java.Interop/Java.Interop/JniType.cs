using System;
using System.Threading;

using Java.Interop;

namespace Java.Interop {

	public class JniType : IDisposable {

		public JniGlobalReference SafeHandle {get; private set;}

		public JniType (string classname)
		{
			using (var t = JniTypes.FindClass (classname))
				SafeHandle = t.NewGlobalRef ();
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

		public JniLocalReference AllocObject ()
		{
			return JniActivator.AllocObject (SafeHandle);
		}

		public JniLocalReference NewObject (JniInstanceMethodID constructor, params JValue[] @params)
		{
			return JniActivator.NewObject (SafeHandle, constructor, @params);
		}

		public JniInstanceMethodID GetInstanceMethod (string name, string signature)
		{
			return JniMembers.GetMethodID (SafeHandle, name, signature);
		}

		public JniInstanceMethodID GetCachedInstanceMethod (ref JniInstanceMethodID cachedMethod, string name, string signature)
		{
			if (cachedMethod != null && !cachedMethod.IsInvalid)
				return cachedMethod;
			var m = GetInstanceMethod (name, signature);
			if (Interlocked.CompareExchange (ref cachedMethod, m, null) != null)
				m.Dispose ();
			return cachedMethod;
		}
	}
}
