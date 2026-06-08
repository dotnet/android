using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xamarin.Installer.Common
{
	static partial class Extensions
	{
		public static string SafeTrim (this string s)
		{
			return (s ?? String.Empty).Trim ();
		}

		public static string RemoveTrailingDirectorySeparator (this string path)
		{
			if (String.IsNullOrEmpty (path) || (!path.EndsWith (Path.DirectorySeparatorChar.ToString ()) && !path.EndsWith (Path.AltDirectorySeparatorChar.ToString ())))
				return path;
			return path.Substring (0, path.Length - 1);
		}
	}
}
