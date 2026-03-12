using System;
using System.Linq;
using NUnit.Framework;
using Xamarin.ProjectTools;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Xamarin.Tools.Zip;
using Xamarin.Android.Tasks;
using System.Collections.Generic;

namespace Xamarin.Android.Build.Tests
{
	public partial class ManifestTest : BaseTest
	{
		static IEnumerable<object[]> Get_DebuggerAttributeCases_Data ()
		{
			var ret = new List<object[]> ();

			foreach (AndroidRuntime runtime in Enum.GetValues (typeof (AndroidRuntime))) {
				AddTestData ( "", true, false, runtime);
				AddTestData ( "", false, true, runtime);
				AddTestData ( "None", true, false, runtime);
				AddTestData ( "None", false, true, runtime);
				AddTestData ( "PdbOnly", true, false, runtime);
				AddTestData ( "PdbOnly", false, true, runtime);
				AddTestData ( "Full", true, false, runtime);
				AddTestData ( "Full", false, true, runtime);
				AddTestData ( "Portable", true, false, runtime);
				AddTestData ( "Portable", false, true, runtime);
			}
			return ret;

			void AddTestData (string debugType, bool isRelease, bool expected, AndroidRuntime runtime)
			{
				ret.Add (new object[] {
					debugType,
					isRelease,
					expected,
					runtime,
				});
			}
		}
	}
}
