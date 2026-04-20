using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;

using NUnit.Framework;

namespace System.Drawing {

	[TestFixture]
	public class TypeConverterTest {

		[Test]
		[RequiresUnreferencedCode ("Tests trimming unsafe features")]
		public void ColorConverter ()
		{
			var typeConverter = TypeDescriptor.GetConverter (typeof (Color));
			var color = (Color)typeConverter.ConvertFromString ("#FFAAEE");

			Assert.AreEqual (0xFF, color.A, "A");
			Assert.AreEqual (0xFF, color.R, "R");
			Assert.AreEqual (0xAA, color.G, "G");
			Assert.AreEqual (0xEE, color.B, "B");
		}

		[Test]
		[RequiresUnreferencedCode ("Tests trimming unsafe features")]
		public void RectangleConverter ()
		{
			var typeConverter = TypeDescriptor.GetConverter (typeof (Rectangle));
			var rect = (Rectangle)typeConverter.ConvertFromString ("10, 20, 30, 40");

			Assert.AreEqual (10, rect.X, "X");
			Assert.AreEqual (20, rect.Y, "Y");
			Assert.AreEqual (30, rect.Width, "Width");
			Assert.AreEqual (40, rect.Height, "Height");
		}

		[Test]
		[RequiresUnreferencedCode ("Tests trimming unsafe features")]
		public void PointConverter ()
		{
			var typeConverter = TypeDescriptor.GetConverter (typeof (Point));
			var point = (Point)typeConverter.ConvertFromString ("10, 20");

			Assert.AreEqual (10, point.X, "X");
			Assert.AreEqual (20, point.Y, "Y");
		}

		[Test]
		[RequiresUnreferencedCode ("Tests trimming unsafe features")]
		public void SizeConverter ()
		{
			var typeConverter = TypeDescriptor.GetConverter (typeof (Size));
			var size = (Size)typeConverter.ConvertFromString ("10, 20");

			Assert.AreEqual (10, size.Width, "Width");
			Assert.AreEqual (20, size.Height, "Height");
		}

		[Test]
		[RequiresUnreferencedCode ("Tests trimming unsafe features")]
		public void SizeFConverter ()
		{
			var typeConverter = TypeDescriptor.GetConverter (typeof (SizeF));
			var sizeF = (SizeF)typeConverter.ConvertFromString ("10.5, 20.5");

			Assert.AreEqual (10.5, sizeF.Width, "Width");
			Assert.AreEqual (20.5, sizeF.Height, "Height");
		}
	}
}
