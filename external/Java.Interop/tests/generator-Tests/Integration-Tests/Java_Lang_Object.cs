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
			Run (target: Xamarin.Android.Binder.CodeGenerationTarget.JavaInterop1,
					outputPath:         "out.ji/java.lang.Object",
					apiDescriptionFile: "expected.ji/java.lang.Object/java.lang.Object.xml",
					expectedPath:       "expected.ji/java.lang.Object");
		}
	}
}

