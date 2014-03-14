using System;
using System.Linq;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class TestTypeTests
	{
		int lrefStartCount;

		[TestFixtureSetUp]
		public void StartArrayTests ()
		{
			lrefStartCount  = JniEnvironment.Current.LocalReferenceCount;
		}

		[TestFixtureTearDown]
		public void EndArrayTests ()
		{
			int lref    = JniEnvironment.Current.LocalReferenceCount;
			Assert.AreEqual (lrefStartCount, lref, "JNI local references");
		}

		[Test]
		public void TestCase ()
		{
			using (var t = new TestType ()) {
				t.RunTests ();
			}
		}

		[Test]
		public void UpdateInt32Array ()
		{
			using (var t = new TestType ()) {
				Assert.AreEqual (-1, t.UpdateInt32Array (null));
				int[] value = new [] { 0 };
				Assert.AreEqual (1, t.UpdateInt32Array (value));
				value = new[]{ 1, 2, 3 };
				Assert.AreEqual (0, t.UpdateInt32Array (value));
				Assert.IsTrue (new[]{ 2, 4, 6 }.SequenceEqual (value));
			}
		}

		[Test]
		public void UpdateInt32ArrayArray ()
		{
			using (var t = new TestType ()) {
				Assert.AreEqual (-1, t.UpdateInt32ArrayArray (null));
				int[][] value = new [] {
					new []{0},
				};
				Assert.AreEqual (1, t.UpdateInt32ArrayArray (value));
				value = new int[][] {
					new int[] {11, 12, 13},
					new int[] {21, 22, 23},
				};
				Assert.AreEqual (0, t.UpdateInt32ArrayArray (value));
				Assert.IsTrue (new[]{ 22, 24, 26 }.SequenceEqual (value [0]));
				Assert.IsTrue (new[]{ 42, 44, 46 }.SequenceEqual (value [1]));
			}
		}

		[Test]
		public void UpdateInt32ArrayArrayArray ()
		{
			using (var t = new TestType ()) {
				Assert.AreEqual (-1, t.UpdateInt32ArrayArrayArray (null));
				int[][][] value = new [] {
					new []{new[]{1}},
				};
				Assert.AreEqual (1, t.UpdateInt32ArrayArrayArray (value));
				value = new int[][][] {
					new int[][] {
						new int[]{111, 112, 113},
						new int[]{121, 122, 123},
					},
					new int[][] {
						new int[]{211, 212, 213},
						new int[]{221, 222, 223},
					},
				};
				Assert.AreEqual (0, t.UpdateInt32ArrayArrayArray (value));
				Assert.IsTrue (new[]{ 222, 224, 226 }.SequenceEqual (value [0][0]));
				Assert.IsTrue (new[]{ 242, 244, 246 }.SequenceEqual (value [0][1]));
				Assert.IsTrue (new[]{ 422, 424, 426 }.SequenceEqual (value [1][0]));
				Assert.IsTrue (new[]{ 442, 444, 446 }.SequenceEqual (value [1][1]));
			}
		}

		[Test]
		public void Identity ()
		{
			using (var t = new TestType ()) {
				for (int i = 0; i < 10; ++i)
					Assert.AreEqual (i, t.Identity (i));
			}
		}

		[Test]
		public void StaticIdentity ()
		{
			for (int i = 0; i < 10; ++i)
				Assert.AreEqual (i, TestType.StaticIdentity (i));
		}
	}
}

