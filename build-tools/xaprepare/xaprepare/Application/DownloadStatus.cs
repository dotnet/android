using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;

namespace Xamarin.Android.Prepare
{
	class DownloadStatus
	{
		const uint DefaultUpdateInterval = 1000;
		readonly object updateLock = new object ();

		public static readonly DownloadStatus Empty = new DownloadStatus (0, _ => { });

		ConcurrentQueue<ulong> byteSnapshots;
		Stopwatch watch;
		Action<DownloadStatus> updater;

		public ulong TotalSize { get; }
		public ulong DownloadedSoFar { get; set; }
		public ulong BytesPerSecond { get; set; }
		public uint UpdateIntervalMS { get; set; } = DefaultUpdateInterval;

		public DownloadStatus (ulong totalSize, Action<DownloadStatus> updaterCallback)
		{
			if (updaterCallback == null)
				throw new ArgumentNullException (nameof (updaterCallback));

			TotalSize = totalSize;
			DownloadedSoFar = 0;
			BytesPerSecond = 0;
			byteSnapshots = new ConcurrentQueue<ulong> ();
			watch = new Stopwatch ();
			updater = updaterCallback;
		}

		public void Start () => watch.Restart ();

		public void Update (ulong bytesRead)
		{
			if (bytesRead == 0)
				return;

			bool timeForUpdate = watch.ElapsedMilliseconds >= UpdateIntervalMS;
			if (timeForUpdate) {
				lock (updateLock) {
					StoreAndUpdate (bytesRead, timeForUpdate);
				}
			} else {
				StoreAndUpdate (bytesRead, timeForUpdate);
			}
		}

		void StoreAndUpdate (ulong bytesRead, bool timeForUpdate)
		{
			byteSnapshots.Enqueue (bytesRead);
			if (!timeForUpdate)
				return;

			ulong[] snapshots = byteSnapshots.ToArray ();
			while (!byteSnapshots.IsEmpty)
				byteSnapshots.TryDequeue (out ulong _);

			// LINQ has no overloads for UInt64 (!?), so we do it by hand...
			ulong bytesPerSecond = 0;
			foreach (ulong u in snapshots) {
				DownloadedSoFar += u;
				bytesPerSecond += u;
			}
			BytesPerSecond = bytesPerSecond;

			updater (this);
			watch.Restart ();
		}
	}
}
