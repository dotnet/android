using System.IO;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	static class OS
	{
		public static bool IsWindows => Path.DirectorySeparatorChar == '\\';
	}
}
