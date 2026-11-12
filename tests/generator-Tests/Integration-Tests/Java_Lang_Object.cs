using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class Java_Lang_Object : BaseGeneratorTest
	{
		[Test]
		public void Generated_OK ()
		{
			RunTarget (
					outputRelativePath: "java.lang.Object",
					apiDescriptionFile: "TestInputs/java.lang.Object/java.lang.Object.xml",
					expectedRelativePath: "java.lang.Object");
		}
	}
}
