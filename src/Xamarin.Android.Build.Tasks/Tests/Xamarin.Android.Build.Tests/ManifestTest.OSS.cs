using System;
using System.Linq;
using NUnit.Framework;
using Xamarin.ProjectTools;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Xamarin.Tools.Zip;

namespace Xamarin.Android.Build.Tests
{
	public partial class ManifestTest : BaseTest
	{
		static object [] DebuggerAttributeCases = new object [] {
			// DebugType, isRelease, extpected
			new object[] { "", true, false, },
			new object[] { "", false, true, },
			new object[] { "None", true, false, },
			new object[] { "None", false, true, },
			new object[] { "PdbOnly", true, false, },
			new object[] { "PdbOnly", false, true, },
			new object[] { "Full", true, false, },
			new object[] { "Full", false, true, },
			new object[] { "Portable", true, false, },
			new object[] { "Portable", false, true, },
		};
	}
}
