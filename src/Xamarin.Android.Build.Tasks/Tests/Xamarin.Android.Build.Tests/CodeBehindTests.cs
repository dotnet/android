using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using NUnit.Framework;

using Xamarin.ProjectTools;
using Xamarin.Android.Tasks;
using System.Text;

namespace Xamarin.Android.Build.Tests
{
	[Parallelizable (ParallelScope.Children)]
	public class CodeBehindTests : BaseTest
	{
		sealed class LocalBuilder : Builder
		{
			public LocalBuilder ()
			{
				BuildingInsideVisualStudio = false;
				Verbosity = LoggerVerbosity.Detailed;
			}

			public bool Build (string projectOrSolution, string target, string [] parameters = null, Dictionary<string, string> environmentVariables = null)
			{
				return BuildInternal (projectOrSolution, target, parameters, environmentVariables);
			}
		}

		sealed class SourceFileMember
		{
			public string Visibility { get; }
			public string Type { get; }
			public string Name { get; }
			public string Arguments { get; }
			public bool IsExpressionBody { get; }
			public bool IsMethod { get; }

			public SourceFileMember (string visibility, string type, string name, bool isExpressionBody)
			{
				if (String.IsNullOrEmpty (visibility))
					throw new ArgumentException (nameof (visibility));
				if (String.IsNullOrEmpty (type))
					throw new ArgumentException (nameof (type));
				if (String.IsNullOrEmpty (name))
					throw new ArgumentException (nameof (name));
				Visibility = visibility;
				Type = type;
				Name = name;
				IsExpressionBody = isExpressionBody;
				IsMethod = false;
			}

			public SourceFileMember (string visibility, string type, string name, string arguments)
			{
				if (String.IsNullOrEmpty (visibility))
					throw new ArgumentException (nameof (visibility));
				if (String.IsNullOrEmpty (type))
					throw new ArgumentException (nameof (type));
				if (String.IsNullOrEmpty (name))
					throw new ArgumentException (nameof (name));
				Visibility = visibility;
				Type = type;
				Name = name;
				Arguments = arguments ?? String.Empty;
				IsExpressionBody = false;
				IsMethod = true;
			}
		}

		sealed class SourceFile : IEnumerable<SourceFileMember>
		{
			readonly List<SourceFileMember> properties;

			public string Path { get; }
			public bool ForMany { get; }

			public SourceFile (string path, bool forMany = false)
			{
				if (String.IsNullOrEmpty (path))
					throw new ArgumentException (nameof (path));
				Path = path;
				ForMany = forMany;
				properties = new List<SourceFileMember> ();
			}

			public void Add (string visibility, string type, string name, bool isExpressionBody = true)
			{
				properties.Add (new SourceFileMember (visibility, type, name, isExpressionBody));
			}

			public void Add (string visibility, string type, string name, string arguments)
			{
				properties.Add (new SourceFileMember (visibility, type, name, arguments));
			}

			public IEnumerator<SourceFileMember> GetEnumerator ()
			{
				return ((IEnumerable<SourceFileMember>) properties).GetEnumerator ();
			}

			IEnumerator IEnumerable.GetEnumerator ()
			{
				return ((IEnumerable<SourceFileMember>) properties).GetEnumerator ();
			}
		}

		sealed class TestProjectInfo
		{
			public string RootDirectory { get; }
			public string OutputDirectory { get; }
			public string TestRootDirectory { get; }
			public string ObjPath { get; }
			public string BinPath { get; }
			public string GeneratedPath { get; }
			public string ProjectPath { get; }
			public string ProjectName { get; }
			public string TestName { get; }
			public AndroidRuntime Runtime { get; }

			public TestProjectInfo (string projectName, string testName, string rootDirectory, string outputRootDir, AndroidRuntime runtime)
			{
				TestName = testName;
				RootDirectory = rootDirectory;
				ProjectName = projectName;
				Runtime = runtime;

				ObjPath = Path.Combine (rootDirectory, "obj");
				GeneratedPath = Path.Combine (ObjPath, XABuildPaths.Configuration, "codebehind");
				BinPath = Path.Combine (rootDirectory, "bin", XABuildPaths.Configuration);
				ProjectPath = Path.Combine (rootDirectory, $"{projectName}.csproj");

				TestRootDirectory = Path.Combine (outputRootDir, testName);
				OutputDirectory = Path.Combine (TestRootDirectory, XABuildPaths.Configuration);
			}
		}

		static readonly string PackageName = "com.xamarin.CodeBehindBuildTests";
		static readonly string ProjectName = "CodeBehindBuildTests";
		const string CommonSampleLibraryName = "CommonSampleLibrary";

		static readonly string TestProjectRootDirectory;
		static readonly string CommonSampleLibraryRootDirectory;
		static readonly string TestOutputDir;

