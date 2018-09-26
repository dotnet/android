using System;
using System.IO;
using System.Linq;

using NUnit.Framework;

namespace EmbeddedDSO
{
	[TestFixture]
	public class EmbeddedDSOApp
	{
		public static string DataDir;
		public static int ApiLevel;

		[Test]
		public void TestApplicationActuallyRan ()
		{
			if (ApiLevel < 23) {
				Assert.Pass ();
				return;
			}

			Assert.That (String.IsNullOrEmpty (DataDir), Is.False, "Data directory unknown");
			string libDir = Path.Combine (DataDir, "lib");
			if (!Directory.Exists (libDir)) {
				Assert.Pass ();
				return;
			}

			Assert.That (Directory.EnumerateFiles (libDir, "*.so", SearchOption.AllDirectories).Any (), Is.False, $"Directory {libDir} should not contain any .so files");
		}
	}
}
