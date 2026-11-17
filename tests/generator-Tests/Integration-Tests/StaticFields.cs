namespace generatortests;

	[TestFixture]
	public class StaticFields : BaseGeneratorTest
	{
		[Test]
		public void GeneratedOK ()
		{
			RunTarget (
					outputRelativePath:     "StaticFields",
					apiDescriptionFile:     "TestInputs/StaticFields/StaticField.xml",
					expectedRelativePath:   "StaticFields");
		}
	}

