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

		public static JniType GetCachedJniType (ref JniType cachedType, string classname)
		{
			if (cachedType != null && !cachedType.SafeHandle.IsInvalid)
				return cachedType;
			var t = new JniType (classname);
			if (Interlocked.CompareExchange (ref cachedType, t, null) != null)
				t.Dispose ();
			return cachedType;
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

		public JniInstanceMethodID GetCachedConstructor (ref JniInstanceMethodID cachedMethod, string signature)
		{
			return GetCachedInstanceMethod (ref cachedMethod, "<init>", signature);
		}

		public JniLocalReference AllocObject ()
		{
			return JniActivator.AllocObject (SafeHandle);
		}

		public JniLocalReference NewObject (JniInstanceMethodID constructor, params JValue[] @params)
		{
			return JniActivator.NewObject (SafeHandle, constructor, @params);
		}

		public JniInstanceFieldID GetInstanceField (string name, string signature)
		{
			return JniMembers.GetFieldID (SafeHandle, name, signature);
		}

		public JniInstanceFieldID GetCachedInstanceField (ref JniInstanceFieldID cachedField, string name, string signature)
		{
			if (cachedField != null && !cachedField.IsInvalid)
				return cachedField;
			var m = GetInstanceField (name, signature);
			if (Interlocked.CompareExchange (ref cachedField, m, null) != null)
				m.Dispose ();
			return cachedField;
		}

		public JniStaticFieldID GetStaticField (string name, string signature)
		{
			return JniMembers.GetStaticFieldID (SafeHandle, name, signature);
		}

		public JniStaticFieldID GetCachedStaticField (ref JniStaticFieldID cachedField, string name, string signature)
		{
			if (cachedField != null && !cachedField.IsInvalid)
				return cachedField;
			var m = GetStaticField (name, signature);
			if (Interlocked.CompareExchange (ref cachedField, m, null) != null)
				m.Dispose ();
			return cachedField;
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

		public JniStaticMethodID GetStaticMethod (string name, string signature)
		{
			return JniMembers.GetStaticMethodID (SafeHandle, name, signature);
		}

		public JniStaticMethodID GetCachedStaticMethod (ref JniStaticMethodID cachedMethod, string name, string signature)
		{
			if (cachedMethod != null && !cachedMethod.IsInvalid)
				return cachedMethod;
			var m = GetStaticMethod (name, signature);
			if (Interlocked.CompareExchange (ref cachedMethod, m, null) != null)
				m.Dispose ();
			return cachedMethod;
		}
	}
}
