using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Xamarin.AndroidTools.Debugging.Java;

namespace Xamarin.Android.Tools.Tests;

[TestFixture]
public class JdwpClientTests
{
	[Test]
	public void WaitForDebuggerReadinessDoesNotTreatDebgAsReady ()
	{
		using (var stream = new DdmStream (
			includeBootStagesFeature: true,
			heloStage: "DEBG",
			ignoreReadCancellation: true))
		using (var client = new JdwpClient (stream)) {
			Assert.ThrowsAsync<TimeoutException> (() => client.WaitForDebuggerReadinessAsync (
				TimeSpan.FromMilliseconds (50),
				CancellationToken.None
			));
		}
	}

	[Test]
	public void WaitForDebuggerReadinessDoesNotTreatFeatErrorAsLegacy ()
	{
		using (var stream = new DdmStream (includeBootStagesFeature: false, featErrorCode: 99))
		using (var client = new JdwpClient (stream)) {
			Assert.ThrowsAsync<InvalidDataException> (() => client.WaitForDebuggerReadinessAsync (
				TimeSpan.FromSeconds (1),
				CancellationToken.None
			));
		}
	}

	[Test]
	public async Task WaitForDebuggerReadinessCompletesAfterAgo ()
	{
		using (var stream = new DdmStream (includeBootStagesFeature: true, heloStage: "DEBG", stagStage: "A_GO"))
		using (var client = new JdwpClient (stream)) {
			await client.WaitForDebuggerReadinessAsync (TimeSpan.FromSeconds (1), CancellationToken.None);
		}
	}

	[Test]
	public async Task WaitForDebuggerReadinessKeepsEarlyAgoObservation ()
	{
		using (var stream = new DdmStream (
			includeBootStagesFeature: true,
			heloStage: "DEBG",
			stagStage: "A_GO",
			stagBeforeHelo: true))
		using (var client = new JdwpClient (stream)) {
			await client.WaitForDebuggerReadinessAsync (TimeSpan.FromSeconds (1), CancellationToken.None);
		}
	}

	[Test]
	public void WaitForDebuggerReadinessHonorsCancellation ()
	{
		using (var cancellationSource = new CancellationTokenSource ())
		using (var stream = new DdmStream (
			includeBootStagesFeature: true,
			heloStage: "DEBG",
			onBlockedRead: cancellationSource.Cancel))
		using (var client = new JdwpClient (stream)) {
			Assert.CatchAsync<OperationCanceledException> (() => client.WaitForDebuggerReadinessAsync (
				TimeSpan.FromSeconds (1),
				cancellationSource.Token
			));
		}
	}

	[Test]
	public async Task WaitForDebuggerReadinessUsesLegacyDelayWithoutBootStages ()
	{
		TimeSpan? observedDelay = null;
		using (var stream = new DdmStream (includeBootStagesFeature: false))
		using (var client = new JdwpClient (stream)) {
			await client.WaitForDebuggerReadinessAsync (
				TimeSpan.FromSeconds (1),
				(delay, _) => {
					observedDelay = delay;
					return Task.CompletedTask;
				},
				CancellationToken.None
			);
		}

		Assert.AreEqual (TimeSpan.FromMilliseconds (1400), observedDelay);
	}

	sealed class DdmStream : Stream
	{
		const uint chunkHelo = 0x48454c4f;
		const uint chunkFeat = 0x46454154;
		const uint chunkStag = 0x53544147;
		readonly Queue<byte> reads = new Queue<byte> ();
		readonly bool includeBootStagesFeature;
		readonly string heloStage;
		readonly string stagStage;
		readonly Action onBlockedRead;
		readonly short featErrorCode;
		readonly bool ignoreReadCancellation;
		readonly bool stagBeforeHelo;
		TaskCompletionSource<int> blockedRead;
		bool blockedReadNotified;

		public DdmStream (
			bool includeBootStagesFeature,
			string heloStage = null,
			string stagStage = null,
			Action onBlockedRead = null,
			short featErrorCode = 0,
			bool ignoreReadCancellation = false,
			bool stagBeforeHelo = false)
		{
			this.includeBootStagesFeature = includeBootStagesFeature;
			this.heloStage = heloStage;
			this.stagStage = stagStage;
			this.onBlockedRead = onBlockedRead;
			this.featErrorCode = featErrorCode;
			this.ignoreReadCancellation = ignoreReadCancellation;
			this.stagBeforeHelo = stagBeforeHelo;
		}

		public override bool CanRead => true;
		public override bool CanSeek => false;
		public override bool CanWrite => true;
		public override long Length => throw new NotSupportedException ();
		public override long Position {
			get => throw new NotSupportedException ();
			set => throw new NotSupportedException ();
		}

		public override Task<int> ReadAsync (byte [] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			if (reads.Count > 0) {
				var bytesToRead = Math.Min (Math.Min (count, reads.Count), 3);
				for (int i = 0; i < bytesToRead; i++)
					buffer [offset + i] = reads.Dequeue ();
				return Task.FromResult (bytesToRead);
			}

			if (!blockedReadNotified) {
				blockedReadNotified = true;
				onBlockedRead?.Invoke ();
			}

			blockedRead = new TaskCompletionSource<int> ();
			if (!ignoreReadCancellation)
				cancellationToken.Register (() => blockedRead.TrySetCanceled ());
			return blockedRead.Task;
		}