		static readonly List <SourceFile> generated_sources;
		static readonly List <string> produced_binaries;

		static readonly List <string> log_files = new List <string> {
			"process.log",
			"msbuild.binlog",
		};

		static CodeBehindTests ()
		{
			TestProjectRootDirectory = Path.GetFullPath (Path.Combine (XABuildPaths.TopDirectory, "tests", "CodeBehind", "BuildTests"));
			CommonSampleLibraryRootDirectory = Path.GetFullPath (Path.Combine (XABuildPaths.TopDirectory, "tests", "CodeBehind", CommonSampleLibraryName));
			TestOutputDir = Path.Combine (XABuildPaths.TestOutputDirectory, "temp", "CodeBehind");
			ProjectName += ".NET";

			generated_sources = new List <SourceFile> {
				new SourceFile ("Binding.Main.g.cs") {
					{"public", "Button", "myButton"},
					{"public", "CommonSampleLibrary.LogFragment", "log_fragment"},
					{"public", "global::Android.App.Fragment", "secondary_log_fragment"},
					{"public", "CommonSampleLibrary.LogFragment", "tertiary_log_fragment"},
				},
				new SourceFile ("Binding.MainMerge.g.cs") {
					{"public", "Button", "myButton"},
					{"public", "CommonSampleLibrary.LogFragment", "log_fragment"},
					{"public", "CommonSampleLibrary.LogFragment", "secondary_log_fragment"},
				},
				new SourceFile ("Binding.onboarding_info.g.cs") {
					{"public", "LinearLayout", "onboarding_stations_info_inner"},
					{"public", "ImageView", "icon_view"},
					{"public", "TextView", "intro_highlighted_text"},
					{"public", "TextView", "intro_primary_text"},
				},
				new SourceFile ("Binding.onboarding_intro.g.cs") {
					{"public", "LinearLayout", "onboarding_intro_View"},
					{"public", "TextView", "title"},
					{"public", "TextView", "welcome"},
					{"public", "global::Android.Views.View", "different_view_types"},
					{"public", "global::Android.Views.View", "onboarding_info"},
					{"public", "TextView", "intro_highlighted_text"},
					{"public", "TextView", "intro_primary_text"},
					{"public", "TextView", "intro_secondary_text"},
					{"public", "RelativeLayout", "more_info"},
					{"public", "TextView", "more_highlighted_text"},
					{"public", "TextView", "more_intro_primary_text"},
					{"public", "TextView", "more_intro_secondary_text"},

				},
				new SourceFile ("Binding.settings.g.cs") {
					{"public", "ScrollView", "settings_container"},
					{"public", "TextView", "title"},
					{"public", "TextView", "account_type"},
					{"public", "TextView", "account_type_subtitle"},
					{"public", "TextView", "account_email"},
					{"public", "Button", "subscribe_button"},
					{"public", "TextView", "stream_quality_item_title"},
				},
				new SourceFile ("Xamarin.Android.Tests.CodeBehindBuildTests.AnotherMainActivity.Main.g.cs") {
					// Properties
					{"public", "Button", "myButton"},
					{"public", "CommonSampleLibrary.LogFragment", "log_fragment"},
					{"public", "global::Android.App.Fragment", "secondary_log_fragment"},
					{"public", "CommonSampleLibrary.LogFragment", "tertiary_log_fragment"},

					// Methods
					{"public override", "void", "SetContentView", "global::Android.Views.View view"},
					{"public override", "void", "SetContentView", "global::Android.Views.View view, global::Android.Views.ViewGroup.LayoutParams @params"},
					{"public override", "void", "SetContentView", "int layoutResID"},
					{"partial", "void", "OnSetContentView", "global::Android.Views.View view, ref bool callBaseAfterReturn"},
					{"partial", "void", "OnSetContentView", "global::Android.Views.View view, global::Android.Views.ViewGroup.LayoutParams @params, ref bool callBaseAfterReturn"},
					{"partial", "void", "OnSetContentView", "int layoutResID, ref bool callBaseAfterReturn"},
				},
				new SourceFile ("Xamarin.Android.Tests.CodeBehindBuildTests.MainActivity.Main.g.cs") {
					// Properties
					{"public", "Button", "myButton"},
					{"public", "CommonSampleLibrary.LogFragment", "log_fragment"},
					{"public", "global::Android.App.Fragment", "secondary_log_fragment"},
					{"public", "CommonSampleLibrary.LogFragment", "tertiary_log_fragment"},

					// Methods
					{"public override", "void", "SetContentView", "global::Android.Views.View view"},
					{"public override", "void", "SetContentView", "global::Android.Views.View view, global::Android.Views.ViewGroup.LayoutParams @params"},
					{"public override", "void", "SetContentView", "int layoutResID"},
					{"partial", "void", "OnSetContentView", "global::Android.Views.View view, ref bool callBaseAfterReturn"},
					{"partial", "void", "OnSetContentView", "global::Android.Views.View view, global::Android.Views.ViewGroup.LayoutParams @params, ref bool callBaseAfterReturn"},
					{"partial", "void", "OnSetContentView", "int layoutResID, ref bool callBaseAfterReturn"},
				},
				new SourceFile ("Xamarin.Android.Tests.CodeBehindBuildTests.MainMergeActivity.MainMerge.g.cs") {
					// Properties
					{"public", "Button", "myButton"},
					{"public", "CommonSampleLibrary.LogFragment", "log_fragment"},
					{"public", "CommonSampleLibrary.LogFragment", "secondary_log_fragment"},

					// Methods
					{"public override", "void", "SetContentView", "global::Android.Views.View view"},
					{"public override", "void", "SetContentView", "global::Android.Views.View view, global::Android.Views.ViewGroup.LayoutParams @params"},
					{"public override", "void", "SetContentView", "int layoutResID"},
					{"partial", "void", "OnSetContentView", "global::Android.Views.View view, ref bool callBaseAfterReturn"},
					{"partial", "void", "OnSetContentView", "global::Android.Views.View view, global::Android.Views.ViewGroup.LayoutParams @params, ref bool callBaseAfterReturn"},
					{"partial", "void", "OnSetContentView", "int layoutResID, ref bool callBaseAfterReturn"},
				},
				new SourceFile ("Xamarin.Android.Tests.CodeBehindBuildTests.OnboardingActivityPartial.onboarding_intro.g.cs") {
					// Properties
					{"public", "LinearLayout", "onboarding_intro_View"},
					{"public", "TextView", "title"},
					{"public", "TextView", "welcome"},
					{"public", "global::Android.Views.View", "different_view_types"},
					{"public", "global::Android.Views.View", "onboarding_info"},
					{"public", "TextView", "intro_highlighted_text"},
					{"public", "TextView", "intro_primary_text"},
					{"public", "TextView", "intro_secondary_text"},
					{"public", "RelativeLayout", "more_info"},
					{"public", "TextView", "more_highlighted_text"},
					{"public", "TextView", "more_intro_primary_text"},
					{"public", "TextView", "more_intro_secondary_text"},

					// Methods
					{"public override", "void", "SetContentView", "global::Android.Views.View view"},
					{"public override", "void", "SetContentView", "global::Android.Views.View view, global::Android.Views.ViewGroup.LayoutParams @params"},
					{"public override", "void", "SetContentView", "int layoutResID"},
					{"partial", "void", "OnSetContentView", "global::Android.Views.View view, ref bool callBaseAfterReturn"},
					{"partial", "void", "OnSetContentView", "global::Android.Views.View view, global::Android.Views.ViewGroup.LayoutParams @params, ref bool callBaseAfterReturn"},
					{"partial", "void", "OnSetContentView", "int layoutResID, ref bool callBaseAfterReturn"},
				},
			};

			// TODO: for some reason the tests no longer produce both aab and apk packages, at least locally? Fine? Bug?
			produced_binaries = new List <string> {
				$"{ProjectName}.dll",
				"CommonSampleLibrary.dll",
//				$"{PackageName}-Signed.apk",
				$"{PackageName}.aab",
				$"{PackageName}-Signed.aab",
			};
		}

