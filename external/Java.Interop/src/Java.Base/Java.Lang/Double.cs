using System;
using Java.Interop;

#if JAVA_API_17
using Java.Lang.Invoke;
using Java.Lang.Constants;

namespace Java.Lang {
	public partial class Double {

		Java.Lang.Object? IConstantDesc.ResolveConstantDesc (MethodHandles.Lookup? lookup) =>
			ResolveConstantDesc (lookup);
	}
}

#endif  // JAVA_API_17
