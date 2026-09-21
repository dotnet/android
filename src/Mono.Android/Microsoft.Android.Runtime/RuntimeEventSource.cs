#nullable enable

using System.Diagnostics.Tracing;

namespace Microsoft.Android.Runtime;

internal static class RuntimeEventSource
{
	internal const string ProviderName = "Microsoft.Android.Runtime";

	internal static bool IsEnabled (EventKeywords keywords)
	{
		return RuntimeFeature.EventSourceSupport &&
			RuntimeEventSourceHolder.Instance.IsEnabled (EventLevel.Informational, keywords);
	}

	static class RuntimeEventSourceHolder
	{
		internal static readonly RuntimeEventSourceImplementation Instance = new ();
	}

	[EventSource (Name = ProviderName)]
	sealed class RuntimeEventSourceImplementation : EventSource
	{
	}
}