		[Test]
		public void SuccessfulBuildFew ([Values] AndroidRuntime runtime)
		{
			RunTest (TestName, many: false, dtb: false, runner: SuccessfulBuild_RunTest, runtime: runtime);
		}

		[Test]
		public void SuccessfulBuildMany ([Values] AndroidRuntime runtime)
		{
			RunTest (TestName, many: true, dtb: false, runner: SuccessfulBuild_RunTest, runtime: runtime);
		}

		[Test]
		public void SuccessfulBuildFew_DTB ([Values] AndroidRuntime runtime)
		{
			RunTest (TestName, many: false, dtb: true, runner: SuccessfulBuild_RunTest, runtime: runtime);
		}

		[Test]
		public void SuccessfulBuildMany_DTB ([Values] AndroidRuntime runtime)
		{
			RunTest (TestName, many: true, dtb: true, runner: SuccessfulBuild_RunTest, runtime: runtime);
		}

		void SuccessfulBuild_RunTest (TestProjectInfo testInfo, bool many, bool dtb, LocalBuilder builder)
		{
			string[] parameters = GetBuildProperties (builder, many, dtb, referenceAndroidX:false);
			bool success = builder.Build (testInfo.ProjectPath, GetBuildTarget (dtb), parameters);

			CopyLogs (testInfo, true);
			Assert.That (success, Is.True, "Build should have succeeded");

			CopyGeneratedFiles (testInfo);

			foreach (SourceFile src in generated_sources) {
				foreach (SourceFileMember member in src) {
					string generatedFile = Path.Combine (testInfo.GeneratedPath, src.Path);
					if (member.IsMethod)
						Assert.That (SourceHasMethod (generatedFile, member.Visibility, member.Type, member.Name, member.Arguments), Is.True, $"Method {member.Name} must exist in {generatedFile}");
					else
						Assert.That (SourceHasProperty (generatedFile, member.Visibility, member.Type, member.Name, member.IsExpressionBody), Is.True, $"Property {member.Name} must exist in {generatedFile}");
				}
			}

			if (dtb)
				return; // DTB doesn't produce binaries

			foreach (string binaryName in produced_binaries)
				AssertExists (testInfo.TestName, Path.Combine (testInfo.BinPath, binaryName));
		}

