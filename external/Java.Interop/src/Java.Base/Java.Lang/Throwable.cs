using System;
using System.Collections.Generic;
using Java.Interop;

namespace Java.Lang {

	public partial class Throwable : JavaException {

		public Throwable (ref JniObjectReference reference, JniObjectReferenceOptions options)
			: base (ref reference, options)
		{
		}
	}
}
