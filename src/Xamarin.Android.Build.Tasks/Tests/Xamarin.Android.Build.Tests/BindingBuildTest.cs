using Mono.Cecil;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[Parallelizable (ParallelScope.Children)]
	public class BindingBuildTest : BaseTest {
#pragma warning disable 414
		static object [] ClassParseOptions = new object [] {
			new object[] {
				/* classParser */   "class-parse",
				},
		};

		[Test]
		[TestCaseSource ("ClassParseOptions")]
		public void BuildBasicBindingLibrary (string classParser)
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
			}
		}

		[Test]
		[TestCaseSource ("ClassParseOptions")]
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
				var fileCount = Directory.GetFiles (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath), "*", SearchOption.AllDirectories)
					.Where (x => !Path.GetFileName (x).StartsWith ("TemporaryGeneratedFile")).Count ();
				Assert.AreEqual (0, Directory.GetDirectories (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath), "*", SearchOption.AllDirectories).Length,
					"All directories in {0} should have been removed", proj.IntermediateOutputPath);
				Assert.AreEqual (0, fileCount,
					"All files in {0} should have been removed", proj.IntermediateOutputPath);
			}
		}

		[Test]
		[TestCaseSource ("ClassParseOptions")]
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
		[TestCaseSource ("ClassParseOptions")]
		public void BuildAarBindigLibraryWithNuGetPackageOfJar (string classParser)
		{
			var proj = new XamarinAndroidBindingProject () {
				UseLatestPlatformSdk = true,
				IsRelease = true,
			};
			proj.PackageReferences.Add (KnownPackages.AndroidSupportV4_22_1_1_1);
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
			var b = CreateDllBuilder (Path.Combine ("temp", TestName));
			Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			b.Dispose ();
		}

		[Test]
		[TestCaseSource ("ClassParseOptions")]
		public void BuildLibraryZipBindigLibraryWithAarOfJar (string classParser)
		{
			var proj = new XamarinAndroidBindingProject () {
				UseLatestPlatformSdk = true,
				IsRelease = true,
			};
			proj.AndroidClassParser = classParser;
			proj.PackageReferences.Add (KnownPackages.AndroidSupportV4_22_1_1_1);
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
			var b = CreateDllBuilder (Path.Combine ("temp", TestName), false, false);
			Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			b.Dispose ();
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
			binding.PackageReferences.Add (KnownPackages.AndroidSupportV4_22_1_1_1);
			using (var bindingBuilder = CreateDllBuilder (Path.Combine ("temp", "RemoveEventHandlerResolution", "Binding"))) {
				Assert.IsTrue (bindingBuilder.Build (binding), "binding build should have succeeded");
			}
		}

		[Test]
		public void JavaSourceJar ()
		{
#if false // Java source with javadoc
			package com.xamarin.android.test.msbuildtest;

public class JavaSourceJarTest
{
    /**
     * Returns greeting message.
     * <p>
     * Returns "Morning, ", "Hello, " or "Evening, " with name argument,
     * depending on the argument hour.
     * </p>
     * @param name name to display.
     * @param date time to determine the greeting message.
     * @return the resulting message.
     */
    public String greet (String name, java.util.Date date)
    {
        String head = date.getHours () < 11 ? "Morning, " :
            date.getHours () < 17 ? "Hello, " : "Evening, ";
        return head + name;
    }
}
#endif
			var binding = new XamarinAndroidBindingProject () {
				AndroidClassParser = "class-parse",
			};
			binding.SetProperty ("DocumentationFile", "UnnamedProject.xml");
			string sourcesJarBase64 = @"UEsDBBQACAgIAC2gP0wAAAAAAAAAAAAAAAAJAAQATUVUQS1JTkYv/soAAAMAUEsHCAAAAAACAAAAAAAAAFBLAwQUAAgICAAtoD9MAAAAAAAAAAAAAAAAFAAAAE1FVEEtSU5GL01BTklGRVNULk1G803My0xLLS7RDUstKs7Mz7NSMNQz4OVyLkpNLElN0XWqBAlY6BnEG5oaKmj4FyUm56QqOOcXFeQXJZYA1WvycvFyAQBQSwcIlldz8EQAAABFAAAAUEsDBBQACAgIACqgP0wAAAAAAAAAAAAAAAA7AAAAY29tL3hhbWFyaW4vYW5kcm9pZC90ZXN0L21zYnVpbGR0ZXN0L0phdmFTb3VyY2VKYXJUZXN0LmphdmF1kc1uwjAMx+99CosTsCqI0yRgG4dNQki7jL2Aaaw2Wz4qJ2WbJt59SSgfQsyq4kb+2X/babH6xJqgckZ8o0FWVqCV7JQUgXwQxm87pWX6nxdF2221qqDS6D2scYcb13FFa+T3CBS/BUSbjMfZwxjeKHRsPdRMFJStwZD3UU8cgUX7eM0OXh3byJYwiN+KtHbRg2MYvOyoj8CXCg1YNATIdWfIhvJYSFJLViY1ZyE0ZwKa2O1ZenLWXrbIaA718hEcSOVbjT/iipEYYlj1DAVioyxlnX+nXHKeLUNMvtO3qEn2/YY3gROSK8Kwv6XOSviIaxddUFo8p1ZSP6Oceth+sp5vCCU8ZELUFFZxeg/DESxgOoWny0XD7JSb7FbGfco4vcbs8jHmp+R+zix8l/s9xPbFvvgDUEsHCDlC8jY2AQAAawIAAFBLAQIUABQACAgIAC2gP0wAAAAAAgAAAAAAAAAJAAQAAAAAAAAAAAAAAAAAAABNRVRBLUlORi/+ygAAUEsBAhQAFAAICAgALaA/TJZXc/BEAAAARQAAABQAAAAAAAAAAAAAAAAAPQAAAE1FVEEtSU5GL01BTklGRVNULk1GUEsBAhQAFAAICAgAKqA/TDlC8jY2AQAAawIAADsAAAAAAAAAAAAAAAAAwwAAAGNvbS94YW1hcmluL2FuZHJvaWQvdGVzdC9tc2J1aWxkdGVzdC9KYXZhU291cmNlSmFyVGVzdC5qYXZhUEsFBgAAAAADAAMA5gAAAGICAAAAAA==";
			string classesjarBase64 = @"
UEsDBBQACAgIAO+EP0wAAAAAAAAAAAAAAAAJAAQATUVUQS1JTkYv/soAAAMAUEsHCAAAAAACAAAAAAAAAFBLAwQUAAgIC
ADvhD9MAAAAAAAAAAAAAAAAFAAAAE1FVEEtSU5GL01BTklGRVNULk1G803My0xLLS7RDUstKs7Mz7NSMNQz4OVyLkpNLE
lN0XWqBAlY6BnEG5oaKmj4FyUm56QqOOcXFeQXJZYA1WvycvFyAQBQSwcIlldz8EQAAABFAAAAUEsDBBQACAgIALOEP0w
AAAAAAAAAAAAAAAA8AAAAY29tL3hhbWFyaW4vYW5kcm9pZC90ZXN0L21zYnVpbGR0ZXN0L0phdmFTb3VyY2VKYXJUZXN0
LmNsYXNzbVHZSsNAFD1j2yStaW3rvlvX1i0g4osiuKLi8lARfHPaDCWaJiWdit/ji6/6UkHBD/CjxDupIloDucuZO+fMn
Hn/eHkDsIpCAp3oS6AfAwYGDQwZGNYxkoCmUA2jKozpGNcxwaBtOJ4jNxki+cIFQ3THtwVD17HjidNGtSSCc15yCYlVAi
Ekw1r++JrfcsvlXsUqysDxKustpCEd19rlUqwX2kcYkkXJyzcnvBYS6sgxJIp+IyiLfUcJ9B3RnhZwxINzUZfLisWEiaS
OSRNTmGaIn/iBR4SLdHT9QLiur6r43q34Rvv/am83HNcWgYkZzJqYQ54uUfar1h2vclq3uGcHvmNbkiStar2kxsO67UAM
6R/ys9K1KP+GWnoMqd9+MBgVIQ+IqR7afEiu81pNeDbD0j92ttv3dQVy0ZD+t0pP/h+fkYN6fvV1gCnvKKaoG6XMKMfmn
8GeqKBHpqiFYIRiGpmv0SvaGqW8sthER7rzHkY28oDusMuoLvqAWDZ2+grt8hn6UhPGAv1NxB9DWcWbIk4QYxeyGEc3Rc
BAJJXc0qmjw4eTvZ9QSwcINzBZxakBAAC1AgAAUEsBAhQAFAAICAgA74Q/TAAAAAACAAAAAAAAAAkABAAAAAAAAAAAAAA
AAAAAAE1FVEEtSU5GL/7KAABQSwECFAAUAAgICADvhD9Mlldz8EQAAABFAAAAFAAAAAAAAAAAAAAAAAA9AAAATUVUQS1J
TkYvTUFOSUZFU1QuTUZQSwECFAAUAAgICACzhD9MNzBZxakBAAC1AgAAPAAAAAAAAAAAAAAAAADDAAAAY29tL3hhbWFya
W4vYW5kcm9pZC90ZXN0L21zYnVpbGR0ZXN0L0phdmFTb3VyY2VKYXJUZXN0LmNsYXNzUEsFBgAAAAADAAMA5wAAANYCAA
AAAA==";
			using (var bindingBuilder = CreateDllBuilder ("temp/JavaSourceJar", false, false)) {
				binding.Jars.Add (new AndroidItem.EmbeddedJar ("javasourcejartest.jar") {
					BinaryContent = () => Convert.FromBase64String (classesjarBase64)
				});
				binding.OtherBuildItems.Add (new BuildItem ("JavaSourceJar", "javasourcejartest-sources.jar") {
					BinaryContent = () => Convert.FromBase64String (sourcesJarBase64)
				});
				Assert.IsTrue (bindingBuilder.Build (binding), "binding build should have succeeded");
				string xml = bindingBuilder.Output.GetIntermediaryAsText ("docs/Com.Xamarin.Android.Test.Msbuildtest/JavaSourceJarTest.xml");
				Assert.IsTrue (xml.Contains ("<param name=\"name\"> - name to display.</param>"), "missing doc");
			}
		}

		[Test]
		[TestCaseSource ("ClassParseOptions")]
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
	}
}
