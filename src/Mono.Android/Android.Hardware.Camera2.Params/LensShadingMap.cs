#if ANDROID_21
namespace Android.Hardware.Camera2.Params {

	partial class LensShadingMap {

		public float GetGainFactor (int colorChannel, int column, int row)
		{
			return GetGainFactor (new Android.Graphics.Color (colorChannel), column, row);
		}
	}
}
#endif // ANDROID_21
