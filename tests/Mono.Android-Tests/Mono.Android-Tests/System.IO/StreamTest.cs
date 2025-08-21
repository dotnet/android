using System;
using System.IO;
using NUnit.Framework;
using JavaStreamTest = global::Net.Dot.Android.Test.StreamTest;

namespace System.IOTests;

[TestFixture]
public class StreamTest
{
	[Test]
	public void InputStreamAdapter_Read ()
	{
		using var stream = new MemoryStream (new byte [JavaStreamTest.BufferSize]);
		var result = JavaStreamTest.InputStreamAdapter_Read (stream);
		Assert.AreEqual (0, result); // First byte is 0
	}

	[Test]
	public void InputStreamAdapter_Read_bytes ()
	{
		using var stream = new MemoryStream (new byte [JavaStreamTest.BufferSize]);
		var result = JavaStreamTest.InputStreamAdapter_Read_bytes (stream);
		Assert.AreEqual (JavaStreamTest.BufferSize, result);
	}

	[Test]
	public void InputStreamAdapter_Read_bytes_int_int ()
	{
		using var stream = new MemoryStream (new byte [JavaStreamTest.BufferSize]);
		var result = JavaStreamTest.InputStreamAdapter_Read_bytes_int_int (stream);
		Assert.AreEqual (JavaStreamTest.BufferSize, result);
	}
}
