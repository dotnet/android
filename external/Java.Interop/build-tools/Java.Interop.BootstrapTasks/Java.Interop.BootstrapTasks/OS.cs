using System;
using System.IO;

namespace Java.Interop.BootstrapTasks {
	class OS {
		public  static  readonly  bool    IsWindows   = Path.DirectorySeparatorChar == '\\';
		public  static  readonly  bool    IsMacOS     = !IsWindows && Directory.Exists ("/Applications");
		public  static  readonly  bool    IsLinux     = !IsWindows && !IsMacOS;
	}
}
