using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Xamarin.Android.Tools.Tests;

[TestFixture]
public class AndroidDeviceExtensionsTests
{
	[Test]
	public async Task GetProcessIDAsyncRetriesUntilProcessAppears ()
	{
		var processIds = new Queue<int> (new [] { 0, 0, 1234 });

		var processId = await AndroidDeviceExtensions.GetProcessIDAsync (
			_ => Task.FromResult (processIds.Dequeue ()),
			maxAttempts: 20,
			timeBetweenAttempts: 0,
			token: CancellationToken.None
		);

		Assert.AreEqual (1234, processId);
		Assert.AreEqual (0, processIds.Count);
	}

	[Test]
	public async Task GetProcessIDAsyncStopsAfterMaximumAttempts ()
	{
		var attempts = 0;

		var processId = await AndroidDeviceExtensions.GetProcessIDAsync (
			_ => {
				attempts++;
				return Task.FromResult (0);
			},
			maxAttempts: 2,
			timeBetweenAttempts: 0,
			token: CancellationToken.None
		);

		Assert.AreEqual (0, processId);
		Assert.AreEqual (3, attempts);
	}

	[Test]
	public void GetProcessIDAsyncHonorsCancellation ()
	{
		using (var cancellationTokenSource = new CancellationTokenSource ()) {
			cancellationTokenSource.Cancel ();

			Assert.ThrowsAsync<TaskCanceledException> (() => AndroidDeviceExtensions.GetProcessIDAsync (
				_ => Task.FromResult (0),
				maxAttempts: 20,
				timeBetweenAttempts: 1,
				token: cancellationTokenSource.Token
			));
		}
	}
}
