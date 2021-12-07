using System;
using MonoDroid.Generation;
using NUnit.Framework;

using CodeGenerationTarget = Xamarin.Android.Binder.CodeGenerationTarget;

namespace generatortests
{
	[TestFixture]
	public class SymbolTableTests
	{
		[Test]
		public void FindGenericTypes ()
		{
			var table = new SymbolTable (CodeGenerationTarget.XAJavaInterop1);

			var list = new InterfaceGen (new GenBaseSupport {
				Name = "System.Collections.Generic.IList`1",
				FullName = "System.Collections.Generic.IList`1",
				JavaSimpleName = "System.Collections.Generic.IList`1"
			});

			table.AddType (list);

			var dict = new InterfaceGen (new GenBaseSupport {
				Name = "System.Collections.Generic.IDictionary`2",
				FullName = "System.Collections.Generic.IDictionary`2",
				JavaSimpleName = "System.Collections.Generic.IDictionary`2"
			});

			table.AddType (dict);

			Assert.AreEqual ("System.Collections.Generic.IList`1", table.Lookup ("System.Collections.Generic.IList<Java.Util.Locale.LanguageRange>").FullName);
			Assert.AreEqual ("System.Collections.Generic.IList`1", table.Lookup ("System.Collections.Generic.IList<List<Java.Util.Locale.LanguageRange>>").FullName);

			Assert.AreEqual ("System.Collections.Generic.IDictionary`2", table.Lookup ("System.Collections.Generic.IDictionary<string, Java.Util.Locale.LanguageRange>").FullName);
			Assert.AreEqual ("System.Collections.Generic.IDictionary`2", table.Lookup ("System.Collections.Generic.IDictionary<string, List<Java.Util.Locale.LanguageRange>>").FullName);

			Assert.AreEqual ("System.Collections.Generic.IList`1", table.Lookup ("System.Collections.Generic.IList<Dictionary<string, Java.Util.Locale.LanguageRange>>").FullName);
		}
	}
}
