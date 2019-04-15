using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests
{
	public class AndroidRegExTests
	{


		/*
"Resources/values/theme.xml(2): error APT0000: Error retrieving parent for item: No resource found that matches the given name '@android:style/Theme.AppCompat'." \
  "Resources/values/theme.xml:2: error APT0000: Error retrieving parent for item: No resource found that matches the given name '@android:style/Theme.AppCompat'." \
  "res/drawable/foo-bar.jpg: Invalid file name: must contain only [a-z0-9_.]"

		*/
		class AndroidRegExTestsCases : IEnumerable {
			public IEnumerator GetEnumerator ()
			{
				yield return new object [] {
					/*message*/		"warning: string 'app_name1' has no default translation.",
					/*expectedToMatch*/	true,
					/*expectedFile*/	"",
					/*expectedLine*/	"",
					/*expectedLevel*/	"warning",
					/*expectedMessage*/	"string 'app_name1' has no default translation."
				};
				yield return new object [] {
					/*message*/		"res\\layout\\main.axml: error: No resource identifier found for attribute \"id2\" in package \"android\" (TaskId:22)",
					/*expectedToMatch*/	true,
					/*expectedFile*/	"res\\layout\\main.axml",
					/*expectedLine*/	"",
					/*expectedLevel*/	"error",
					/*expectedMessage*/	"No resource identifier found for attribute \"id2\" in package \"android\" (TaskId:22)"
				};
				yield return new object [] {
					/*message*/		"Resources/values/theme.xml(2): error APT0000: Error retrieving parent for item: No resource found that matches the given name '@android:style/Theme.AppCompat'.",
					/*expectedToMatch*/	true,
					/*expectedFile*/	"Resources/values/theme.xml",
					/*expectedLine*/	"2",
					/*expectedLevel*/	"error APT0000",
					/*expectedMessage*/	"Error retrieving parent for item: No resource found that matches the given name '@android:style/Theme.AppCompat'."
				};
				yield return new object [] {
					/*message*/		"Resources/values/theme.xml:2: error APT0000: Error retrieving parent for item: No resource found that matches the given name '@android:style/Theme.AppCompat'.",
					/*expectedToMatch*/	true,
					/*expectedFile*/	"Resources/values/theme.xml",
					/*expectedLine*/	"2",
					/*expectedLevel*/	"error APT0000",
					/*expectedMessage*/	"Error retrieving parent for item: No resource found that matches the given name '@android:style/Theme.AppCompat'."
				};
				yield return new object [] {
					/*message*/		"res/drawable/foo-bar.jpg: Invalid file name: must contain only [a-z0-9_.]",
					/*expectedToMatch*/	true,
					/*expectedFile*/	"res/drawable/foo-bar.jpg",
					/*expectedLine*/	"",
					/*expectedLevel*/	"",
					/*expectedMessage*/	"Invalid file name: must contain only [a-z0-9_.]"
				};
				yield return new object [] {
					/*message*/		"max res 10, skipping values-sw600dp",
					/*expectedToMatch*/	true,
					/*expectedFile*/	"",
					/*expectedLine*/	"",
					/*expectedLevel*/	"",
					/*expectedMessage*/	"max res 10, skipping values-sw600dp"
				};
				yield return new object [] {
					/*message*/		"max res 10, skipping values-sw600dp-land",
					/*expectedToMatch*/	true,
					/*expectedFile*/	"",
					/*expectedLine*/	"",
					/*expectedLevel*/	"",
					/*expectedMessage*/	"max res 10, skipping values-sw600dp-land"
				};
				yield return new object [] {
 					/*message*/		"max res 10, skipping values-sw720dp-land-v13",
 					/*expectedToMatch*/	true,
 					/*expectedFile*/	"",
 					/*expectedLine*/	"",
 					/*expectedLevel*/	"",
 					/*expectedMessage*/	"max res 10, skipping values-sw720dp-land-v13"
				};
				yield return new object [] {
					/*message*/		"Error: unable to generate entry for resource data",
					/*expectedToMatch*/	true,
					/*expectedFile*/	"",
					/*expectedLine*/	"",
					/*expectedLevel*/	"Error",
					/*expectedMessage*/	"unable to generate entry for resource data"
				};
				yield return new object [] {
					/*message*/		"Error: malformed resource filename",
					/*expectedToMatch*/	true,
					/*expectedFile*/	"",
					/*expectedLine*/	"",
					/*expectedLevel*/	"Error",
					/*expectedMessage*/	"malformed resource filename"
				};
				yield return new object [] {
					/*message*/		"warning: Multiple AndroidManifest.xml files found, using foo\\AndroidManifest.xml",
					/*expectedToMatch*/	true,
					/*expectedFile*/	"",
					/*expectedLine*/	"",
					/*expectedLevel*/	"warning",
					/*expectedMessage*/	"Multiple AndroidManifest.xml files found, using foo\\AndroidManifest.xml"
				};
				yield return new object [] {
					/*message*/		"Resources/values/theme.xml:55: No start tag found",
					/*expectedToMatch*/	true,
					/*expectedFile*/	"Resources/values/theme.xml",
					/*expectedLine*/	"55",
					/*expectedLevel*/	"",
					/*expectedMessage*/	"No start tag found"
				};
				yield return new object [] {
					/*message*/		"package name is required with --rename-manifest-package.",
					/*expectedToMatch*/	true,
					/*expectedFile*/	"",
					/*expectedLine*/	"",
					/*expectedLevel*/	"",
					/*expectedMessage*/	"package name is required with --rename-manifest-package."
				};
				yield return new object [] {
					/*message*/		"invalid resource directory name: bar-55",
					/*expectedToMatch*/	true,
					/*expectedFile*/	"",
					/*expectedLine*/	"",
					/*expectedLevel*/	"",
					/*expectedMessage*/	"invalid resource directory name: bar-55"
				};
				yield return new object [] {
					/*message*/		"W/ResourceType(23681): For resource 0x0101053d, entry index(1341) is beyond type entryCount(733)",
					/*expectedToMatch*/	true,
					/*expectedFile*/	"W/ResourceType",
					/*expectedLine*/	"23681",
					/*expectedLevel*/	"",
					/*expectedMessage*/	"For resource 0x0101053d, entry index(1341) is beyond type entryCount(733)"
				};
				yield return new object [] {
					/*message*/		"W/ResourceType( 5536): For resource 0x0101053d, entry index(1341) is beyond type entryCount(1155)",
					/*expectedToMatch*/	true,
					/*expectedFile*/	"W/ResourceType",
					/*expectedLine*/	" 5536",
					/*expectedLevel*/	"",
					/*expectedMessage*/	"For resource 0x0101053d, entry index(1341) is beyond type entryCount(1155)"
				};
			}
		}

		[Test]
		[TestCaseSource(typeof (AndroidRegExTestsCases))]
		public void RegExTests(string message, bool expectedToMatch, string expectedFile, string expectedLine, string expectedLevel, string expextedMessage)
		{
			var regex = Xamarin.Android.Tasks.AndroidToolTask.AndroidErrorRegex;
			var result = regex.Match (message);
			Assert.AreEqual (expectedToMatch,result.Success);
			Assert.AreEqual (expectedFile, result.Groups["file"].Value);
			Assert.AreEqual (expectedLine.ToString (), result.Groups ["line"].Value);
			Assert.AreEqual (expectedLevel, result.Groups ["level"].Value);
			Assert.AreEqual (expextedMessage, result.Groups ["message"].Value);
		}
	}
}
