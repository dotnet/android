using System;
using System.Linq;
using NUnit.Framework;
using MonoDroid.Generation;
using System.Xml;

namespace generatortests
{
	[TestFixture]
	public class Java_Lang_Object : BaseGeneratorTest
	{
		[Test]
		public void Generated_OK ()
		{
			Run (target: Xamarin.Android.Binder.CodeGenerationTarget.XamarinAndroid,
					outputPath: "out/java.lang.Object",
					apiDescriptionFile: "expected/java.lang.Object/java.lang.Object.xml",
					expectedPath: "expected/java.lang.Object");

			var javaLangObject = BuiltAssembly.GetType ("Java.Lang.Object");

			Assert.IsNotNull (javaLangObject);
			Assert.IsTrue (javaLangObject.IsPublic);
			Assert.IsTrue (javaLangObject.FullName == "Java.Lang.Object");
			Assert.IsTrue (javaLangObject.GetCustomAttributes (false).Any (x => x.GetType ().Name == "RegisterAttribute"));

			Run (target: Xamarin.Android.Binder.CodeGenerationTarget.JavaInterop1,
					outputPath:         "out.ji/java.lang.Object",
					apiDescriptionFile: "expected/java.lang.Object/java.lang.Object.xml",
					expectedPath:       "expected.ji/java.lang.Object");
		}
	}
}

