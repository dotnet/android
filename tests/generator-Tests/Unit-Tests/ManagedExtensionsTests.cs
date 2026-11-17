namespace generatortests;

	[TestFixture]
	public class ManagedExtensionsTests
	{
		[Test]
		public void StripArity ()
		{
			Assert.AreEqual ("List<string>", "List`1<string>".StripArity ());
			Assert.AreEqual ("List<List<string>>", "List`10<List`1<string>>".StripArity ());
			Assert.AreEqual ("List<string>", "List<string>".StripArity ());
			Assert.AreEqual ("List`1", "List`1".StripArity ());
			Assert.AreEqual ("L<blah>ist<string>", "L<blah>ist`1<string>".StripArity ());
			Assert.AreEqual ("List<string>", "List`1`<string>".StripArity ());
			Assert.AreEqual ("List<<string>", "List`1<<string>".StripArity ());
			Assert.AreEqual ("List<", "List`<".StripArity ());
			Assert.AreEqual ("<", "`1<".StripArity ());
			Assert.AreEqual ("`", "`".StripArity ());
			Assert.AreEqual (string.Empty, string.Empty.StripArity ());
			Assert.IsNull ((null as string).StripArity ());
		}
	}
