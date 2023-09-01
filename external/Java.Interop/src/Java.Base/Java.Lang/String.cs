using System;
using System.Collections;
using System.Collections.Generic;
using Java.Interop;

#if JAVA_API_17
using Java.Lang.Invoke;
using Java.Lang.Constants;
#endif  // JAVA_API_17

namespace Java.Lang {
	public partial class String : IEnumerable, IEnumerable<char> {

#if JAVA_API_17
		unsafe Java.Lang.Object? IConstantDesc.ResolveConstantDesc (MethodHandles.Lookup? lookup)
		{
			const string __id = "resolveConstantDesc.(Ljava/lang/invoke/MethodHandles$Lookup;)Ljava/lang/String;";
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (lookup);
				var __rm = _members.InstanceMethods.InvokeAbstractObjectMethod (__id, this, __args);
				return JniEnvironment.Runtime.ValueManager.GetValue<String>(ref __rm, JniObjectReferenceOptions.CopyAndDispose);
			} finally {
				global::System.GC.KeepAlive (lookup);
			}
		}
#endif  // JAVA_API_17
	}
}
