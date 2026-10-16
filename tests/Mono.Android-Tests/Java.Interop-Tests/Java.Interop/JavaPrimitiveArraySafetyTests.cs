using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests;

[TestFixture]
[Category ("JniPrimitiveArrayBounds")]
public class JavaPrimitiveArraySafetyTests {

	[TestCase (typeof (JniBooleanArrayElements))]
	[TestCase (typeof (JniSByteArrayElements))]
	[TestCase (typeof (JniCharArrayElements))]
	[TestCase (typeof (JniInt16ArrayElements))]
	[TestCase (typeof (JniInt32ArrayElements))]
	[TestCase (typeof (JniInt64ArrayElements))]
	[TestCase (typeof (JniSingleArrayElements))]
	[TestCase (typeof (JniDoubleArrayElements))]
	public void RefIndexerRequiresUnsafe (
		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicProperties)] Type type)
	{
		var getter = type.GetProperty ("Item")?.GetMethod;
		Assert.IsNotNull (getter);
		if (getter == null)
			throw new InvalidOperationException ($"No ref-returning indexer on {type}.");

		Assert.IsTrue (getter.ReturnType.IsByRef, $"{type}.Item must preserve its ref return.");
		Assert.IsTrue (RequiresUnsafe (getter), $"{type}.Item must require an unsafe caller context.");
	}

	[TestCase (typeof (JavaBooleanArray))]
	[TestCase (typeof (JavaSByteArray))]
	[TestCase (typeof (JavaCharArray))]
	[TestCase (typeof (JavaInt16Array))]
	[TestCase (typeof (JavaInt32Array))]
	[TestCase (typeof (JavaInt64Array))]
	[TestCase (typeof (JavaSingleArray))]
	[TestCase (typeof (JavaDoubleArray))]
	[TestCase (typeof (JavaPrimitiveArray<int>))]
	public void GetElementsRemainsSafe (
		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicMethods)] Type type)
	{
		var method = type.GetMethod ("GetElements", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
		Assert.IsNotNull (method);
		if (method == null)
			throw new InvalidOperationException ($"No declared GetElements method on {type}.");

		Assert.IsFalse (RequiresUnsafe (method), $"{type}.GetElements must remain safe-callable.");
	}

	static bool RequiresUnsafe (MethodInfo method)
	{
		return method.GetCustomAttributesData ().Any (
			a => a.AttributeType.FullName == "System.Diagnostics.CodeAnalysis.RequiresUnsafeAttribute");
	}

	[Test]
	public void BorrowedReferenceReadsAndWritesWhileAcquisitionIsActive ()
	{
		using var array = new JavaInt32Array (new [] { 10 });
		using (var elements = array.GetElements ()) {
			unsafe {
				ref int value = ref elements [0];
				Assert.AreEqual (10, value);
				value = 42;
				Assert.AreEqual (42, elements [0]);
			}
		}
		Assert.AreEqual (42, array [0]);
	}

	[TestCase (-1)]
	[TestCase (1)]
	public void RefIndexerChecksBounds (int index)
	{
		using var array = new JavaInt32Array (1);
		using var elements = array.GetElements ();
		Assert.Throws<ArgumentOutOfRangeException> (() => {
			unsafe {
				_ = elements [index];
			}
		});
	}

	[Test]
	public void RefIndexerRejectsDisposedAcquisition ()
	{
		using var array = new JavaInt32Array (1);
		using var elements = array.GetElements ();
		elements.Dispose ();
		Assert.Throws<ObjectDisposedException> (() => {
			unsafe {
				_ = elements [0];
			}
		});
	}
}
