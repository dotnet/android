#if ANDROID_11

using Java.Lang;
using Android.Runtime;

namespace Android.Animation
{
	public partial class FloatEvaluator
	{
		public virtual Object Evaluate (float fraction, Object startValue, Object endValue)
		{
			return Evaluate (fraction, startValue.JavaCast<Number> (), endValue.JavaCast<Number> ());
		}
	}
}

#endif
