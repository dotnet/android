#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Android.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests.Tasks
{
	[TestFixture]
	public class FindLayoutsToBindTests
	{
		string root = "";

		[SetUp]
		public void SetUp ()
		{
			root = Path.Combine (Path.GetTempPath (), "FindLayoutsToBindTests", Guid.NewGuid ().ToString ("N"));
			Directory.CreateDirectory (root);
		}

		[TearDown]
		public void TearDown ()
		{
			Directory.Delete (root, recursive: true);
		}

		string CreateFile (params string [] parts)
		{
			string path = Path.Combine (root, Path.Combine (parts));
			Directory.CreateDirectory (Path.GetDirectoryName (path) ?? root);
			File.WriteAllText (path, "<LinearLayout />");
			return path;
		}

		[Test]
		public void CollectsLayoutsFromResourcesBoundLayoutsAndCache ()
		{
			string layout = CreateFile ("Resources", "layout", "main.xml");
			string qualified = CreateFile ("Resources", "layout-land-v21", "main.axml");
			string upper = CreateFile ("Resources", "Layout-Port", "other.XML");
			string drawable = CreateFile ("Resources", "drawable", "icon.xml");
			string notLayout = CreateFile ("Resources", "notlayout", "main.xml");
			string json = CreateFile ("Resources", "layout", "data.json");
			string missing = Path.Combine (root, "Resources", "layout", "missing.xml");
			string cached = CreateFile ("Cached", "layout", "cached.xml");
			string cacheFile = Path.Combine (root, "deps.cache");
			File.WriteAllLines (cacheFile, [cached, drawable, ""]);

			var bound = new TaskItem ("Bound.xml");
			bound.SetMetadata ("ClassName", "Foo.Bar");

			var task = new FindLayoutsToBind {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				GenerateLayoutBindings = true,
				ResourceFiles = new [] { layout, qualified, upper, drawable, notLayout, json, missing }
					.Select (f => (ITaskItem) new TaskItem (f)).ToArray (),
				BoundLayouts = [bound, new TaskItem (layout.ToUpperInvariant ())],
				BindingDependenciesCacheFile = cacheFile,
			};

			Assert.IsTrue (task.Execute ());
			var actual = task.LayoutsToBind?.Select (i => i.ItemSpec).ToArray ();
			CollectionAssert.AreEqual (new [] { layout, qualified, upper, "Bound.xml", cached }, actual);
			Assert.AreEqual ("Foo.Bar", task.LayoutsToBind? [3].GetMetadata ("ClassName"));
		}

		[Test]
		public void ShortTaskNameResolves ()
		{
			string repo = NativeAotBootstrapTestTools.FindRepositoryRoot ();
			var common = XDocument.Load (Path.Combine (repo, "src", "Xamarin.Android.Build.Tasks", "Xamarin.Android.Common.targets"));
			var usingTasks = common.Descendants ()
				.Where (e => e.Name.LocalName == "UsingTask" && (string?) e.Attribute ("TaskName") == "Microsoft.Android.Tasks.FindLayoutsToBind")
				.Select (e => new XElement (e.Name.LocalName, e.Attributes ()))
				.ToArray ();
			Assert.AreEqual (2, usingTasks.Length);
			Assert.AreEqual ("TaskHostFactory", (string?) usingTasks.Single (u => ((string?) u.Attribute ("Condition"))?.Contains ("'Full'") == true).Attribute ("TaskFactory"));
			Assert.IsNull (usingTasks.Single (u => ((string?) u.Attribute ("Condition"))?.Contains ("'Core'") == true).Attribute ("TaskFactory"));

			string layout = CreateFile ("Resources", "layout", "main.xml");
			XElement Invoke (string taskName, string itemName) =>
				new XElement (taskName,
					new XAttribute ("GenerateLayoutBindings", "true"),
					new XAttribute ("ResourceFiles", layout),
					new XElement ("Output", new XAttribute ("ItemName", itemName), new XAttribute ("TaskParameter", "LayoutsToBind")));
			string projectFile = Path.Combine (root, "names.proj");
			new XDocument (
				new XElement ("Project",
					new XElement ("PropertyGroup",
						new XElement ("_MicrosoftAndroidBuildTasksAssembly", typeof (FindLayoutsToBind).Assembly.Location)),
					usingTasks,
					new XElement ("Target", new XAttribute ("Name", "Bind"),
						Invoke ("FindLayoutsToBind", "Short"))))
				.Save (projectFile);

			string dotnet = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
			var result = NativeAotBootstrapTestTools.Run (dotnet, "msbuild", projectFile, "-t:Bind", "-getItem:Short", "-v:quiet", "-nr:false");
			Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
			using var json = JsonDocument.Parse (result.Output);
			var identities = json.RootElement.GetProperty ("Items").GetProperty ("Short").EnumerateArray ()
				.Select (i => i.GetProperty ("Identity").GetString ()).ToArray ();
			CollectionAssert.AreEqual (new [] { layout }, identities);
		}

		[Test]
		public void SkipsResourcesWhenLayoutBindingsAreDisabled ()
		{
			string layout = CreateFile ("Resources", "layout", "main.xml");

			var task = new FindLayoutsToBind {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				GenerateLayoutBindings = false,
				ResourceFiles = [new TaskItem (layout)],
			};

			Assert.IsTrue (task.Execute ());
			Assert.AreEqual (0, task.LayoutsToBind?.Length);
		}
	}
}
