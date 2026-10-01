using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	public class FastDeployTypeMapTests : BaseTest
	{
		[TestCase (true, true, true, true, 2)]
		[TestCase (true, true, true, false, 3)]
		[TestCase (true, true, false, true, 3)]
		[TestCase (true, false, true, true, 3)]
		[TestCase (false, true, true, true, 1)]
		public void PreTrimTypeMapYieldsOnlyToMatchingFinalCopy (
			bool includeFinal, bool finalExists, bool trimmed, bool sameAbi, int expectedCount)
		{
			var path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);
			try {
				var preTrimPath = Path.Combine (path, "pretrim", "_Microsoft.Android.TypeMaps.dll");
				var finalPath = Path.Combine (path, "R2R", "_Microsoft.Android.TypeMaps.dll");
				Directory.CreateDirectory (Path.GetDirectoryName (preTrimPath));
				Directory.CreateDirectory (Path.GetDirectoryName (finalPath));
				File.WriteAllBytes (preTrimPath, [1]);
				if (finalExists) {
					File.WriteAllBytes (finalPath, [2]);
				}

				var preTrim = new TaskItem (preTrimPath);
				preTrim.SetMetadata ("TargetPath", "x86_64/_Microsoft.Android.TypeMaps.dll");
				if (trimmed) {
					preTrim.SetMetadata ("_AndroidPreTrimTypeMapCandidate", "true");
				}
				var final = new TaskItem (finalPath);
				final.SetMetadata ("TargetPath", sameAbi
					? "x86_64\\_Microsoft.Android.TypeMaps.dll"
					: "arm64-v8a/_Microsoft.Android.TypeMaps.dll");

				var files = new List<ITaskItem> { preTrim };
				if (includeFinal) {
					files.Add (final);
					files.Add (new TaskItem (final));
				}
				var selected = new FastDeploy ().FilterPreTrimTypeMapFiles (files.ToArray ());
				Assert.AreEqual (expectedCount, selected.Length);
				Assert.AreEqual (!includeFinal || !finalExists || !trimmed || !sameAbi,
					selected.Any (item => item.ItemSpec == preTrimPath));
			} finally {
				Directory.Delete (path, recursive: true);
			}
		}
	}
}
