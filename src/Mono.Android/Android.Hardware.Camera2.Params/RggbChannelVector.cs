#if ANDROID_21
namespace Android.Hardware.Camera2.Params {

	partial class RggbChannelVector {

		public float GetComponent (int colorChannel)
		{
			return GetComponent (new Android.Graphics.Color (colorChannel));
		}
	}
}
#endif // ANDROID_21
