using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Android.Build.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Android.Build.BaseTasks.Tests
{
	[TestClass]
	public class AsyncTaskExtensionsTests
	{
		const int Iterations = 32;

		class TestAsyncTask : AsyncTask
		{
			public override string TaskPrefix => "TEST";
		}

		[TestMethod]
		public async Task RunTask ()
		{
			bool set = false;
			await new TestAsyncTask ().RunTask (delegate { set = true; }); // delegate { } has void return type
			Assert.IsTrue (set);
		}

		[TestMethod]
		public async Task RunTaskOfT ()
		{
			bool set = false;
			Assert.IsTrue (await new TestAsyncTask ().RunTask (() => set = true), "RunTask should return true");
			Assert.IsTrue (set);
		}

		[TestMethod]
		public async Task WhenAll ()
		{
			bool set = false;
			await new TestAsyncTask ().WhenAll (new [] { 0 }, _ => set = true);
			Assert.IsTrue (set);
		}

		[TestMethod]
		public async Task WhenAllWithLock ()
		{
			var input = new int [Iterations];
			var output = new List<int> ();
			await new TestAsyncTask ().WhenAllWithLock (input, (i, l) => {
				lock (l) output.Add (i);
			});
			Assert.AreEqual (Iterations, output.Count);
		}

		[TestMethod]
		public void ParallelForEach ()
		{
			bool set = false;
			new TestAsyncTask ().ParallelForEach (new [] { 0 }, _ => set = true);
			Assert.IsTrue (set);
		}

		[TestMethod]
		public void ParallelForEachWithLock ()
		{
			var input = new int [Iterations];
			var output = new List<int> ();
			new TestAsyncTask ().ParallelForEachWithLock (input, (i, l) => {
				lock (l) output.Add (i);
			});
			Assert.AreEqual (Iterations, output.Count);
		}
	}
}