		[Test]
		public void SuccessfulAndroidXApp ([Values] AndroidRuntime runtime)
		{
			RunTest (TestName, many: true, dtb: false, runner: SuccessfulBuild_AndroidX, runtime: runtime);
		}

		void SuccessfulBuild_AndroidX (TestProjectInfo testInfo, bool many, bool dtb, LocalBuilder builder)
		{
			string[] parameters = GetBuildProperties (builder, many, dtb, referenceAndroidX:true, "__HAVE_ANDROIDX__");
			bool success = builder.Build (testInfo.ProjectPath, GetBuildTarget (dtb), parameters);

			CopyLogs (testInfo, true);
			Assert.That (success, Is.True, "Build should have succeeded");
			if (testInfo.Runtime != AndroidRuntime.NativeAOT) { // NativeAOT currently (Nov 2025) produces 9 ILC warnings
				Assert.IsTrue (StringAssertEx.ContainsText (builder.LastBuildOutput, " 0 Warning(s)"), $"{builder.BuildLogFile} should have no MSBuild warnings.");
			}

			CopyGeneratedFiles (testInfo);

			foreach (SourceFile src in generated_sources) {
				foreach (SourceFileMember member in src) {
					string type = member.Type.Replace ("global::Android.App.Fragment", "global::AndroidX.Fragment.App.Fragment");
					string generatedFile = Path.Combine (testInfo.GeneratedPath, src.Path);
					if (member.IsMethod)
						Assert.That (SourceHasMethod (generatedFile, member.Visibility, type, member.Name, member.Arguments), Is.True, $"Method {member.Name} must exist in {generatedFile}");
					else
						Assert.That (SourceHasProperty (generatedFile, member.Visibility, type, member.Name, member.IsExpressionBody), Is.True, $"Property {member.Name} must exist in {generatedFile}");
				}
			}

			if (dtb)
				return; // DTB doesn't produce binaries

			foreach (string binaryName in produced_binaries)
				AssertExists (testInfo.TestName, Path.Combine (testInfo.BinPath, binaryName));
		}

		[Test]
		public void FailedBuildFew_ConflictingFragment ([Values] AndroidRuntime runtime)
		{
			RunTest (TestName, many: false, dtb: false, runner: FailedBuild_ConflictingFragment_RunTest, runtime: runtime);
		}

		[Test]
		public void FailedBuildMany_ConflictingFragment ([Values] AndroidRuntime runtime)
		{
			RunTest (TestName, many: true, dtb: false, runner: FailedBuild_ConflictingFragment_RunTest, runtime: runtime);
		}

		void FailedBuild_ConflictingFragment_RunTest (TestProjectInfo testInfo, bool many, bool dtb, LocalBuilder builder)
		{
			string[] parameters = GetBuildProperties (builder, many, dtb, referenceAndroidX:false, "NOT_CONFLICTING_FRAGMENT");
			bool success = builder.Build (testInfo.ProjectPath, GetBuildTarget (dtb), parameters);

			CopyLogs (testInfo, true);
			Assert.That (success, Is.False, "Build should have failed");

			string logPath = GetTestLogPath (testInfo);
			bool haveError = HaveCompilerError_CS0266 (logPath, "MainActivity.cs", 26, "Android.App.Fragment", "CommonSampleLibrary.LogFragment");
			AssertHaveCompilerError (haveError, "MainActivity.cs", 26);

			haveError = HaveCompilerError_CS0266 (logPath, "AnotherMainActivity.cs", 23, "Android.App.Fragment", "CommonSampleLibrary.LogFragment");
			AssertHaveCompilerError (haveError, "AnotherMainActivity.cs", 23);
		}

