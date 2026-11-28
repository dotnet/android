using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xamarin.Android.Tools.Aidl;

namespace Xamarin.Android.Tools.Aidl_Tests
{
	[TestClass]
	public class AidlCompilerTests : AidlCompilerTestBase
	{
		[TestMethod]
		public void ListAndMap () => RunTest (nameof (ListAndMap));

		[TestMethod]
		public void NamespaceResolution () => RunTest (nameof (NamespaceResolution));

		[TestMethod]
		public void PrimitiveTypes () => RunTest (nameof (PrimitiveTypes));

		[TestMethod]
		public void FaceService () => RunTest (nameof (FaceService));

		[TestMethod]
		public void IUpdateEngine () => RunTest (nameof (IUpdateEngine));

		[TestMethod]
		public void ITelephony () => RunTest (nameof (ITelephony));

		[TestMethod]
		[DataRow (false)]
		[DataRow (true)]
		public void TypeNameNullIdentifierException (bool hasGenericArguments)
		{
			Action action = hasGenericArguments
				? () => new TypeName (new [] { "android", null }, Array.Empty<TypeName> ())
				: () => new TypeName (new [] { "android", null });

			var exception = Assert.ThrowsExactly<ArgumentException> (action);
			Assert.AreEqual ("identifiers", exception.ParamName);
			StringAssert.StartsWith (exception.Message, "'identifiers' contain one or more null values: android");
		}
	}
}
