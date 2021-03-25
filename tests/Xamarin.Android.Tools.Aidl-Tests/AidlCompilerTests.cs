using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

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
	}
}
