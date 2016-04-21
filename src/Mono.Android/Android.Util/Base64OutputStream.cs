namespace Android.Util {

	partial class Base64OutputStream {

#if ANDROID_8
		public Base64OutputStream (System.IO.Stream @out, Base64Flags flags)
			: this (@out, (int) flags)
		{
		}
#endif
	}
}
