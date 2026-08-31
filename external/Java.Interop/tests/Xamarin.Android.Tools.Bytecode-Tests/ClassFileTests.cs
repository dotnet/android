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

			Assert.AreEqual (
				"\ud000\ud001",
				Decode (pool, new byte [] { 0xed, 0x80, 0x80, 0xed, 0x80, 0x81 })
			);
			Assert.AreEqual (
				"\U00010400",
				Decode (pool, new byte [] { 0xed, 0xa0, 0x81, 0xed, 0xb0, 0x80 })
			);

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
}
