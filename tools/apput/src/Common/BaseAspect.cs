using System;
using System.IO;

namespace ApplicationUtility;

/// <summary>
/// Base class for all aspect implementations. Manages the underlying stream and provides
/// common probe/load logging helpers.
/// </summary>
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

	protected static void LogProbeAspectStart (Type aspectType)
	{
		LogStart ($"ProbeAspect", aspectType);
	}

	protected static void LogProbeAspectEnd () => LogEnd ();

	protected static void LogLoadAspectStart (Type aspectType)
	{
		LogStart ($"LoadAspect", aspectType);
	}

	protected static void LogLoadAspectEnd () => LogEnd ();

	static void LogStart (string ofWhat, Type aspectType)
	{
		Log.StartContext ($"{ofWhat} for '{aspectType}'");
	}

	static void LogEnd ()
	{
		Log.EndContext ();
	}
}
