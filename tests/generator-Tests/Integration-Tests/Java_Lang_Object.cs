using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace generatortests
{
	[TestClassAttribute]
	public class Java_Lang_Object : BaseGeneratorTest
	{
		[TestMethodAttribute]
		public void Generated_OK ()
		{
			RunTarget (
					outputRelativePath: "java.lang.Object",
					apiDescriptionFile: "TestInputs/java.lang.Object/java.lang.Object.xml",
					expectedRelativePath: "java.lang.Object");
		}
	}
}
