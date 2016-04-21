#if ANDROID_21
namespace Android.Hardware.Camera2.Params {

	partial class TonemapCurve {

		public void CopyColorCurve (int colorChannel, float[] destination, int offset)
		{
			CopyColorCurve (new Android.Graphics.Color (colorChannel), destination, offset);
		}

		public Android.Graphics.PointF GetPoint (int colorChannel, int index)
		{
			return GetPoint (new Android.Graphics.Color (colorChannel), index);
		}

		public int GetPointCount (int colorChannel)
		{
			return GetPointCount (new Android.Graphics.Color (colorChannel));
		}
	}
}
#endif // ANDROID_21
