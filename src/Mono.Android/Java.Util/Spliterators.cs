#if ANDROID_24
using System;
using Android.Runtime;
using Java.Interop;
using Java.Util.Functions;

namespace Java.Util
{
	// FIXME: this should not be required to be hand-bound.
	public partial class Spliterators
	{
		public partial class AbstractSpliterator
		{
			public abstract bool TryAdvance (IConsumer? action);
		}

		internal partial class AbstractSpliteratorInvoker
		{
			public override bool TryAdvance (IConsumer? action)
			{
				unsafe {
					// SAFETY: tryAdvance is an instance Boolean-returning call with one Consumer; the local reference and argument buffer survive until finally.
					const string __id = "tryAdvance.(Ljava/util/function/Consumer;)Z";
					IntPtr native_action = JNIEnv.ToLocalJniHandle (action);
					try {
						JniArgumentValue* __args = stackalloc JniArgumentValue [1];
						__args [0] = new JniArgumentValue (native_action);
						var __rm = _members.InstanceMethods.InvokeVirtualBooleanMethod (__id, this, __args);
						return __rm;
					} finally {
						JNIEnv.DeleteLocalRef (native_action);
						GC.KeepAlive (action);
						GC.KeepAlive (this);
					}
				}
			}
		}
	}
}
#endif
