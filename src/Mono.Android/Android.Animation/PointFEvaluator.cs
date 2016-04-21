#if ANDROID_21

using Java.Lang;
using Android.Graphics;

namespace Android.Animation
{
	public partial class PointFEvaluator
	{
		Object ITypeEvaluator.Evaluate (float fraction, Object startValue, Object endValue)
		{
			return Evaluate (fraction, (PointF) startValue, (PointF) endValue);
		}
	}
}

#endif
