using NUnit.Framework;
using System;
using System.Reflection;

namespace SystemTests
{
	[TestFixture]
	public class AppContextTests
	{
		static readonly object [] GetDataSource = new object [] {
			new object [] {
				/* name */     "test_bool",
				/* expected */ "true",
			},
			new object [] {
				/* name */     "test_integer",
				/* expected */ "42",
			},
			new object [] {
				/* name */     "test_string",
				/* expected */ "foo",
			},
		};

		[Test]
		[TestCaseSource (nameof (GetDataSource))]
		public void GetData (string name, string expected)
		{
			Assert.AreEqual (expected, AppContext.GetData (name));
		}

		static readonly object [] TestPrivateSwitchesSource = new object [] {
			new object [] {
				/* className */    "System.LocalAppContextSwitches, System.Private.CoreLib",
				/* propertyName */ "ForceInterpretedInvoke",
				/* expected */     true,
			},
			new object [] {
				/* className */    "System.Diagnostics.Metrics.Meter, System.Diagnostics.DiagnosticSource",
				/* propertyName */ "IsSupported",
				/* expected */     false,
			},
		};

		[Test]
		[TestCaseSource (nameof (TestPrivateSwitchesSource))]
		public void TestPrivateSwitches (string className, string propertyName, object expected)
		{
			var type = Type.GetType (className, throwOnError: true);
			var property = type.GetProperty (propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
			Assert.IsNotNull (property);
			Assert.AreEqual (expected, property.GetValue (null));
		}
	}
}
