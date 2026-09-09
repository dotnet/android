namespace System
{
	[Android.Runtime.Register ("my/app/LookalikeInt32", DoNotGenerateAcw = true)]
	public class Int32 : Java.Lang.Object
	{
	}

	[Android.Runtime.Register ("my/app/LookalikeIntPtr", DoNotGenerateAcw = true)]
	public class IntPtr : Java.Lang.Object
	{
	}
}

namespace Android.Runtime
{
	[Register ("my/app/LookalikeOwnership", DoNotGenerateAcw = true)]
	public class JniHandleOwnership : Java.Lang.Object
	{
	}
}

namespace Java.Interop
{
	[Android.Runtime.Register ("my/app/LookalikeReference", DoNotGenerateAcw = true)]
	public class JniObjectReference : Java.Lang.Object
	{
	}

	[Android.Runtime.Register ("my/app/LookalikeOptions", DoNotGenerateAcw = true)]
	public class JniObjectReferenceOptions : Java.Lang.Object
	{
	}
}
