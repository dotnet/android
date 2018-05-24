using System;
using System.IO;

namespace Java.Interop.BootstrapTasks {
	class OS {
		public  static  readonly  bool    IsWindows   = Path.DirectorySeparatorChar == '\\';
		public  static  readonly  bool    IsMacOS     = !IsWindows && Directory.Exists ("/Applications");
		public  static  readonly  bool    IsLinux     = !IsWindows && !IsMacOS;

		public  static  readonly  string  NativeLibraryFormat;

		static OS ()
		{
			if (IsWindows)
				NativeLibraryFormat = "{0}.dll";
			if (IsMacOS)
				NativeLibraryFormat = "lib{0}.dylib";
			if (IsLinux)
				NativeLibraryFormat = "lib{0}.so";
		}
	}
}
