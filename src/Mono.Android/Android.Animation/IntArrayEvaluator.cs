#if ANDROID_21

using Java.Lang;
using Android.Runtime;

namespace Android.Animation
{
	public partial class IntArrayEvaluator
	{
		Object ITypeEvaluator.Evaluate (float fraction, Object startValue, Object endValue)
		{
			return new JavaArray<int> (JNIEnv.NewArray<int> (Evaluate (fraction, (int []) (JavaArray<int>) startValue, (int []) (JavaArray<int>) endValue)), JniHandleOwnership.TransferLocalRef);
		}
	}
}

#endif
