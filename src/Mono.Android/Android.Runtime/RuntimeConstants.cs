#if !NETCOREAPP || INSIDE_MONO_ANDROID_RUNTIME
namespace Android.Runtime
{
	internal static class RuntimeConstants
	{
		public const string InternalDllName = "xa-internal-api";
	}
}
#endif // !NETCOREAPP || INSIDE_MONO_ANDROID_RUNTIME
