namespace Android.Util {

	partial class Base64InputStream {

#if ANDROID_8
		public Base64InputStream (System.IO.Stream @in, Base64Flags flags)
			: this (@in, (int) flags)
		{
		}
#endif
	}
}