		[Test]
		public void FailedBuildFew_ConflictingTextView ([Values] AndroidRuntime runtime)
		{
			RunTest (TestName, many: false, dtb: false, runner: FailedBuild_ConflictingTextView_RunTest, runtime: runtime);
		}

		[Test]
		public void FailedBuildMany_ConflictingTextView ([Values] AndroidRuntime runtime)
		{
			RunTest (TestName, many: true, dtb: false, runner: FailedBuild_ConflictingTextView_RunTest, runtime: runtime);
		}

		void FailedBuild_ConflictingTextView_RunTest (TestProjectInfo testInfo, bool many, bool dtb, LocalBuilder builder)
		{
			string[] parameters = GetBuildProperties (builder, many, dtb, referenceAndroidX:false, "NOT_CONFLICTING_TEXTVIEW");
			bool success = builder.Build (testInfo.ProjectPath, GetBuildTarget (dtb), parameters);

			CopyLogs (testInfo, true);
			Assert.That (success, Is.False, "Build should have failed");

			string logPath = GetTestLogPath (testInfo);
			bool haveError = HaveCompilerError_CS0266 (logPath, "OnboardingActivityPartial.cs", 32, "Android.Views.View", "Android.Widget.TextView");
			AssertHaveCompilerError (haveError, "OnboardingActivityPartial.cs", 32);

			haveError = HaveCompilerError_CS0266 (logPath, "OnboardingActivity.cs", 27, "Android.Views.View", "Android.Widget.TextView");
			AssertHaveCompilerError (haveError, "OnboardingActivity.cs", 27);
		}

		[Test]
		public void FailedBuildFew_ConflictingButton ([Values] AndroidRuntime runtime)
		{
			RunTest (TestName, many: false, dtb: false, runner: FailedBuild_ConflictingButton_RunTest, runtime: runtime);
		}

		[Test]
		public void FailedBuildMany_ConflictingButton ([Values] AndroidRuntime runtime)
		{
			RunTest (TestName, many: true, dtb: false, runner: FailedBuild_ConflictingButton_RunTest, runtime: runtime);
		}

		void FailedBuild_ConflictingButton_RunTest (TestProjectInfo testInfo, bool many, bool dtb, LocalBuilder builder)
		{
			string[] parameters = GetBuildProperties (builder, many, dtb, referenceAndroidX:false, "NOT_CONFLICTING_BUTTON");
			bool success = builder.Build (testInfo.ProjectPath, GetBuildTarget (dtb), parameters);

			CopyLogs (testInfo, true);
			Assert.That (success, Is.False, "Build should have failed");

			string logPath = GetTestLogPath (testInfo);
			bool haveError = HaveCompilerError_CS0266 (logPath, "OnboardingActivityPartial.cs", 34, "Android.Views.View", "Android.Widget.Button");
			AssertHaveCompilerError (haveError, "OnboardingActivityPartial.cs", 34);

			haveError = HaveCompilerError_CS0266 (logPath, "OnboardingActivity.cs", 29, "Android.Views.View", "Android.Widget.Button");
			AssertHaveCompilerError (haveError, "OnboardingActivity.cs", 29);
		}

		[Test]
		public void FailedBuildFew_ConflictingLinearLayout ([Values] AndroidRuntime runtime)
		{
			RunTest (TestName, many: false, dtb: false, runner: FailedBuild_ConflictingLinearLayout_RunTest, runtime: runtime);
		}

		[Test]
		public void FailedBuildMany_ConflictingLinearLayout ([Values] AndroidRuntime runtime)
		{
			RunTest (TestName, many: true, dtb: false, runner: FailedBuild_ConflictingLinearLayout_RunTest, runtime: runtime);
		}

		void FailedBuild_ConflictingLinearLayout_RunTest (TestProjectInfo testInfo, bool many, bool dtb, LocalBuilder builder)
		{
			string[] parameters = GetBuildProperties (builder, many, dtb, referenceAndroidX:false, "NOT_CONFLICTING_LINEARLAYOUT");
			bool success = builder.Build (testInfo.ProjectPath, GetBuildTarget (dtb), parameters);

			CopyLogs (testInfo, true);
			Assert.That (success, Is.False, "Build should have failed");

			string logPath = GetTestLogPath (testInfo);
			bool haveError = HaveCompilerError_CS0266 (logPath, "OnboardingActivityPartial.cs", 41, "Android.Views.View", "Android.Widget.LinearLayout");
			AssertHaveCompilerError (haveError, "OnboardingActivityPartial.cs", 41);

			haveError = HaveCompilerError_CS0266 (logPath, "OnboardingActivity.cs", 36, "Android.Views.View", "Android.Widget.LinearLayout");
			AssertHaveCompilerError (haveError, "OnboardingActivity.cs", 36);
		}

