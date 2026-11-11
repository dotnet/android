using System;
using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Xamarin.Android.Tools.Bytecode;

namespace Xamarin.Android.Tools.BytecodeTests {

	[TestClass]
	public class ConstantPoolFloatingPointTests {

		[TestMethod]
		[DataRow (0x00000000u)]
		[DataRow (0x80000000u)]
		[DataRow (0x00000001u)]
		[DataRow (0x7F7FFFFFu)]
		[DataRow (0x7F800000u)]
		[DataRow (0xFF800000u)]
		[DataRow (0x7FC00000u)]
		[DataRow (0x7FC12345u)]
		[DataRow (0xFFC00001u)]
		[DataRow (0x12345678u)]
		public void FloatPreservesBitPattern (uint bits)
		{
			var stream = CreatePool (4, [
				(byte) (bits >> 24),
				(byte) (bits >> 16),
				(byte) (bits >> 8),
				(byte) bits,
			]);
			var pool = new ConstantPool (stream);

			Assert.AreEqual (2, pool.Count);
			var item = (ConstantPoolFloatItem) pool [1];
			Assert.AreEqual (bits, BitConverter.SingleToUInt32Bits (item.Value));
			Assert.AreEqual (stream.Length, stream.Position);
		}

		[TestMethod]
		[DataRow (0x0000000000000000ul)]
		[DataRow (0x8000000000000000ul)]
		[DataRow (0x0000000000000001ul)]
		[DataRow (0x7FEFFFFFFFFFFFFFul)]
		[DataRow (0x7FF0000000000000ul)]
		[DataRow (0xFFF0000000000000ul)]
		[DataRow (0x7FF8000000000000ul)]
		[DataRow (0x7FF8123456789ABCul)]
		[DataRow (0xFFF8000000000001ul)]
		[DataRow (0x0123456789ABCDEFul)]
		public void DoublePreservesBitPattern (ulong bits)
		{
			var data = new byte [8];
			for (int i = 0; i < data.Length; ++i)
				data [i] = (byte) (bits >> (56 - (8 * i)));

			// Doubles occupy two constant pool slots.
			var stream = CreatePool (6, data, constantPoolCount: 3);
			var pool = new ConstantPool (stream);

			Assert.AreEqual (3, pool.Count);
			var item = (ConstantPoolDoubleItem) pool [1];
			Assert.AreSame (item, pool [2]);
			Assert.AreEqual (bits, (ulong) BitConverter.DoubleToInt64Bits (item.Value));
			Assert.AreEqual (stream.Length, stream.Position);
		}

		[TestMethod]
		[DataRow (4, 0)]
		[DataRow (4, 3)]
		[DataRow (6, 0)]
		[DataRow (6, 7)]
		public void TruncatedFloatingPointThrowsBadImageFormatException (byte tag, int availableBytes)
		{
			var stream = CreatePool (tag, new byte [availableBytes], constantPoolCount: tag == 6 ? (ushort) 3 : (ushort) 2);

			Assert.ThrowsExactly<BadImageFormatException> (() => new ConstantPool (stream));
			Assert.AreEqual (stream.Length, stream.Position);
		}

		static MemoryStream CreatePool (byte tag, byte [] data, ushort constantPoolCount = 2)
		{
			var bytes = new byte [3 + data.Length];
			bytes [0] = (byte) (constantPoolCount >> 8);
			bytes [1] = (byte) constantPoolCount;
			bytes [2] = tag;
			data.CopyTo (bytes, 3);
			return new MemoryStream (bytes);
		}
	}
}
