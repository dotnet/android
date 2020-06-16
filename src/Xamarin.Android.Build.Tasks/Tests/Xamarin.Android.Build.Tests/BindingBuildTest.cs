﻿using Mono.Cecil;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[Category ("Node-3")]
	[Parallelizable (ParallelScope.Children)]
	public class BindingBuildTest : BaseTest {
#pragma warning disable 414
		static object [] ClassParseOptions = new object [] {
			new object[] {
				/* classParser */   "class-parse",
				},
		};

		[Test]
		[TestCaseSource (nameof (ClassParseOptions))]
		[Category ("SmokeTests")]
		public void BuildBasicBindingLibrary (string classParser)
		{
			var targets = new List<string> {
				"_ExtractJavaDocJars",
				"_ExportJarToXml",
				"GenerateBindings",
				"_CreateBindingResourceArchive",
				"BuildDocumentation",
				"_ResolveLibraryProjectImports",
				"CoreCompile",
			};

			var proj = new XamarinAndroidBindingProject () {
				IsRelease = true,
			};
			proj.Jars.Add (new AndroidItem.EmbeddedJar ("Jars\\svg-android.jar") {
				WebContent = "https://storage.googleapis.com/google-code-archive-downloads/v2/code.google.com/svg-android/svg-android.jar"
			});
			proj.AndroidClassParser = classParser;
			using (var b = CreateDllBuilder (Path.Combine ("temp", TestName))) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");

				//A list of properties we check exist in binding projects
				var properties = new [] {
					"AndroidSdkBuildToolsVersion",
					"AndroidSdkPlatformToolsVersion",
					"AndroidSdkToolsVersion",
					"AndroidNdkVersion",
				};
				foreach (var property in properties) {
					Assert.IsTrue (StringAssertEx.ContainsText (b.LastBuildOutput, property + " = "), $"$({property}) should be set!");
				}

				Assert.IsTrue (b.Build (proj, doNotCleanupOnUpdate: true, saveProject: false), "second build should succeed");
				foreach (var target in targets) {
					Assert.IsTrue (b.Output.IsTargetSkipped (target), $"`{target}` should be skipped on second build!");
				}
			}
		}

		[Test]
		[TestCaseSource (nameof (ClassParseOptions))]
		public void CleanBasicBindingLibrary (string classParser)
		{
			var proj = new XamarinAndroidBindingProject () {
				IsRelease = true,
			};
			proj.Jars.Add (new AndroidItem.EmbeddedJar ("Jars\\svg-android.jar") {
				WebContent = "https://storage.googleapis.com/google-code-archive-downloads/v2/code.google.com/svg-android/svg-android.jar"
			});
			proj.AndroidClassParser = classParser;
			using (var b = CreateDllBuilder (Path.Combine ("temp", TestName))) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				Assert.IsTrue (b.Clean (proj), "Clean should have succeeded");
				var ignoreFiles = new string [] {
					"TemporaryGeneratedFile",
					"FileListAbsolute.txt",
				};
				var files = Directory.GetFiles (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath), "*", SearchOption.AllDirectories)
					.Where (x => !ignoreFiles.Any (i => !Path.GetFileName (x).Contains (i)));
				Assert.AreEqual (0, Directory.GetDirectories (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath), "*", SearchOption.AllDirectories).Length,
					"All directories in {0} should have been removed", proj.IntermediateOutputPath);
				Assert.AreEqual (0, files.Count (), "{0} should be empty. Found {1}",
					proj.IntermediateOutputPath, string.Join (Environment.NewLine, files));
			}
		}

		[Test]
		[TestCaseSource (nameof (ClassParseOptions))]
		public void BuildAarBindigLibraryStandalone (string classParser)
		{
			var proj = new XamarinAndroidBindingProject () {
				UseLatestPlatformSdk = true,
				IsRelease = true,
			};
			proj.Jars.Add (new AndroidItem.LibraryProjectZip ("Jars\\material-menu-1.1.0.aar") {
				WebContent = "https://repo.jfrog.org/artifactory/libs-release-bintray/com/balysv/material-menu/1.1.0/material-menu-1.1.0.aar"
			});
			proj.AndroidClassParser = classParser;
			var b = CreateDllBuilder (Path.Combine ("temp", TestName));
			Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			b.Dispose ();

		}

		[Test]
		[TestCaseSource (nameof (ClassParseOptions))]
		public void BuildAarBindigLibraryWithNuGetPackageOfJar (string classParser)
		{
			var proj = new XamarinAndroidBindingProject () {
				UseLatestPlatformSdk = true,
				IsRelease = true,
			};
			proj.PackageReferences.Add (KnownPackages.AndroidSupportV4_27_0_2_1);
			proj.Jars.Add (new AndroidItem.LibraryProjectZip ("Jars\\android-crop-1.0.1.aar") {
				WebContent = "https://jcenter.bintray.com/com/soundcloud/android/android-crop/1.0.1/android-crop-1.0.1.aar"
			});
			proj.MetadataXml = @"
				<metadata>
					<attr path=""/api/package[@name='com.soundcloud.android.crop']"" name='managedName'>AndroidCropBinding</attr>
					<attr path=""/api/package[@name='com.soundcloud.android.crop']/class[@name='MonitoredActivity']"" name='visibility'>public</attr>
					<attr path=""/api/package[@name='com.soundcloud.android.crop']/class[@name='ImageViewTouchBase']"" name='visibility'>public</attr>
					<attr path=""/api/package[@name='com.soundcloud.android.crop']/class[@name='RotateBitmap']"" name='visibility'>public</attr>
				</metadata>";
			proj.AndroidClassParser = classParser;
			using (var b = CreateDllBuilder ()) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			}
		}

		[Test]
		[TestCaseSource (nameof (ClassParseOptions))]
		[NonParallelizable]
		[Category ("SmokeTests")]
		public void BuildLibraryZipBindigLibraryWithAarOfJar (string classParser)
		{
			var proj = new XamarinAndroidBindingProject () {
				UseLatestPlatformSdk = true,
				IsRelease = true,
			};
			proj.AndroidClassParser = classParser;
			proj.PackageReferences.Add (KnownPackages.AndroidSupportV4_27_0_2_1);
			proj.Jars.Add (new AndroidItem.LibraryProjectZip ("Jars\\aFileChooserBinaries.zip") {
				WebContentFileNameFromAzure = "aFileChooserBinaries.zip"
			});
			proj.MetadataXml = @"
				<metadata>
					<attr path=""/api/package[@name='com.ipaulpro.afilechooser']/class[@name='FileListAdapter']/method[@name='getItem' and count(parameter)=1 and parameter[1][@type='int']]"" name=""managedReturn"">Java.Lang.Object</attr>
					<attr path=""/api/package[@name='com.ipaulpro.afilechooser']/class[@name='FileLoader']/method[@name='loadInBackground' and count(parameter)=0]"" name=""managedName"">LoadInBackgroundImpl</attr>
				</metadata>";
			proj.Sources.Add (new BuildItem (BuildActions.Compile, "Fixup.cs") {
				TextContent = () => @"using System;
using System.Collections.Generic;
using Android.App;
using Android.Runtime;

namespace Com.Ipaulpro.Afilechooser {
	[Activity (Name = ""com.ipaulpro.afilechooser.FileChooserActivity"",
	           Icon = ""@drawable/ic_chooser"",
	           Exported = true)]
	[IntentFilter (new string [] {""android.intent.action.GET_CONTENT""},
	               Categories = new string [] {
				""android.intent.category.DEFAULT"",
				//""android.intent.category.OPENABLE""
				},
	               DataMimeType = ""*/*"")]
	public partial class FileChooserActivity
	{
	}

	public partial class FileListFragment : global::Android.Support.V4.App.ListFragment, global::Android.Support.V4.App.LoaderManager.ILoaderCallbacks {

		public void OnLoadFinished (global::Android.Support.V4.Content.Loader p0, Java.Lang.Object p1)
		{
			OnLoadFinished (p0, (IList<Java.IO.File>) new JavaList<Java.IO.File> (p1.Handle, JniHandleOwnership.DoNotTransfer));
		}
	}
	public partial class FileLoader : Android.Support.V4.Content.AsyncTaskLoader {
		public override Java.Lang.Object LoadInBackground ()
		{
			return (Java.Lang.Object) LoadInBackgroundImpl ();
		}
	}                                                   
}"
			});
			using (var b = CreateDllBuilder ()) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			}
		}

		[Test]
		[Category ("Minor")]
		public void BindByteArrayInMethodParameter ()
		{
			var proj = new XamarinAndroidBindingProject () {
				IsRelease = true,
				AndroidClassParser = "class-parse",
			};
			proj.Jars.Add (new AndroidItem.EmbeddedJar ("Jars\\svg-android.jar") {
				WebContentFileNameFromAzure = "javaBindingIssue.jar"
			});
			using (var b = CreateDllBuilder ("temp/BindByteArrayInMethodParameter")) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			}
		}

		[Test]
		public void MergeAndroidManifest ()
		{
			var binding = new XamarinAndroidBindingProject () {
				IsRelease = true,
			};
			binding.AndroidClassParser = "class-parse";
			binding.Jars.Add (new AndroidItem.LibraryProjectZip ("Jars\\adal-1.0.7.aar") {
				WebContentFileNameFromAzure = "adal-1.0.7.aar"
			});
			binding.MetadataXml = @"
<metadata>
	<remove-node path=""/api/package/class[@visibility='']"" />
</metadata>";
			using (var bindingBuilder = CreateDllBuilder ("temp/MergeAndroidManifest/AdalBinding")) {
				bindingBuilder.Build (binding);
				var proj = new XamarinAndroidApplicationProject () {
					IsRelease = true,
				};
				proj.OtherBuildItems.Add (new BuildItem ("ProjectReference", "..\\AdalBinding\\UnnamedProject.csproj"));
				using (var b = CreateApkBuilder ("temp/MergeAndroidManifest/App")) {
					b.Verbosity = Microsoft.Build.Framework.LoggerVerbosity.Diagnostic;
					Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
					var manifest = File.ReadAllText (Path.Combine (Root, b.ProjectDirectory, "obj", "Release", "android", "AndroidManifest.xml"));
					Assert.IsTrue (manifest.Contains ("com.microsoft.aad.adal.AuthenticationActivity"), "manifest merge failure");
					Directory.Delete (Path.Combine (Root, "temp", "MergeAndroidManifest"), recursive: true);
				}
			}
		}

		[Test]
		public void AnnotationSupport ()
		{
			// https://trello.com/c/a36dDVS6/37-support-for-annotations-zip
			var binding = new XamarinAndroidBindingProject () {
				IsRelease = true,
			};
			binding.AndroidClassParser = "class-parse";
			binding.Jars.Add (new AndroidItem.LibraryProjectZip ("Jars\\mylibrary.aar") {
				WebContentFileNameFromAzure = "mylibrary-debug.aar"
			});
			var bindingBuilder = CreateDllBuilder ("temp/AnnotationSupport");
			Assert.IsTrue (bindingBuilder.Build (binding), "binding build failed");
			var src = File.ReadAllText (Path.Combine (Root, bindingBuilder.ProjectDirectory, "obj", "Release", "generated", "src", "Com.Example.Atsushi.Mylibrary.AnnotSample.cs"));
			Assert.IsTrue (src.Contains ("IntDef"), "missing IntDef");
			bindingBuilder.Dispose ();
		}

		[Test]
		public void BindingCustomJavaApplicationClass ()
		{
			var binding = new XamarinAndroidBindingProject () {
				IsRelease = true,
			};
			binding.AndroidClassParser = "class-parse";

			using (var bindingBuilder = CreateDllBuilder ("temp/BindingCustomJavaApplicationClass/MultiDexBinding")) {
				string multidexJar = Path.Combine (bindingBuilder.AndroidMSBuildDirectory, "android-support-multidex.jar");
				binding.Jars.Add (new AndroidItem.EmbeddedJar (() => multidexJar));
				bindingBuilder.Build (binding);
				var proj = new XamarinAndroidApplicationProject ();
				proj.OtherBuildItems.Add (new BuildItem ("ProjectReference", "..\\MultiDexBinding\\UnnamedProject.csproj"));
				using (var b = CreateApkBuilder ("temp/BindingCustomJavaApplicationClass/App")) {
					b.Verbosity = Microsoft.Build.Framework.LoggerVerbosity.Diagnostic;
					Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				}
			}
		}

		[Test]
		public void BindngFilterUnsupportedNativeAbiLibraries ()
		{
			var binding = new XamarinAndroidBindingProject () {
				IsRelease = true,
			};
			binding.AndroidClassParser = "class-parse";
			binding.Jars.Add (new AndroidItem.LibraryProjectZip ("Jars\\mylibrary.aar") {
				WebContentFileNameFromAzure = "card.io-5.3.0.aar"
			});
			using (var bindingBuilder = CreateDllBuilder (Path.Combine ("temp", "BindngFilterUnsupportedNativeAbiLibraries", "Binding"))) {
				Assert.IsTrue (bindingBuilder.Build (binding), "binding build should have succeeded");
				var proj = new XamarinAndroidApplicationProject ();
				proj.OtherBuildItems.Add (new BuildItem ("ProjectReference", "..\\Binding\\UnnamedProject.csproj"));
				using (var b = CreateApkBuilder (Path.Combine ("temp", "BindngFilterUnsupportedNativeAbiLibraries", "App"))) {
					Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				}
			}
		}

		[Test]
		public void BindingCheckHiddenFiles ([Values (true, false)] bool useShortFileNames)
		{
			var binding = new XamarinAndroidBindingProject () {
				UseLatestPlatformSdk = true,
				IsRelease = true,
			};
			binding.AndroidClassParser = "class-parse";
			binding.Jars.Add (new AndroidItem.LibraryProjectZip ("Jars\\mylibrary.aar") {
				WebContentFileNameFromAzure = "mylibrary.aar"
			});
			binding.Jars.Add (new AndroidItem.EmbeddedJar ("Jars\\svg-android.jar") {
				WebContentFileNameFromAzure = "javaBindingIssue.jar"
			});
			var path = Path.Combine ("temp", TestContext.CurrentContext.Test.Name);
			binding.SetProperty (binding.ActiveConfigurationProperties, "UseShortFileNames", useShortFileNames);
			using (var bindingBuilder = CreateDllBuilder (Path.Combine (path, "Binding"))) {
				bindingBuilder.Verbosity = Microsoft.Build.Framework.LoggerVerbosity.Diagnostic;
				Assert.IsTrue (bindingBuilder.Build (binding), "binding build should have succeeded");
				var proj = new XamarinAndroidApplicationProject ();
				proj.OtherBuildItems.Add (new BuildItem ("ProjectReference", "..\\Binding\\UnnamedProject.csproj"));
				proj.SetProperty (proj.ActiveConfigurationProperties, "UseShortFileNames", useShortFileNames);
				proj.AndroidManifest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" xmlns:tools=""http://schemas.android.com/tools"" android:versionCode=""1"" android:versionName=""1.0"" package=""{proj.PackageName}"">
	<uses-sdk />
	<application android:label=""{proj.ProjectName}"" tools:replace=""android:label"">
	</application>
</manifest>";
				using (var b = CreateApkBuilder (Path.Combine (path, "App"))) {
					Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
					var assemblyMap = b.Output.GetIntermediaryPath (Path.Combine ("lp", "map.cache"));
					if (useShortFileNames)
						Assert.IsTrue (File.Exists (assemblyMap), $"{assemblyMap} should exist.");
					else
						Assert.IsFalse (File.Exists (assemblyMap), $"{assemblyMap} should not exist.");
					var assemblyIdentityMap = new List<string> ();
					if (useShortFileNames) {
						foreach (var s in File.ReadLines (assemblyMap)) {
							assemblyIdentityMap.Add (s);
						}
					}
					var assmeblyIdentity = useShortFileNames ? assemblyIdentityMap.IndexOf ("UnnamedProject").ToString () : "UnnamedProject";
					var libaryImportsFolder = useShortFileNames ? "lp" : "__library_projects__";
					var jlibs = useShortFileNames ? "jl" : "library_project_imports";
					var dsStorePath = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, libaryImportsFolder,
						assmeblyIdentity, jlibs);
					Assert.IsTrue (Directory.Exists (dsStorePath), "{0} should exist.", dsStorePath);
					Assert.IsFalse (File.Exists (Path.Combine (dsStorePath, ".DS_Store")), "{0} should NOT exist.",
						Path.Combine (dsStorePath, ".DS_Store"));
					var _macOSStorePath = Path.Combine (dsStorePath, "_MACOSX");
					Assert.IsFalse (Directory.Exists (_macOSStorePath), "{0} should NOT exist.", _macOSStorePath);
					var svgJar = Path.Combine (dsStorePath, "svg-android.jar");
					Assert.IsTrue (File.Exists (svgJar), $"{svgJar} should exist.");
				}
			}
		}

		[Test]
		public void BindingDoNotPackage ()
		{
			var binding = new XamarinAndroidBindingProject () {
				IsRelease = true,
				Jars = {
					new AndroidItem.EmbeddedJar ("Jars\\svg-android.jar") {
						WebContentFileNameFromAzure = "javaBindingIssue.jar"
					}
				},
				AssemblyInfo = @"
using Java.Interop;
[assembly:DoNotPackage(""svg-android.jar"")]
			"
			};
			binding.AndroidClassParser = "class-parse";
			using (var bindingBuilder = CreateDllBuilder (Path.Combine ("temp", "BindingDoNotPackage", "Binding"))) {
				Assert.IsTrue (bindingBuilder.Build (binding), "binding build should have succeeded");
				var proj = new XamarinAndroidApplicationProject () {
					ProjectName = "App1",
					OtherBuildItems = {
						new BuildItem ("ProjectReference", "..\\Binding\\UnnamedProject.csproj")
					},
					Sources = {
						new BuildItem.Source ("MyClass.cs") { TextContent = ()=> @"
using System;
using Foo.Bar;

namespace Foo {
	public class MyClass : Java.Lang.Object, IUpdateListener {

		public MyClass ()
		{
			var sub = new Subscriber ();
		}

		public void OnUpdate (Java.Lang.Object p0)
		{
		}
	}
}
						"}
					}
				};
				using (var b = CreateApkBuilder (Path.Combine ("temp", "BindingDoNotPackage", "App"))) {
					Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				}
			}
		}

		[Test]
		public void RemoveEventHandlerResolution ()
		{
			var binding = new XamarinAndroidBindingProject () {
				IsRelease = true,
				UseLatestPlatformSdk = true,
				Jars = {
					new AndroidItem.LibraryProjectZip ("Jars\\ActionBarSherlock-4.3.1.zip") {
						WebContent = "https://github.com/xamarin/monodroid-samples/blob/master/ActionBarSherlock/ActionBarSherlock/Jars/ActionBarSherlock-4.3.1.zip?raw=true"
					}
				},
				AndroidClassParser = "class-parse",
				MetadataXml = @"<metadata>
	<remove-node path=""/api/package[starts-with(@name, 'com.actionbarsherlock.internal')]"" />
	<attr path=""/api/package[@name='com.actionbarsherlock']"" name=""managedName"">Xamarin.ActionbarSherlockBinding</attr>
	<attr path=""/api/package[@name='com.actionbarsherlock.widget']"" name=""managedName"">Xamarin.ActionbarSherlockBinding.Widget</attr>
	<attr path=""/api/package[@name='com.actionbarsherlock.app']"" name=""managedName"">Xamarin.ActionbarSherlockBinding.App</attr>
	<attr path=""/api/package[@name='com.actionbarsherlock.view']"" name=""managedName"">Xamarin.ActionbarSherlockBinding.Views</attr>
</metadata>",
			};
			binding.PackageReferences.Add (KnownPackages.AndroidSupportV4_27_0_2_1);
			using (var bindingBuilder = CreateDllBuilder (Path.Combine ("temp", "RemoveEventHandlerResolution", "Binding"))) {
				Assert.IsTrue (bindingBuilder.Build (binding), "binding build should have succeeded");
			}
		}

		[Test]
		public void JavaSourceJar ()
		{
			var binding = new XamarinAndroidBindingProject () {
				AndroidClassParser = "class-parse",
			};
			binding.SetProperty ("DocumentationFile", "UnnamedProject.xml");
			using (var bindingBuilder = CreateDllBuilder ()) {
				binding.Jars.Add (new AndroidItem.EmbeddedJar ("javasourcejartest.jar") {
					BinaryContent = () => Convert.FromBase64String (InlineData.JavaClassesJarBase64)
				});
				binding.OtherBuildItems.Add (new BuildItem ("JavaSourceJar", "javasourcejartest-sources.jar") {
					BinaryContent = () => Convert.FromBase64String (InlineData.JavaSourcesJarBase64)
				});
				Assert.IsTrue (bindingBuilder.Build (binding), "binding build should have succeeded");
				var jdkVersion = GetJdkVersion ();
				if (jdkVersion > new Version (9, 0)) {
					Assert.Ignore ("JDK 11 and @(JavaSourceJar) don't currently mix.");
					return;
				}
				string xml = bindingBuilder.Output.GetIntermediaryAsText ("docs/Com.Xamarin.Android.Test.Msbuildtest/JavaSourceJarTest.xml");
				Assert.IsTrue (xml.Contains ("<param name=\"name\"> - name to display.</param>"), "missing doc");
			}
		}

		static Version GetJdkVersion ()
		{
			var jdkPath     = AndroidSdkResolver.GetJavaSdkPath ();
			var releasePath = Path.Combine (jdkPath, "release");
			if (!File.Exists (releasePath))
				return null;
			foreach (var line in File.ReadLines (releasePath)) {
				const string JavaVersionStart = "JAVA_VERSION=\"";
				if (!line.StartsWith (JavaVersionStart, StringComparison.OrdinalIgnoreCase))
					continue;
				var value   = line.Substring (JavaVersionStart.Length, line.Length - JavaVersionStart.Length - 1);
				int last    = 0;
				for (last = 0; last < value.Length; ++last) {
					if (char.IsDigit (value, last) || value [last] == '.')
						continue;
					break;
				}
				return Version.Parse (last == value.Length ? value : value.Substring (0, last));
			}
			return null;
		}

		[Test]
		[TestCaseSource (nameof (ClassParseOptions))]
		public void DesignTimeBuild (string classParser)
		{
			var proj = new XamarinAndroidBindingProject {
				AndroidClassParser = classParser
			};
			proj.Jars.Add (new AndroidItem.LibraryProjectZip ("Jars\\material-menu-1.1.0.aar") {
				WebContent = "https://repo.jfrog.org/artifactory/libs-release-bintray/com/balysv/material-menu/1.1.0/material-menu-1.1.0.aar"
			});
			using (var b = CreateDllBuilder (Path.Combine ("temp", TestName))) {
				b.Target = "Compile";
				Assert.IsTrue (b.Build (proj, parameters: new [] { "DesignTimeBuild=True" }), "design-time build should have succeeded.");

				var intermediate = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath);
				var api_xml = Path.Combine (intermediate, "api.xml");
				FileAssert.Exists (api_xml);
				var xml = XDocument.Load (api_xml);
				var element = xml.Element ("api");
				Assert.IsNotNull (element, "api.xml should contain an `api` element!");
				Assert.IsTrue (element.HasElements, "api.xml should contain elements!");

				var assemblyFile = Path.Combine (intermediate, proj.ProjectName + ".dll");
				using (var assembly = AssemblyDefinition.ReadAssembly (assemblyFile)) {
					var typeName = "Com.Balysv.Material.Drawable.Menu.MaterialMenuView";
					Assert.IsTrue (assembly.MainModule.Types.Any (t => t.FullName == typeName), $"Type `{typeName}` should exist!");
				}
			}
		}

		[Test]
		[TestCaseSource (nameof (ClassParseOptions))]
		public void BindDefaultInterfaceMethods (string classParser)
		{
			var proj = new XamarinAndroidBindingProject {
				IsRelease = true,
			};

			// The sources for the .jar is in the jar itself.
			string classesJarBase64 = @"
UEsDBBQACAgIANWk6UwAAAAAAAAAAAAAAAAJAAQATUVUQS1JTkYv/soAAAMAUEsHCAAAAAACAAAAAAA
AAFBLAwQUAAgICADVpOlMAAAAAAAAAAAAAAAAFAAAAE1FVEEtSU5GL01BTklGRVNULk1G803My0xLLS
7RDUstKs7Mz7NSMNQz4OVyLkpNLElN0XWqBAlY6BnEG5obKmj4FyUm56QqOOcXFeQXJZYA1WvycvFyA
QBQSwcIFGFrLUQAAABFAAAAUEsDBAoAAAgAAK2k6UwAAAAAAAAAAAAAAAAEAAAAY29tL1BLAwQKAAAI
AACtpOlMAAAAAAAAAAAAAAAADAAAAGNvbS94YW1hcmluL1BLAwQKAAAIAACwpOlMAAAAAAAAAAAAAAA
AEQAAAGNvbS94YW1hcmluL3Rlc3QvUEsDBBQACAgIAJmk6UwAAAAAAAAAAAAAAAAuAAAAY29tL3hhbW
FyaW4vdGVzdC9EZWZhdWx0SW50ZXJmYWNlTWV0aG9kcy5jbGFzc3WOvU7DMBSFjxsnKeWnXUE8QLrgh
ScAhBSJnwHE7qQ3JVUSC8dGFc/EwsrAA/BQiOuqLKnw8Pn4+LuWv38+vwCcY5biKMVUIKqMYWbzXEBe
mgUJTG/qju58W5B91EXDTbIkd6HtX3jj0G+DzPL5E28v3q8FJg/G25Ku6zB1ekWV9o3LO0e20iXdkns
2i/5spV+1QFaaVq11q23dKUe9U//4ArMwoRrdLdV9saLSJQICI4QVc4ogmTGfThBugFH0zuR/MpNNE7
x015NDL3C868VDL2XuYbL1joMTjI+BNpYC+/xcaA820uEvUEsHCIw1aijpAAAAhQEAAFBLAwQUAAgIC
ACYpOlMAAAAAAAAAAAAAAAAHAAAAERlZmF1bHRJbnRlcmZhY2VNZXRob2RzLmphdmF1zLEOwiAQBuCd
p7hRl0Zd2YyLgw9xwlGJFCocTWPTdxdSHarxxv///utR3bElUKFrRuwwWt8wJZZC9PnqrALrmaJBRXA
ig9nx+RNciG9BJzEJKKeXtnowIcBmCxNE4hw97CTMP6glPmJcuf1f91y5w7cbgtWQ3rCOBnSZymJhNX
nkPJYnUsziBVBLBwgzfz2miQAAAPUAAABQSwECFAAUAAgICADVpOlMAAAAAAIAAAAAAAAACQAEAAAAA
AAAAAAAAAAAAAAATUVUQS1JTkYv/soAAFBLAQIUABQACAgIANWk6UwUYWstRAAAAEUAAAAUAAAAAAAA
AAAAAAAAAD0AAABNRVRBLUlORi9NQU5JRkVTVC5NRlBLAQIKAAoAAAgAAK2k6UwAAAAAAAAAAAAAAAA
EAAAAAAAAAAAAAAAAAMMAAABjb20vUEsBAgoACgAACAAAraTpTAAAAAAAAAAAAAAAAAwAAAAAAAAAAA
AAAAAA5QAAAGNvbS94YW1hcmluL1BLAQIKAAoAAAgAALCk6UwAAAAAAAAAAAAAAAARAAAAAAAAAAAAA
AAAAA8BAABjb20veGFtYXJpbi90ZXN0L1BLAQIUABQACAgIAJmk6UyMNWoo6QAAAIUBAAAuAAAAAAAA
AAAAAAAAAD4BAABjb20veGFtYXJpbi90ZXN0L0RlZmF1bHRJbnRlcmZhY2VNZXRob2RzLmNsYXNzUEs
BAhQAFAAICAgAmKTpTDN/PaaJAAAA9QAAABwAAAAAAAAAAAAAAAAAgwIAAERlZmF1bHRJbnRlcmZhY2
VNZXRob2RzLmphdmFQSwUGAAAAAAcABwDOAQAAVgMAAAAA
";
			proj.Jars.Add (new AndroidItem.EmbeddedJar ("dim.jar") {
				BinaryContent = () => Convert.FromBase64String (classesJarBase64)
			});

			proj.AndroidClassParser = classParser;

			proj.SetProperty ("_EnableInterfaceMembers", "True");
			proj.SetProperty ("LangVersion", "preview");

			using (var b = CreateDllBuilder (Path.Combine ("temp", TestName), false, false)) {
				proj.NuGetRestore (b.ProjectDirectory);
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");

				string asmpath = Path.GetFullPath (Path.Combine (Path.GetDirectoryName (new Uri (GetType ().Assembly.CodeBase).LocalPath), b.ProjectDirectory, b.Output.OutputPath, (proj.AssemblyName ?? proj.ProjectName) + ".dll"));
				Assert.IsTrue (File.Exists (asmpath), "assembly does not exist");

				var cs = b.Output.GetIntermediaryAsText (Path.Combine ("generated", "src", "Com.Xamarin.Test.IDefaultInterfaceMethods.cs"));
				Assert.IsTrue (cs.Contains ("int Quux ();"), "Quux not generated.");
				Assert.IsTrue (cs.Contains ("virtual unsafe int Foo ()"), "Foo not generated.");
				Assert.IsTrue (cs.Contains ("virtual unsafe int Bar {"), "Bar not generated.");
				Assert.IsTrue (cs.Contains ("set {"), "(Baz) setter not generated.");
			}
		}

		[Test]
		public void BugzillaBug11964 ()
		{
			var proj = new XamarinAndroidBindingProject ();

			proj.Sources.Add (new BuildItem ("LibraryProjectProperties", "project.properties") {
				TextContent = () => ""
			});

			using (var builder = CreateDllBuilder ()) {
				builder.ThrowOnBuildFailure = false;
				Assert.IsFalse (builder.Build (proj), "Build should have failed.");
				string error = builder.LastBuildOutput
						.SkipWhile (x => !x.StartsWith ("Build FAILED."))
						.FirstOrDefault (x => x.Contains ("error XA1019:"));
				Assert.IsNotNull (error, "Build should have failed with XA1019.");
			}
		}
	}
}