		[Test]
		public void FailedBuildFew_ConflictingRelativeLayout ([Values] AndroidRuntime runtime)
		{
			RunTest (TestName, many: false, dtb: false, runner: FailedBuild_ConflictingRelativeLayout_RunTest, runtime: runtime);
		}

		[Test]
		public void FailedBuildMany_ConflictingRelativeLayout ([Values] AndroidRuntime runtime)
		{
			RunTest (TestName, many: true, dtb: false, runner: FailedBuild_ConflictingRelativeLayout_RunTest, runtime: runtime);
		}

		void FailedBuild_ConflictingRelativeLayout_RunTest (TestProjectInfo testInfo, bool many, bool dtb, LocalBuilder builder)
		{
			string[] parameters = GetBuildProperties (builder, many, dtb, referenceAndroidX:false, "NOT_CONFLICTING_RELATIVELAYOUT");
			bool success = builder.Build (testInfo.ProjectPath, GetBuildTarget (dtb), parameters);

			CopyLogs (testInfo, true);
			Assert.That (success, Is.False, "Build should have failed");

			string logPath = GetTestLogPath (testInfo);
			bool haveError = HaveCompilerError_CS0266 (logPath, "OnboardingActivityPartial.cs", 43, "Android.Views.View", "Android.Widget.RelativeLayout");
			AssertHaveCompilerError (haveError, "OnboardingActivityPartial.cs", 43);

			haveError = HaveCompilerError_CS0266 (logPath, "OnboardingActivity.cs", 38, "Android.Views.View", "Android.Widget.RelativeLayout");
			AssertHaveCompilerError (haveError, "OnboardingActivity.cs", 38);
		}

		string GetBuildTarget (bool isDTB)
		{
			return isDTB ? "Compile" : "SignAndroidPackage";
		}

		string[] GetBuildProperties (LocalBuilder builder, bool manyBuild, bool dtbBuild, bool referenceAndroidX, params string[] extraConstants)
		{
			var ret = new List <string> {
				"AndroidGenerateLayoutBindings=true",
				"\"NoWarn=CS0414;CA1416;CS1591;XA1005;XA4225\""
			};
			if (manyBuild)
				ret.Add ("ForceParallelBuild=true");

			if (dtbBuild) {
				ret.Add ("DesignTimeBuild=True");
				ret.Add ("BuildingInsideVisualStudio=True");
				ret.Add ("SkipCompilerExecution=True");
				ret.Add ("ProvideCommandLineArgs=True");
			}
			if (referenceAndroidX) {
				ret.Add ("ReferenceAndroidX=True");
			}

			if (extraConstants != null && extraConstants.Length > 0) {
				string extras = String.Join (";", extraConstants);
				ret.Add ($"ExtraConstants={extras}");
			}

			ret.Add ($"Configuration={XABuildPaths.Configuration}");

			return ret.ToArray ();
		}

		void AssertHaveCompilerError (bool haveError, string fileName, int line)
		{
			Assert.That (haveError, Is.True, $"Expected compiler error CS0266 in {fileName}({line},?)");
		}

		/// <summary>
		/// Main “driver” method of all the tests. Contains common housekeeping code. All tests must call it.
		/// </summary>
		/// <remarks>
		///   <para>
		///     The <paramref name="runner"/> parameter is an action which is passed the following arguments, in
		///     the order of declaration:
		///   </para>
		///   <para>
		///     <list type="bullet">
		///        <item>
		///          <term>TestProjectInfo testInfo</term>
		///          <description>Information about the location of all the project files and directories</description>
		///        </item>
		///
		///        <item>
		///          <term>bool many</term>
		///          <description>whether the test is building more than 20 layouts to test parallel builds</description>
		///        </item>
		///
		///        <item>
		///          <term>bool dtb</term>
		///          <description>whether the test is running a design time build (DTB)</description>
		///        </item>
		///
		///        <item>
		///          <term>LocalBuilder builder</term>
		///          <description>the object that runs the actual build session</description>
		///        </item>
		///     </list>
		///   </para>
		/// </remarks>
		/// <param name="testName">Name of the test being run. Must be unique</param>
		/// <param name="many">Generate code in parallel if <c>true</c>, serially otherwise</param>
		/// <param name="dtb">Test design-time build if <c>true</c>, regular build otherwise</param>
		/// <param name="runner">Action consituting the main body of the test. Passed parameters are described above in the remarks section.</param>
		void RunTest (string testName, bool many, bool dtb, Action<TestProjectInfo, bool, bool, LocalBuilder> runner, AndroidRuntime runtime)
		{
			bool isRelease = runtime == AndroidRuntime.NativeAOT;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			string temporaryProjectDir = PrepareProject (testName, isRelease, runtime);
			LocalBuilder builder = GetBuilder ($"{ProjectName}.{testName}");
			builder.BuildingInsideVisualStudio = dtb;
			var testInfo = new TestProjectInfo (ProjectName, testName, temporaryProjectDir, TestOutputDir, runtime);

			try {
				runner (testInfo, many, dtb, builder);

				if (many) {
					Assert.That (WasParsedInParallel (testInfo), Is.True, "Should have been parsed in parallel");
					Assert.That (WasGeneratedInParallel (testInfo), Is.True, "Should have been generated in parallel");
				} else {
					Assert.That (WasParsedInParallel (testInfo), Is.False, "Should have been parsed in serial manner");
					Assert.That (WasGeneratedInParallel (testInfo), Is.False, "Should have been generated in serial manner");
				}
			} catch {
				CopyLogs (testInfo, false);
				foreach (var file in Directory.GetFiles (testInfo.OutputDirectory, "*.*log", SearchOption.AllDirectories)) {
					TestContext.AddTestAttachment (file);
				}
				throw;
			}

			// Clean up successful tests
			FileSystemUtils.SetDirectoryWriteable (testInfo.OutputDirectory);
			Directory.Delete (testInfo.TestRootDirectory, recursive: true);
		}

