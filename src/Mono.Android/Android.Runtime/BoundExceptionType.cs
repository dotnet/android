namespace Android.Runtime
{
	// Keep the enum values in sync with those in src/monodroid/jni/monodroid-glue-internal.hh
	enum BoundExceptionType : byte
	{
		System = 0x00,
		Java   = 0x01,
	};
}
