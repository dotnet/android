using Com.Xamarin.Android;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Xamarin.Android.JcwGenTests
{
	[TestClass]
	[TestCategory ("ArrayMutation")]
	public class ArrayMutationTests
	{
		[TestMethod]
		public void UpdateInt32Array ()
		{
			using var peer = new ArrayMutation ();

			Assert.AreEqual (-1, peer.UpdateInt32Array (null));

			int [] invalid = [0];
			Assert.AreEqual (1, peer.UpdateInt32Array (invalid));
			CollectionAssert.AreEqual (new int [] { 0 }, invalid);

			int [] value = [1, 2, 3];
			Assert.AreEqual (0, peer.UpdateInt32Array (value));
			CollectionAssert.AreEqual (new int [] { 2, 4, 6 }, value);
		}

		[TestMethod]
		public void UpdateInt32ArrayArray ()
		{
			using var peer = new ArrayMutation ();

			Assert.AreEqual (-1, peer.UpdateInt32ArrayArray (null));

			int [][] invalid = [[0]];
			Assert.AreEqual (1, peer.UpdateInt32ArrayArray (invalid));
			int [][] expectedInvalid = [[0]];
			CollectionAssert.AreEqual (expectedInvalid, invalid);

			int [][] value = [
				[11, 12, 13],
				[21, 22, 23],
			];
			Assert.AreEqual (0, peer.UpdateInt32ArrayArray (value));
			int [][] expected = [
				[22, 24, 26],
				[42, 44, 46],
			];
			CollectionAssert.AreEqual (expected, value);
		}

		[TestMethod]
		public void UpdateInt32ArrayArrayArray ()
		{
			using var peer = new ArrayMutation ();

			Assert.AreEqual (-1, peer.UpdateInt32ArrayArrayArray (null));

			int [][][] invalid = [[[1]]];
			Assert.AreEqual (1, peer.UpdateInt32ArrayArrayArray (invalid));
			int [][][] expectedInvalid = [[[1]]];
			CollectionAssert.AreEqual (expectedInvalid, invalid);

			int [][][] value = [
				[
					[111, 112, 113],
					[121, 122, 123],
				],
				[
					[211, 212, 213],
					[221, 222, 223],
				],
			];
			Assert.AreEqual (0, peer.UpdateInt32ArrayArrayArray (value));
			int [][][] expected = [
				[
					[222, 224, 226],
					[242, 244, 246],
				],
				[
					[422, 424, 426],
					[442, 444, 446],
				],
			];
			CollectionAssert.AreEqual (expected, value);
		}
	}
}