		bool WasParsedInParallel (TestProjectInfo testInfo)
		{
			var regex = new Regex ($"^\\s*Parsing layouts in parallel.*$", RegexOptions.Compiled);
			return FileMatches (regex, GetTestLogPath (testInfo));
		}

		bool WasGeneratedInParallel (TestProjectInfo testInfo)
		{
			var regex = new Regex ($"^\\s*Generating binding code in parallel.*$", RegexOptions.Compiled);
			return FileMatches (regex, GetTestLogPath (testInfo));
		}

		bool HaveCompilerError_CS0266 (string logFile, string sourceFile, int line, string typeFrom, string typeTo)
		{
			var regex = new Regex ($"{Regexify(sourceFile)}\\({line},\\d+\\): [^\\s]+ CS0266:[^']+'{Regexify(typeFrom)}'[^']+'{Regexify(typeTo)}'.*$", RegexOptions.Compiled);
			return FileMatches (regex, logFile);
		}

		bool SourceHasProperty (string sourceFile, string visibility, string typeName, string propertyName, bool isExpression = true)
		{
			return SourceHasMember (sourceFile, visibility, typeName, propertyName, false, null, isExpression);
		}

		bool SourceHasMethod (string sourceFile, string visibility, string typeName, string methodName, string arguments)
		{
			return SourceHasMember (sourceFile, visibility, typeName, methodName, true, arguments, false);
		}

		bool SourceHasMember (string sourceFile, string visibility, string typeName, string memberName, bool isMethod, string arguments, bool isExpression)
		{
			Regex regex;

			if (isMethod) {
				regex = new Regex ($"^\\s+{visibility}\\s+{Regexify(typeName)}\\s+{Regexify(memberName)}\\s+\\({Regexify(arguments)}\\).*$");
			} else {
				string propertyCodeStart = isExpression ? "=>" : "{";
				regex = new Regex ($"^\\s+{visibility}\\s+{Regexify(typeName)}\\s+{Regexify(memberName)}\\s+{propertyCodeStart}.*$", RegexOptions.Compiled);
			}
			return FileMatches (regex, sourceFile);
		}

		bool FileMatches (Regex regex, string inputFile)
		{
			using (var sr = new StreamReader (inputFile)) {
				string text;
				while ((text = sr.ReadLine ()) != null) {
					if (regex.IsMatch (text))
						return true;
				}
			}

			return false;
		}

		string Regexify (string input)
		{
			if (input == null)
				throw new ArgumentNullException (nameof (input));
			return input
				.Replace (".", "\\.")
				.Replace ("(", "\\(")
				.Replace (")", "\\)")
				.Replace ("[", "\\[")
				.Replace ("]", "\\]")
				.Replace ("{", "\\{")
				.Replace ("}", "\\}");
		}

