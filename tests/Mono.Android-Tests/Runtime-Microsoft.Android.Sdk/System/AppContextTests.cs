using NUnit.Framework;
using System;
using System.Diagnostics.CodeAnalysis;
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
				/* propertyName */ "<IsSupported>k__BackingField",
				/* expected */     false,
			},
		};

		[Test]
		[TestCaseSource (nameof (TestPrivateSwitchesSource))]
		public void TestPrivateSwitches (
				[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
				string className,
				string propertyName,
				object expected)
		{
			var type = Type.GetType (className, throwOnError: true);
			var members = type.GetMember (propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
			Assert.AreEqual (1, members.Length);
			if (members [0] is PropertyInfo property) {
				Assert.AreEqual (expected, property.GetValue (null));
			} else if (members [0] is FieldInfo field) {
				Assert.AreEqual (expected, field.GetValue (null));
			} else {
				Assert.Fail($"Unknown member type: {members [0]}");
			}
		}
	}
}
