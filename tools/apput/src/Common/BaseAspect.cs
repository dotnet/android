using System;
using System.IO;

namespace ApplicationUtility;

public abstract class BaseAspect : IAspect
{
	bool disposed;
	readonly Stream? stream;

	public abstract string AspectName { get; }

	protected Stream AspectStream => stream ?? throw new InvalidOperationException ("Internal error: aspect stream is null");
	protected bool Disposed => disposed;

	protected BaseAspect (Stream? aspectStream)
	{
		stream = aspectStream;
	}

	protected virtual void Dispose (bool disposing)
	{
		if (disposed) {
			return;
		}

		if (disposing) {
			AspectStream?.Close ();
			AspectStream?.Dispose ();
		}

		disposed = true;
	}

	public void Dispose ()
	{
		Dispose (disposing: true);
		GC.SuppressFinalize (this);
	}
}
