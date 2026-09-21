#nullable enable

using System.Diagnostics.Tracing;

namespace Microsoft.Android.Runtime;

internal static class RuntimeEventSource
{
	internal const string ProviderName = "Microsoft.Android.Runtime";

	static class RuntimeEventSourceHolder
	{
		internal static readonly RuntimeEventSourceImplementation Instance = new ();
	}

	[EventSource (Name = ProviderName)]
	sealed class RuntimeEventSourceImplementation : EventSource
	{
	}
}
