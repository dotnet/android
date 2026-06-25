using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xamarin.Installer.Common
{
	public static partial class Extensions
	{
		public static Version CloneFillWithZeros (this Version v)
		{
			return v.CloneValidVersion ();
		}

		public static Version CloneValidVersion (this Version v, int defaultMajor = -1, int defaultMinor = -1, int defaultBuild = -1, int defaultRevision = -1)
		{
			if (v == null)
				return null;

			return new Version (
				GetValidValue (v.Major, defaultMajor),
				GetValidValue (v.Minor, defaultMinor),
				GetValidValue (v.Build, defaultBuild),
				GetValidValue (v.Revision, defaultRevision)
			);
		}

		static int GetValidValue (int versionPart, int defaultValue)
		{
			if (versionPart >= 0)
				return versionPart;

			return defaultValue > 0 ? defaultValue : 0;
		}
	}
}
