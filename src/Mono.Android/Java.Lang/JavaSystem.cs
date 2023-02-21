using System.Threading.Tasks;
using System.Runtime.InteropServices;

using Android.Runtime;

namespace Java.Lang
{
	public sealed partial class JavaSystem : Java.Lang.Object
	{
		[DllImport (RuntimeConstants.InternalDllName, CallingConvention = CallingConvention.Cdecl)]
		static extern void monodroid_javasystem_loadLibrary (string libname);

		public static void LoadLibrary (string libname)
		{
			monodroid_javasystem_loadLibrary (libname);
		}

		public static Task LoadLibraryAsync (string libname)
		{
			return Task.Run (() => LoadLibrary (libname));
		}
	}
}