		string PrepareProject (string testName, bool isRelease, AndroidRuntime runtime)
		{
			string tempRoot = Path.Combine (TestOutputDir, testName, XABuildPaths.Configuration);
			string temporaryProjectPath = Path.Combine (tempRoot, "project");

			var ignore = new HashSet <string> {
				Path.Combine (TestProjectRootDirectory, "bin"),
				Path.Combine (TestProjectRootDirectory, "obj"),
				Path.Combine (CommonSampleLibraryRootDirectory, "bin"),
				Path.Combine (CommonSampleLibraryRootDirectory, "obj"),
			};

			CopyRecursively (TestProjectRootDirectory, temporaryProjectPath, ignore);
			CopyRecursively (CommonSampleLibraryRootDirectory, Path.Combine (tempRoot, CommonSampleLibraryName), ignore);
			CopyFile (Path.Combine (XABuildPaths.TopDirectory, "Directory.Build.props"), Path.Combine (tempRoot, "Directory.Build.props" ));
			var project = new XamarinAndroidApplicationProject ();
			project.CopyNuGetConfig (Path.Combine (tempRoot, "NuGet.config"));

			PatchProject (Path.Combine (tempRoot, CommonSampleLibraryName, $"{CommonSampleLibraryName}.NET.csproj"));
			PatchProject (Path.Combine (temporaryProjectPath, $"{ProjectName}.csproj"));

			return temporaryProjectPath;

			void PatchProject (string csprojPath)
			{
				AssertExists (testName, csprojPath);
				var sb = new StringBuilder (File.ReadAllText (csprojPath));
				sb.Replace ("@UseMonoRuntime@", (runtime == AndroidRuntime.MonoVM).ToString ())
				  .Replace ("@PublishAot@", (runtime == AndroidRuntime.NativeAOT).ToString ())
				  .Replace ("@Configuration@", isRelease ? "Release" : "Debug");
				File.WriteAllText (csprojPath, sb.ToString ());
			}
		}

		void CopyRecursively (string fromDir, string toDir, HashSet <string> ignoreDirs)
		{
			if (String.IsNullOrEmpty (fromDir))
				throw new ArgumentException ($"{nameof (fromDir)} is must have a non-empty value");
			if (String.IsNullOrEmpty (toDir))
				throw new ArgumentException ($"{nameof (toDir)} is must have a non-empty value");

			if (ignoreDirs.Contains (fromDir))
				return;

			var fdi = new DirectoryInfo (fromDir);
			if (!fdi.Exists)
				throw new InvalidOperationException ($"Source directory '{fromDir}' does not exist");

			if (Directory.Exists (toDir))
				Directory.Delete (toDir, true);

			foreach (FileSystemInfo fsi in fdi.EnumerateFileSystemInfos ("*", SearchOption.TopDirectoryOnly)) {
				if (fsi is FileInfo finfo)
					CopyFile (fsi.FullName, Path.Combine (toDir, finfo.Name));
				else
					CopyRecursively (fsi.FullName, Path.Combine (toDir, fsi.Name), ignoreDirs);
			}
		}

		void CopyFile (string from, string to)
		{
			string dir = Path.GetDirectoryName (to);
			if (!Directory.Exists (dir))
				Directory.CreateDirectory (dir);
			File.Copy (from, to, true);
		}

		LocalBuilder GetBuilder (string baseLogFileName)
		{
			return new LocalBuilder {
				BuildLogFile = $"{baseLogFileName}.log"
			};
		}

		void CopyLogs (TestProjectInfo testInfo, bool assert)
		{
			AssertExistsAndCopy (testInfo, GetTestLogName (testInfo), testInfo.OutputDirectory, assert);
			foreach (string log in log_files) {
				AssertExistsAndCopy (testInfo, log, testInfo.OutputDirectory, assert);
			}
		}

		string GetTestLogName (TestProjectInfo testInfo)
		{
			return $"{testInfo.ProjectName}.{testInfo.TestName}.log";
		}

		string GetTestLogPath (TestProjectInfo testInfo)
		{
			return Path.Combine (testInfo.RootDirectory, GetTestLogName (testInfo));
		}

		string GetTemporaryProjectDir (string testName)
		{
			return Path.Combine (TestOutputDir, $"{testName}_Project", XABuildPaths.Configuration);
		}

		void CopyGeneratedFiles (TestProjectInfo testInfo)
		{
			string destDir = Path.Combine (testInfo.OutputDirectory, "generated");

			foreach (SourceFile src in generated_sources) {
				AssertExistsAndCopy (testInfo, Path.Combine (testInfo.GeneratedPath, src.Path), destDir);
			}
		}

		void AssertExistsAndCopy (TestProjectInfo testInfo, string relativeFilePath, string destDir, bool nunitAssert = true)
		{
			string source = Path.Combine (testInfo.RootDirectory, relativeFilePath);
			if (nunitAssert)
				AssertExists (testInfo.TestName, source);
			else if (!File.Exists (source))
				return;

			if (!Directory.Exists (destDir))
				Directory.CreateDirectory (destDir);
			string destination = Path.Combine (destDir, Path.GetFileName (relativeFilePath));
			File.Copy (source, destination, true);
		}

		void AssertExists (string testName, string filePath)
		{
			Assert.That (new FileInfo (filePath), Does.Exist, $"file {filePath} should exist for test '{testName}'");
		}
	}
}
