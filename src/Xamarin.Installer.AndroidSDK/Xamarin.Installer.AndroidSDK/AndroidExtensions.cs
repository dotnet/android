using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Kajabity.Tools.Java;
using Xamarin.Installer.Common;

namespace Xamarin.Installer.AndroidSDK
{
	static class AndroidExtensions
	{
		public static bool GetPkgRevision (this JavaProperties props, out string original, out AndroidRevision parsed, string packageName = null)
		{
			original = null;
			parsed = null;

			packageName = packageName.SafeTrim ();
			if (!String.IsNullOrEmpty (packageName))
				packageName = String.Format ("'{0}' ", packageName);

			if (props == null) {
				Logger.Debug ("No properties object, cannot obtain Android package {0}revision", packageName);
				return false;
			}

			string rev;
			if (!props.GetProperty("Pkg.Revision", out rev) || String.IsNullOrEmpty (rev)) {
				Logger.Debug ("Android package {0}has no revision defined", packageName);
				return false;
			}

			original = rev;
			var arev = new AndroidRevision (rev, false);
			parsed = arev.IsValid ? arev : null;

			return parsed != null;
		}

		public static Version TryParseAsAndroidVersion (this string av, bool returnDefaultZeros = false)
		{
			if (String.IsNullOrEmpty (av))
				goto returnDefault;

			if (av.IndexOf ('.') < 0) {
				int v;

				if (!Int32.TryParse (av, out v)) {
					Logger.Info ("Version is not an integer: {0}", av);
					goto returnDefault;
				}

				return new Version (v, 0).CloneFillWithZeros ();
			}

			Version ver;
			if (!Version.TryParse (av, out ver)) {
				Logger.Info ("Package revision is not a valid version: {0}", av);
				goto returnDefault;
			}

			return ver.CloneFillWithZeros ();

			returnDefault:
			return returnDefaultZeros ? new Version (0, 0, 0, 0) : null;
		}
	}
}