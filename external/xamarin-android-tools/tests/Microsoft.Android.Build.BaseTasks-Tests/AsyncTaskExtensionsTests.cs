using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Android.Build.Tasks;
using NUnit.Framework;
using Xamarin.Build;

namespace Microsoft.Android.Build.BaseTasks.Tests
{
	[TestFixture]
	public class AsyncTaskExtensionsTests
	{
		const int Iterations = 32;

		[Test]
		public async Task RunTask ()
		{
			bool set = false;
			await new AsyncTask ().RunTask (delegate { set = true; }); // delegate { } has void return type
			Assert.IsTrue (set);
		}

		[Test]
		public async Task RunTaskOfT ()
		{
			bool set = false;
			Assert.IsTrue (await new AsyncTask ().RunTask (() => set = true), "RunTask should return true");
			Assert.IsTrue (set);
		}

		[Test]
		public async Task WhenAll ()
		{
			bool set = false;
			await new AsyncTask ().WhenAll (new [] { 0 }, _ => set = true);
			Assert.IsTrue (set);
		}

		[Test]
		public async Task WhenAllWithLock ()
		{
			var input = new int [Iterations];
			var output = new List<int> ();
			await new AsyncTask ().WhenAllWithLock (input, (i, l) => {
				lock (l) output.Add (i);
			});
			Assert.AreEqual (Iterations, output.Count);
		}

		[Test]
		public void ParallelForEach ()
		{
			bool set = false;
			new AsyncTask ().ParallelForEach (new [] { 0 }, _ => set = true);
			Assert.IsTrue (set);
		}

		[Test]
		public void ParallelForEachWithLock ()
		{
			var input = new int [Iterations];
			var output = new List<int> ();
			new AsyncTask ().ParallelForEachWithLock (input, (i, l) => {
				lock (l) output.Add (i);
			});
			Assert.AreEqual (Iterations, output.Count);
		}
	}
}
