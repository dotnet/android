#if ANDROID_18

using Java.Lang;
using Android.Graphics;

namespace Android.Animation
{
	public partial class RectEvaluator
	{
		Object ITypeEvaluator.Evaluate (float fraction, Object startValue, Object endValue)
		{
			return Evaluate (fraction, (Rect) startValue, (Rect) endValue);
		}
	}
}

#endif
