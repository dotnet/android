﻿using System;
using Xamarin.ProjectTools;
using NUnit.Framework;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace Xamarin.Android.Build.Tests
{
	[Parallelizable (ParallelScope.Children)]
	public class BindingBuildTest : BaseTest
	{
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
			proj.Packages.Add (KnownPackages.AndroidSupportV4_22_1_1_1);
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
			proj.Packages.Add (KnownPackages.AndroidSupportV4_22_1_1_1);
			proj.Jars.Add (new AndroidItem.LibraryProjectZip ("Jars\\aFileChooserBinaries.zip") {
				WebContent = "https://www.dropbox.com/s/hl98jrvlw4d9vjy/aFileChooserBinaries.zip?dl=1"
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
				WebContent = "https://www.dropbox.com/s/5ovudccigydohys/javaBindingIssue.jar?dl=1"
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
				WebContent = "https://www.dropbox.com/s/bubopadhd9d1l4b/adal-1.0.7.aar?dl=1"
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
				WebContent = "https://www.dropbox.com/s/53679881kg9rdiq/mylibrary-debug.aar?dl=1"
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
				WebContent = "https://www.dropbox.com/s/apphdrh9cjqvtye/card.io-5.3.0.aar?dl=1"
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
				WebContent = "https://www.dropbox.com/s/astiqp8jo97x91h/mylibrary.aar?dl=1"
			});
			binding.Jars.Add (new AndroidItem.EmbeddedJar ("Jars\\svg-android.jar") {
				WebContent = "https://www.dropbox.com/s/5ovudccigydohys/javaBindingIssue.jar?dl=1"
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
						WebContent = "https://www.dropbox.com/s/5ovudccigydohys/javaBindingIssue.jar?dl=1"
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
			binding.Packages.Add (KnownPackages.AndroidSupportV4_22_1_1_1);
			using (var bindingBuilder = CreateDllBuilder (Path.Combine ("temp", "RemoveEventHandlerResolution", "Binding"))) {
				Assert.IsTrue (bindingBuilder.Build (binding), "binding build should have succeeded");
			}
		}
	}
}
