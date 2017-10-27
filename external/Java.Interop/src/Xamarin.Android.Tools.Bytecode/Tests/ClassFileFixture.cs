using System;
using System.IO;
using System.Linq;
using System.Reflection;

using Xamarin.Android.Tools.Bytecode;

using NUnit.Framework;

namespace Xamarin.Android.Tools.BytecodeTests {

	public class ClassFileFixture {

		protected static ClassFile LoadClassFile (string resource)
		{
			using (var s = Assembly.GetExecutingAssembly ().GetManifestResourceStream (resource)) {
				return new ClassFile (s);
			}
		}

		protected static string LoadString (string resource)
		{
			using (var s = Assembly.GetExecutingAssembly ().GetManifestResourceStream (resource))
			using (var r = new StreamReader (s))
				return r.ReadToEnd ();
		}

		protected static string LoadToTempFile (string resource)
		{
			var tempFilePath = Path.GetTempFileName ();

			using (var w = File.Create (tempFilePath))
			using (var s = Assembly.GetExecutingAssembly ().GetManifestResourceStream (resource))
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
	}
}

