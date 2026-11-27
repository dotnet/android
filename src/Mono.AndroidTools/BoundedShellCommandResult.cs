namespace Mono.AndroidTools
{
	internal enum BoundedShellCommandStatus
	{
		/// <summary>The output ended before any limit was reached.</summary>
		Completed,
		/// <summary>The deadline expired; whether the remote command ended is unknown.</summary>
		Timeout,
		/// <summary>The byte limit was reached; further output may have been discarded.</summary>
		ByteLimit,
		/// <summary>More lines arrived than the line limit allows.</summary>
		LineLimit,
		/// <summary>The connection, the command or the output callback failed.</summary>
		ReadFailure,
	}

	internal sealed class BoundedShellCommandResult
	{
		internal BoundedShellCommandResult (BoundedShellCommandStatus status, int bytes, int lines)
		{
			Status = status;
			Bytes = bytes;
			Lines = lines;
		}

		public BoundedShellCommandStatus Status { get; }

		/// <summary>Number of output bytes read from the host connection.</summary>
		public int Bytes { get; }

		/// <summary>Number of lines delivered to the output callback.</summary>
		public int Lines { get; }
	}
}
