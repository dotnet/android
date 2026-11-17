namespace Java.InteropTests;

[TestFixture]
public class JavaCharArrayContractTests : JavaPrimitiveArrayContract<JavaCharArray, char>
{
	protected override ICollection<char> CreateCollection (IEnumerable<char> values)
	{
		return new JavaCharArray (values);
	}

	protected override ICollection<char> CreateCollection (IList<char> values)
	{
		return new JavaCharArray (values);
	}

	protected override ICollection<char> CreateCollection (int length)
	{
		return new JavaCharArray (length);
	}
}

