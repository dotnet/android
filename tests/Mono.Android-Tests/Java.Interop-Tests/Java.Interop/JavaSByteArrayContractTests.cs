namespace Java.InteropTests;

[TestFixture]
public class JavaSByteArrayContractTests : JavaPrimitiveArrayContract<JavaSByteArray, sbyte>
{
	protected override ICollection<sbyte> CreateCollection (IEnumerable<sbyte> values)
	{
		return new JavaSByteArray (values);
	}

	protected override ICollection<sbyte> CreateCollection (IList<sbyte> values)
	{
		return new JavaSByteArray (values);
	}

	protected override ICollection<sbyte> CreateCollection (int length)
	{
		return new JavaSByteArray (length);
	}
}

