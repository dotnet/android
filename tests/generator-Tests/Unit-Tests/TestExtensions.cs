namespace generatortests;

	static class TestExtensions
	{
		public static string NormalizeLineEndings (this string str)
		{
			// Normalize all line endings to \n so that our tests pass on
			// both Mac and Windows
			return str?.Replace ("\r\n", "\n").Replace ("\n", "").Replace ("\t", "").Replace (" ", "");
		}
	}
