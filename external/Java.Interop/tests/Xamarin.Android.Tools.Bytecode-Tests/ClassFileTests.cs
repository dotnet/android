using System;
using System.IO;
using System.Reflection;

using Xamarin.Android.Tools.Bytecode;

using NUnit.Framework;

namespace Xamarin.Android.Tools.BytecodeTests {

	[TestFixture]
	public class ClassFileTests {

		[Test]
		public void Constructor_Exceptions ()
		{
			Assert.Throws<ArgumentNullException> (() => new ClassFile (null));
		}

		[Test]
		public void ModifiedUtf8_DecodesUtf16CodeUnits ()
		{
			using var poolStream = new MemoryStream (new byte [] { 0, 1 });
			var pool = new ConstantPool (poolStream);

			Assert.AreEqual ("\0", Decode (pool, new byte [] { 0xc0, 0x80 }));
			Assert.AreEqual ("\u0080\u0800", Decode (pool, new byte [] { 0xc2, 0x80, 0xe0, 0xa0, 0x80 }));
			Assert.AreEqual (
				"\ud000\ud001",
				Decode (pool, new byte [] { 0xed, 0x80, 0x80, 0xed, 0x80, 0x81 })
			);
			Assert.AreEqual (
				"\U00010400",
				Decode (pool, new byte [] { 0xed, 0xa0, 0x81, 0xed, 0xb0, 0x80 })
			);
			Assert.AreEqual (
				"\ud801\udc00\ud801",
				Decode (pool, new byte [] { 0xed, 0xa0, 0x81, 0xed, 0xb0, 0x80, 0xed, 0xa0, 0x81 })
			);
		}

		[Test]
		public void ModifiedUtf8_RejectsMalformedEncodings ()
		{
			using var poolStream = new MemoryStream (new byte [] { 0, 1 });
			var pool = new ConstantPool (poolStream);
			foreach (var bytes in new [] {
				new byte [] { 0 },
				new byte [] { 0xc0, 0x81 },
				new byte [] { 0xc1, 0xbf },
				new byte [] { 0xe0, 0x81, 0x81 },
			}) {
				Assert.Throws<InvalidDataException> (() => Decode (pool, bytes));
			}
		}

		static string Decode (ConstantPool pool, byte [] bytes)
		{
			using var stream = new MemoryStream ();
			stream.WriteByte ((byte) (bytes.Length >> 8));
			stream.WriteByte ((byte) bytes.Length);
			stream.Write (bytes, 0, bytes.Length);
			stream.Position = 0;
			return new ConstantPoolUtf8Item (pool, stream).Value;
		}
	}
}
