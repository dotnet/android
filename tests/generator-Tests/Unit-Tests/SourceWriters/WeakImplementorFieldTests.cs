using generator.SourceWriters;
using MonoDroid.Generation;
using NUnit.Framework;

namespace generatortests.SourceWriters
{
	[TestFixture]
	public class WeakImplementorFieldTests : SourceWritersTestBase
	{
		[Test]
		public void WeakImplementorField_Regular ()
		{
			var field = new WeakImplementorField ("foo", new CodeGenerationOptions ());

			Assert.AreEqual ("WeakReference weak_implementor_foo;", GetOutput (field).Trim ());
		}

		[Test]
		public void WeakImplementorField_Nullable ()
		{
			var field = new WeakImplementorField ("foo", new CodeGenerationOptions { SupportNullableReferenceTypes = true });

			Assert.AreEqual ("WeakReference? weak_implementor_foo;", GetOutput (field).Trim ());
		}
	}
}