		public override Task WriteAsync (byte [] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested ();
			var packet = new ReadOnlyMemory<byte> (buffer, offset, count);
			var id = BinaryPrimitives.ReadInt32BigEndian (packet.Slice (4, 4).Span);
			var chunkType = BinaryPrimitives.ReadUInt32BigEndian (packet.Slice (11, 4).Span);

			if (chunkType == chunkHelo) {
				if (stagBeforeHelo && stagStage != null)
					Enqueue (CreateCommand (chunkStag, Encoding.ASCII.GetBytes (stagStage)));
				Enqueue (CreateReply (id, chunkHelo, CreateHeloData (heloStage)));
			} else if (chunkType == chunkFeat) {
				Enqueue (CreateReply (id, chunkFeat, CreateFeatureData (includeBootStagesFeature), featErrorCode));
				if (!stagBeforeHelo && stagStage != null)
					Enqueue (CreateCommand (chunkStag, Encoding.ASCII.GetBytes (stagStage)));
			} else {
				throw new InvalidDataException ($"Unexpected DDM chunk type 0x{chunkType:x8}.");
			}

			return Task.CompletedTask;
		}

		void Enqueue (byte [] data)
		{
			foreach (var value in data)
				reads.Enqueue (value);
		}

		static byte [] CreateReply (int id, uint chunkType, byte [] chunkData, short errorCode = 0)
		{
			var ddmData = errorCode == 0 ? CreateChunk (chunkType, chunkData) : [];
			var packet = new byte [11 + ddmData.Length];
			BinaryPrimitives.WriteInt32BigEndian (packet.AsSpan (0, 4), packet.Length);
			BinaryPrimitives.WriteInt32BigEndian (packet.AsSpan (4, 4), id);
			packet [8] = 0x80;
			BinaryPrimitives.WriteInt16BigEndian (packet.AsSpan (9, 2), errorCode);
			ddmData.CopyTo (packet, 11);
			return packet;
		}

		static byte [] CreateCommand (uint chunkType, byte [] chunkData)
		{
			var ddmData = CreateChunk (chunkType, chunkData);
			var packet = new byte [11 + ddmData.Length];
			BinaryPrimitives.WriteInt32BigEndian (packet.AsSpan (0, 4), packet.Length);
			BinaryPrimitives.WriteInt32BigEndian (packet.AsSpan (4, 4), 900);
			packet [9] = 0xc7;
			packet [10] = 0x01;
			ddmData.CopyTo (packet, 11);
			return packet;
		}

		static byte [] CreateChunk (uint chunkType, byte [] chunkData)
		{
			var data = new byte [8 + chunkData.Length];
			BinaryPrimitives.WriteUInt32BigEndian (data.AsSpan (0, 4), chunkType);
			BinaryPrimitives.WriteInt32BigEndian (data.AsSpan (4, 4), chunkData.Length);
			chunkData.CopyTo (data, 8);
			return data;
		}

		static byte [] CreateHeloData (string stage)
		{
			var data = new byte [stage == null ? 33 : 37];
			var offset = 0;
			WriteInt32 (data, ref offset, 1);
			WriteInt32 (data, ref offset, 1234);
			WriteInt32 (data, ref offset, 0);
			WriteInt32 (data, ref offset, 0);
			WriteInt32 (data, ref offset, 0);
			WriteInt32 (data, ref offset, 0);
			WriteInt32 (data, ref offset, 0);
			data [offset++] = 0;
			WriteInt32 (data, ref offset, 0);
			if (stage != null)
				Encoding.ASCII.GetBytes (stage, 0, stage.Length, data, offset);
			return data;
		}

		static byte [] CreateFeatureData (bool includeBootStagesFeature)
		{
			if (!includeBootStagesFeature)
				return new byte [4];

			const string feature = "support_boot_stages";
			var featureBytes = Encoding.BigEndianUnicode.GetBytes (feature);
			var data = new byte [8 + featureBytes.Length];
			BinaryPrimitives.WriteInt32BigEndian (data.AsSpan (0, 4), 1);
			BinaryPrimitives.WriteInt32BigEndian (data.AsSpan (4, 4), feature.Length);
			featureBytes.CopyTo (data, 8);
			return data;
		}

		static void WriteInt32 (byte [] data, ref int offset, int value)
		{
			BinaryPrimitives.WriteInt32BigEndian (data.AsSpan (offset, 4), value);
			offset += 4;
		}

		public override void Flush ()
		{
		}

		public override int Read (byte [] buffer, int offset, int count)
		{
			throw new NotSupportedException ();
		}

		public override long Seek (long offset, SeekOrigin origin)
		{
			throw new NotSupportedException ();
		}

		public override void SetLength (long value)
		{
			throw new NotSupportedException ();
		}

		public override void Write (byte [] buffer, int offset, int count)
		{
			throw new NotSupportedException ();
		}

		protected override void Dispose (bool disposing)
		{
			if (disposing)
				blockedRead?.TrySetException (new IOException ("The stream was closed."));
			base.Dispose (disposing);
		}
	}
}
