using System;

namespace Java.Interop.Dynamic {

	abstract class JavaMemberInfo : IDisposable {

		public      abstract    string      Name            {get;}
		public      abstract    bool        IsStatic        {get;}

		protected JavaMemberInfo ()
		{
		}

		public void Dispose ()
		{
			Dispose (disposing: true);
		}

		protected virtual void Dispose (bool disposing)
		{
		}

		protected static object ToReturnValue (ref JniObjectReference handle, string signature, int n)
		{
			var instance    = JniEnvironment.Runtime.ValueMarshaler.GetObject (ref handle, JniObjectReferenceOptions.CopyAndDispose);
			switch (signature [n]) {
			case 'L':
				return new DynamicJavaInstance (instance);
			case '[':
			default:
				return instance;
			}
		}
	}
}

