using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Xamarin.Android.Tools.Aidl;

namespace Xamarin.Android.Tools.Aidl_Tests
{
	[TestFixture]
	public class AidlCompilerTests : AidlCompilerTestBase
	{
		[Test]
		public void ListAndMap () => RunTest (nameof (ListAndMap));

		[Test]
		public void NamespaceResolution () => RunTest (nameof (NamespaceResolution));

		[Test]
		public void PrimitiveTypes () => RunTest (nameof (PrimitiveTypes));

		[Test]
		public void FaceService () => RunTest (nameof (FaceService));

		[Test]
		public void IUpdateEngine () => RunTest (nameof (IUpdateEngine));

		[Test]
		public void ITelephony () => RunTest (nameof (ITelephony));

		[TestCase (false)]
		[TestCase (true)]
		public void TypeNameNullIdentifierException (bool hasGenericArguments)
		{
			TestDelegate action = hasGenericArguments
				? () => new TypeName (new [] { "android", null }, Array.Empty<TypeName> ())
				: () => new TypeName (new [] { "android", null });

			var exception = Assert.Throws<ArgumentException> (action);
			Assert.AreEqual ("identifiers", exception.ParamName);
			StringAssert.StartsWith ("'identifiers' contain one or more null values: android", exception.Message);
		}
	}
}
