using NUnit.Framework;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace SystemTests
{
	[TestFixture]
	[Category ("RuntimeConfig")] //TODO: https://github.com/dotnet/android/issues/10069
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
#if DEBUG
				/* expected */     true,
#else   // !DEBUG
				/* expected */     false,
#endif  // !DEBUG
			},
		};

		[Test]
		[Category ("NativeAOTIgnore")] // These switches only exist in Mono & CoreCLR BCL assemblies
		[DynamicDependency (DynamicallyAccessedMemberTypes.All, "System.LocalAppContextSwitches", "System.Private.CoreLib")]
		[DynamicDependency (DynamicallyAccessedMemberTypes.All, "System.Diagnostics.Metrics.Meter", "System.Diagnostics.DiagnosticSource")]
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

		// `AppContext.BaseDirectory` is backed by the `APP_CONTEXT_BASE_DIRECTORY` host property,
		// which every runtime must set to `Context.getFilesDir()`. When it is left unset, the
		// `AppContext.GetBaseDirectoryCore()` fallback returns something useless on Android:
		// the empty string on CoreCLR (assemblies are embedded, so `Assembly.Location` is empty)
		// and `/system/bin/` on NativeAOT (that is where `app_process64` lives).
		[Test]
		public void BaseDirectoryIsFilesDir ()
		{
			var filesDir = Android.App.Application.Context.FilesDir;
			if (filesDir == null) {
				Assert.Fail ("`Context.FilesDir` was null.");
				return;
			}

			// .NET always terminates `AppContext.BaseDirectory` with a directory separator. Android
			// does not return one today, but normalize instead of assuming it never will.
			string expected = filesDir.AbsolutePath.TrimEnd ('/') + "/";
			Assert.AreEqual (expected, AppContext.BaseDirectory);
		}
	}
}
