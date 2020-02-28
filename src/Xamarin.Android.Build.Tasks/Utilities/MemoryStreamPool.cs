using System.IO;
using System.Text;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// A class for pooling and reusing MemoryStream objects.
	/// 
	/// Based on:
	/// https://docs.microsoft.com/dotnet/standard/collections/thread-safe/how-to-create-an-object-pool
	/// https://docs.microsoft.com/dotnet/api/system.buffers.arraypool-1
	/// </summary>
	class MemoryStreamPool : ObjectPool<MemoryStream>
	{
		/// <summary>
		/// Static instance across the entire process. Use this most of the time.
		/// </summary>
		public static readonly MemoryStreamPool Shared = new MemoryStreamPool ();

		public MemoryStreamPool () : base (() => new MemoryStream ()) { }

		public override void Return (MemoryStream stream)
		{
			// We want to throw here before base.Return() if it was disposed
			stream.SetLength (0);
			base.Return (stream);
		}

		/// <summary>
		/// Creates a StreamWriter that uses the underlying MemoryStreamPool. Calling Dispose() will Return() the MemoryStream.
		/// By default uses MonoAndroidHelper.UTF8withoutBOM for the encoding.
		/// </summary>
		public StreamWriter CreateStreamWriter () => CreateStreamWriter (MonoAndroidHelper.UTF8withoutBOM);

		/// <summary>
		/// Creates a StreamWriter that uses the underlying MemoryStreamPool. Calling Dispose() will Return() the MemoryStream.
		/// </summary>
		public StreamWriter CreateStreamWriter (Encoding encoding) => new ReturningStreamWriter (this, Rent (), encoding);

		/// <summary>
		/// Creates a BinaryWriter that uses the underlying MemoryStreamPool. Calling Dispose() will Return() the MemoryStream.
		/// By default uses MonoAndroidHelper.UTF8withoutBOM for the encoding.
		/// </summary>
		public BinaryWriter CreateBinaryWriter () => CreateBinaryWriter (MonoAndroidHelper.UTF8withoutBOM);

		/// <summary>
		/// Creates a BinaryWriter that uses the underlying MemoryStreamPool. Calling Dispose() will Return() the MemoryStream.
		/// </summary>
		public BinaryWriter CreateBinaryWriter (Encoding encoding) => new ReturningBinaryWriter (this, Rent (), encoding);

		class ReturningStreamWriter : StreamWriter
		{
			readonly MemoryStreamPool pool;
			readonly MemoryStream stream;
			bool returned;

			public ReturningStreamWriter (MemoryStreamPool pool, MemoryStream stream, Encoding encoding)
				: base (stream, encoding, bufferSize: 8 * 1024, leaveOpen: true)
			{
				this.pool = pool;
				this.stream = stream;
			}

			protected override void Dispose (bool disposing)
			{
				base.Dispose (disposing);

				//NOTE: Dispose() can be called multiple times
				if (disposing && !returned) {
					returned = true;
					pool.Return (stream);
				}
			}
		}

		class ReturningBinaryWriter : BinaryWriter
		{
			readonly MemoryStreamPool pool;
			readonly MemoryStream stream;
			bool returned;

			public ReturningBinaryWriter (MemoryStreamPool pool, MemoryStream stream, Encoding encoding)
				: base (stream, encoding, leaveOpen: true)
			{
				this.pool = pool;
				this.stream = stream;
			}

			protected override void Dispose (bool disposing)
			{
				base.Dispose (disposing);

				//NOTE: Dispose() can be called multiple times
				if (disposing && !returned) {
					returned = true;
					pool.Return (stream);
				}
			}
		}
	}
}
