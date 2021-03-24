using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

using NUnit.Framework;

namespace Xamarin.Android.Tools.Tests
{
	[TestFixture]
	public class AndroidSdkWindowsTests
	{
		[Test]
		public void ExtractVersion ()
		{
			var sep = Path.DirectorySeparatorChar;

			var tests = new[]{
				new {
					Path        = $"foo{sep}",
					Prefix      = "",
					Expected    = (Version) null,
				},
				new {
					Path        = $"foo{sep}bar-1-extra",
					Prefix      = "bar-",
					Expected    = (Version) null,
				},
				new {
					Path        = $"foo{sep}abcdef",
					Prefix      = "a",
					Expected    = (Version) null,
				},
				new {
					Path        = $"foo{sep}a{sep}b.c.d",
					Prefix      = "none-of-the-above",
					Expected    = (Version) null,
				},
				new {
					Path        = $"jdks{sep}jdk-1.2.3-hotspot-extra",
					Prefix      = "jdk-",
					Expected    = new Version (1, 2, 3),
				},
				new {
					Path        = $"jdks{sep}jdk-1.2.3-hotspot-extra",
					Prefix      = "jdk",
					Expected    = new Version (1, 2, 3),
				},
				new {
					Path        = $"jdks{sep}jdk-1.2.3.4.5.6-extra",
					Prefix      = "jdk-",
					Expected    = new Version (1, 2, 3, 4),
				},
			};

			foreach (var test in tests) {
				Assert.AreEqual (
						test.Expected,
						AndroidSdkWindows.ExtractVersion (test.Path, test.Prefix),
						$"Version couldn't be extracted from Path=`{test.Path}` Prefix=`{test.Prefix}`!"
				);
			}
		}
	}
}
