using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

using Xamarin.Android.Tools.Bytecode;

using NUnit.Framework;

namespace Xamarin.Android.Tools.BytecodeTests {

	public class ClassFileFixture {

		static void OnLog (TraceLevel level, int verbosity, string format, object[] args)
		{
			var message = string.Format (format, args);

			// No "debug" messages from `Methods.cs` should be generated when parsing `.class` files.
			if (message.StartsWith ("class-parse: method", StringComparison.OrdinalIgnoreCase)) {
				Assert.Fail ($"TraceLevel={level}, Verbosity={verbosity}, Message={message}");
			}
		}

		[SetUp]
		public void CreateLogger ()
		{
			Log.OnLog   = OnLog;
		}

		[TearDown]
		public void DestroyLogger ()
		{
			Log.OnLog   = null;
		}

		protected static ClassFile LoadClassFile (string resource)
		{
			using (var stream = GetResourceStream (resource)) {
				if (stream == null) {
					throw new InvalidOperationException ($"Could not find resource `{resource}`!");
				}
				return new ClassFile (stream);
			}
		}

		protected static string LoadString (string resource)
		{
			var s   = GetResourceStream (resource);
			if (s == null) {
				throw new InvalidOperationException ($"Could not find resource `{resource}`!");
			}
			using (s)
			using (var r = new StreamReader (s))
				return r.ReadToEnd ();
		}

		protected static string LoadToTempFile (string resource)
		{
			var tempFilePath = Path.GetTempFileName ();

			using (var w = File.Create (tempFilePath))
			using (var s = GetResourceStream (resource))
				s.CopyTo (w);

			return tempFilePath;
		}

		protected static void AssertXmlDeclaration (string classResource, string xmlResource, string documentationPath = null)
		{
			var classPathBuilder    = new ClassPath () {
				ApiSource           = "class-parse",
				DocumentationPaths  = new string[] {
					documentationPath,
				},
			};
			classPathBuilder.Add (LoadClassFile (classResource));

			var actual  = new StringWriter ();
			classPathBuilder.ApiSource  = "class-parse";
			classPathBuilder.SaveXmlDescription (actual);

			var expected    = LoadString (xmlResource);

			Assert.AreEqual (expected, actual.ToString ());
		}

		protected static void AssertXmlDeclaration (string[] classResources, string xmlResource, string documentationPath = null)
		{
			var classPathBuilder    = new ClassPath () {
				ApiSource           = "class-parse",
				DocumentationPaths  = new string[] {
					documentationPath,
				},
				AutoRename = true
			};
			foreach(var classFile in classResources.Select(s => LoadClassFile (s)))
				classPathBuilder.Add (classFile);

			var actual  = new StringWriter ();
			classPathBuilder.SaveXmlDescription (actual);

			var expected    = LoadString (xmlResource);

			Assert.AreEqual (expected, actual.ToString ());
		}

		static Stream GetResourceStream (string resource)
		{
			// Look for resources that end with our name, this allows us to
			// avoid the LogicalName stuff
			var assembly = Assembly.GetExecutingAssembly ();
			var name = assembly.GetManifestResourceNames ().FirstOrDefault (n => n.EndsWith ("." + resource, StringComparison.OrdinalIgnoreCase)) ?? resource;

			return assembly.GetManifestResourceStream (name);
		}
	}
}

