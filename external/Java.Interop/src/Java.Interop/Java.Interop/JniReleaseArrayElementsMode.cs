#nullable enable

using System;

namespace Java.Interop {

	// http://docs.oracle.com/javase/8/docs/technotes/guides/jni/spec/functions.html#Release_PrimitiveType_ArrayElements_routines
	public enum JniReleaseArrayElementsMode {
		Default = 0,    // 0
		Commit  = 1,    // JNI_COMMIT
		Abort   = 2,    // JNI_ABORT
	}
}
