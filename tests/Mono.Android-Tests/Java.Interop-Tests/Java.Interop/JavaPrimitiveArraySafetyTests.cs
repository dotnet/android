using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests;

[TestFixture]
public class JavaPrimitiveArraySafetyTests {

	[TestCase (typeof (JavaBooleanArray))]
	[TestCase (typeof (JavaSByteArray))]
	[TestCase (typeof (JavaCharArray))]
	[TestCase (typeof (JavaInt16Array))]
	[TestCase (typeof (JavaInt32Array))]
	[TestCase (typeof (JavaInt64Array))]
	[TestCase (typeof (JavaSingleArray))]
	[TestCase (typeof (JavaDoubleArray))]
	[TestCase (typeof (JavaPrimitiveArray<int>))]
	[Category ("JniPrimitiveArrayBounds")]
	public void GetElementsRequiresUnsafe (
		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicMethods)] Type type)
	{
		var method = type.GetMethod ("GetElements", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
		Assert.IsNotNull (method);
		if (method == null)
			throw new InvalidOperationException ($"No declared GetElements method on {type}.");

		Assert.IsTrue (method.GetCustomAttributesData ().Any (
			a => a.AttributeType.FullName == "System.Diagnostics.CodeAnalysis.RequiresUnsafeAttribute"),
			$"{type}.GetElements must require an unsafe caller context.");
	}
}
