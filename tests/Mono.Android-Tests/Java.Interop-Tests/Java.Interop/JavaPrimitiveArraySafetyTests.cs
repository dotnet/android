using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests;

[TestFixture]
[Category ("JniPrimitiveArrayBounds")]
public class JavaPrimitiveArraySafetyTests {

	unsafe delegate ref T RefIndexer<T> (int index);

	[Test]
	public void BooleanElements ()
	{
		using var array = new JavaBooleanArray (new [] { false });
		using var elements = array.GetElements ();
		unsafe {
			AssertAcquisition (array, elements, (int index) => ref elements [index], false, true, sizeof (bool));
		}
	}

	[Test]
	public void SByteElements ()
	{
		using var array = new JavaSByteArray (new sbyte [] { 10 });
		using var elements = array.GetElements ();
		unsafe {
			AssertAcquisition (array, elements, (int index) => ref elements [index], (sbyte) 10, (sbyte) 42, sizeof (sbyte));
		}
	}

	[Test]
	public void CharElements ()
	{
		using var array = new JavaCharArray (new [] { 'A' });
		using var elements = array.GetElements ();
		unsafe {
			AssertAcquisition (array, elements, (int index) => ref elements [index], 'A', 'B', sizeof (char));
		}
	}

	[Test]
	public void Int16Elements ()
	{
		using var array = new JavaInt16Array (new short [] { 10 });
		using var elements = array.GetElements ();
		unsafe {
			AssertAcquisition (array, elements, (int index) => ref elements [index], (short) 10, (short) 42, sizeof (short));
		}
	}

	[Test]
	public void Int32Elements ()
	{
		using var array = new JavaInt32Array (new [] { 10 });
		using var elements = array.GetElements ();
		unsafe {
			AssertAcquisition (array, elements, (int index) => ref elements [index], 10, 42, sizeof (int));
		}
	}

	[Test]
	public void Int64Elements ()
	{
		using var array = new JavaInt64Array (new [] { 10L });
		using var elements = array.GetElements ();
		unsafe {
			AssertAcquisition (array, elements, (int index) => ref elements [index], 10L, 42L, sizeof (long));
		}
	}

	[Test]
	public void SingleElements ()
	{
		using var array = new JavaSingleArray (new [] { 10f });
		using var elements = array.GetElements ();
		unsafe {
			AssertAcquisition (array, elements, (int index) => ref elements [index], 10f, 42f, sizeof (float));
		}
	}

	[Test]
	public void DoubleElements ()
	{
		using var array = new JavaDoubleArray (new [] { 10d });
		using var elements = array.GetElements ();
		unsafe {
			AssertAcquisition (array, elements, (int index) => ref elements [index], 10d, 42d, sizeof (double));
		}
	}

	static unsafe void AssertAcquisition<T> (
		JavaPrimitiveArray<T> array,
		JniArrayElements elements,
		RefIndexer<T> getElement,
		T initialValue,
		T updatedValue,
		int sizeOfElement)
	{
		Assert.AreEqual (sizeOfElement, elements.Size);
		Assert.AreNotEqual (IntPtr.Zero, elements.Elements);

		{
			ref T value = ref getElement (0);
			Assert.AreEqual (initialValue, value);
			value = updatedValue;
			Assert.AreEqual (updatedValue, getElement (0));
		}

		Assert.Throws<ArgumentOutOfRangeException> (() => { _ = getElement (-1); });
		Assert.Throws<ArgumentOutOfRangeException> (() => { _ = getElement (array.Length); });

		elements.Dispose ();
		Assert.AreEqual (updatedValue, array [0]);
		Assert.Throws<ObjectDisposedException> (() => { _ = getElement (0); });
		Assert.Throws<ObjectDisposedException> (() => { _ = elements.Elements; });
		Assert.Throws<ObjectDisposedException> (() => { _ = elements.Size; });
		Assert.Throws<ObjectDisposedException> (() => elements.Release (JniReleaseArrayElementsMode.Abort));
		elements.Dispose ();

		using (var genericElements = array.GetElements ()) {
			Assert.AreEqual (sizeOfElement, genericElements.Size);
			Assert.AreNotEqual (IntPtr.Zero, genericElements.Elements);
		}
	}
}
