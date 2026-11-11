using generator.SourceWriters;
using MonoDroid.Generation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace generatortests.SourceWriters
{
	[TestClassAttribute]
	public class WeakImplementorFieldTests : SourceWritersTestBase
	{
		[TestMethodAttribute]
		public void WeakImplementorField_Regular ()
		{
			var field = new WeakImplementorField ("foo", new CodeGenerationOptions ());

			Assert.AreEqual ("WeakReference weak_implementor_foo;", GetOutput (field).Trim ());
		}

		[TestMethodAttribute]
		public void WeakImplementorField_Nullable ()
		{
			var field = new WeakImplementorField ("foo", new CodeGenerationOptions { SupportNullableReferenceTypes = true });

			Assert.AreEqual ("WeakReference? weak_implementor_foo;", GetOutput (field).Trim ());
		}
	}
}
