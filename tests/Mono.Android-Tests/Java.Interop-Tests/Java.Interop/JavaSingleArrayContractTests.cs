namespace Java.InteropTests;

[TestFixture]
public class JavaSingleArrayContractTests : JavaPrimitiveArrayContract<JavaSingleArray, float>
{
	protected override ICollection<float> CreateCollection (IEnumerable<float> values)
	{
		return new JavaSingleArray (values);
	}

	protected override ICollection<float> CreateCollection (IList<float> values)
	{
		return new JavaSingleArray (values);
	}

	protected override ICollection<float> CreateCollection (int length)
	{
		return new JavaSingleArray (length);
	}
}

