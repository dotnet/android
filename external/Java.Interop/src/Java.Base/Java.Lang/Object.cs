using System;
using System.Collections.Generic;
using Java.Interop;

namespace Java.Lang {

	public partial class Object : JavaObject {

		public Object (ref JniObjectReference reference, JniObjectReferenceOptions options)
			: base (ref reference, options)
		{
		}
	}
}
