#nullable enable

using System;
using System.Diagnostics.Tracing;

#if INSIDE_MONO_ANDROID_RUNTIME
namespace Microsoft.Android.Runtime
#else
namespace Java.Interop
#endif
{
	internal static class InteropEventSource
	{
#if INSIDE_MONO_ANDROID_RUNTIME
		internal const string ProviderName = "Microsoft.Android.Runtime";
#else
		internal const string ProviderName = "Java.Interop";
#endif
		const string UnknownValue = "Unknown";
		const string RuntimeFeatureSwitchPrefix = "Microsoft.Android.Runtime.RuntimeFeature.";
		internal const EventKeywords PeerLifecycleKeyword = (EventKeywords) 0x1;
		internal const EventKeywords ReachabilityKeyword = (EventKeywords) 0x2;

		static readonly InteropEventSourceImplementation source = new InteropEventSourceImplementation ();

		internal static bool IsEnabled (EventKeywords keywords)
		{
			return source.IsEnabled (EventLevel.Informational, keywords);
		}

		internal static void ManagedPeerCreated (
				string? managedType,
				string? javaType,
				int jniIdentityHashCode,
				int managedObjectHashCode)
		{
			source.ManagedPeerCreated (
				GetPayloadValue (managedType),
				GetPayloadValue (javaType),
				jniIdentityHashCode,
				managedObjectHashCode,
				GetRuntimeFlavor ());
		}

		internal static void JavaPeerCreated (
				string? managedType,
				string? javaType,
				int jniIdentityHashCode,
				int managedObjectHashCode)
		{
			source.JavaPeerCreated (
				GetPayloadValue (managedType),
				GetPayloadValue (javaType),
				jniIdentityHashCode,
				managedObjectHashCode,
				GetRuntimeFlavor ());
		}

		internal static void ManagedPeerReleasedJavaPeer (
				string? managedType,
				string? javaType,
				int jniIdentityHashCode,
				int managedObjectHashCode)
		{
			source.ManagedPeerReleasedJavaPeer (
				GetPayloadValue (managedType),
				GetPayloadValue (javaType),
				jniIdentityHashCode,
				managedObjectHashCode,
				GetRuntimeFlavor ());
		}

		internal static void JavaPeerReleasedManagedPeer (
				string? managedType,
				string? javaType,
				int jniIdentityHashCode,
				int managedObjectHashCode)
		{
			source.JavaPeerReleasedManagedPeer (
				GetPayloadValue (managedType),
				GetPayloadValue (javaType),
				jniIdentityHashCode,
				managedObjectHashCode,
				GetRuntimeFlavor ());
		}

		internal static void ManagedPeerOnlyReachableFromJavaPeer (
				string? managedType,
				string? javaType,
				int jniIdentityHashCode,
				int managedObjectHashCode,
				int componentIndex,
				int contextIndex,
				long contextPointer)
		{
			source.ManagedPeerOnlyReachableFromJavaPeer (
				GetPayloadValue (managedType),
				GetPayloadValue (javaType),
				jniIdentityHashCode,
				managedObjectHashCode,
				GetRuntimeFlavor (),
				componentIndex,
				contextIndex,
				contextPointer);
		}

		internal static void JavaPeerOnlyReachableFromManagedPeer (
				string? managedType,
				string? javaType,
				int jniIdentityHashCode,
				int managedObjectHashCode,
				int componentIndex,
				int contextIndex,
				long contextPointer)
		{
			source.JavaPeerOnlyReachableFromManagedPeer (
				GetPayloadValue (managedType),
				GetPayloadValue (javaType),
				jniIdentityHashCode,
				managedObjectHashCode,
				GetRuntimeFlavor (),
				componentIndex,
				contextIndex,
				contextPointer);
		}

		static string GetPayloadValue (string? value)
		{
			return value ?? UnknownValue;
		}

		static string GetRuntimeFlavor ()
		{
			if (IsRuntimeFeatureEnabled ("IsNativeAotRuntime")) {
				return "NativeAOT";
			}
			if (IsRuntimeFeatureEnabled ("IsCoreClrRuntime")) {
				return "CoreCLR";
			}
			if (IsRuntimeFeatureEnabled ("IsMonoRuntime")) {
				return "MonoVM";
			}
			return UnknownValue;
		}

		static bool IsRuntimeFeatureEnabled (string feature)
		{
			return AppContext.TryGetSwitch ($"{RuntimeFeatureSwitchPrefix}{feature}", out bool isEnabled) && isEnabled;
		}

		[EventSource (Name = ProviderName)]
		sealed class InteropEventSourceImplementation : EventSource
		{
			public static class Keywords
			{
				public const EventKeywords PeerLifecycle = PeerLifecycleKeyword;
				public const EventKeywords Reachability = ReachabilityKeyword;
			}

			[Event (1, Level = EventLevel.Informational, Keywords = Keywords.PeerLifecycle)]
			public void ManagedPeerCreated (string managedType, string javaType, int jniIdentityHashCode, int managedObjectHashCode, string runtimeFlavor)
			{
				WriteEvent (1, managedType, javaType, jniIdentityHashCode, managedObjectHashCode, runtimeFlavor);
			}

			[Event (2, Level = EventLevel.Informational, Keywords = Keywords.PeerLifecycle)]
			public void JavaPeerCreated (string managedType, string javaType, int jniIdentityHashCode, int managedObjectHashCode, string runtimeFlavor)
			{
				WriteEvent (2, managedType, javaType, jniIdentityHashCode, managedObjectHashCode, runtimeFlavor);
			}

			[Event (3, Level = EventLevel.Informational, Keywords = Keywords.PeerLifecycle)]
			public void ManagedPeerReleasedJavaPeer (string managedType, string javaType, int jniIdentityHashCode, int managedObjectHashCode, string runtimeFlavor)
			{
				WriteEvent (3, managedType, javaType, jniIdentityHashCode, managedObjectHashCode, runtimeFlavor);
			}

			[Event (4, Level = EventLevel.Informational, Keywords = Keywords.PeerLifecycle)]
			public void JavaPeerReleasedManagedPeer (string managedType, string javaType, int jniIdentityHashCode, int managedObjectHashCode, string runtimeFlavor)
			{
				WriteEvent (4, managedType, javaType, jniIdentityHashCode, managedObjectHashCode, runtimeFlavor);
			}

			[Event (5, Level = EventLevel.Informational, Keywords = Keywords.Reachability)]
			public void ManagedPeerOnlyReachableFromJavaPeer (string managedType, string javaType, int jniIdentityHashCode, int managedObjectHashCode, string runtimeFlavor, int componentIndex, int contextIndex, long contextPointer)
			{
				WriteEvent (5, managedType, javaType, jniIdentityHashCode, managedObjectHashCode, runtimeFlavor, componentIndex, contextIndex, contextPointer);
			}

			[Event (6, Level = EventLevel.Informational, Keywords = Keywords.Reachability)]
			public void JavaPeerOnlyReachableFromManagedPeer (string managedType, string javaType, int jniIdentityHashCode, int managedObjectHashCode, string runtimeFlavor, int componentIndex, int contextIndex, long contextPointer)
			{
				WriteEvent (6, managedType, javaType, jniIdentityHashCode, managedObjectHashCode, runtimeFlavor, componentIndex, contextIndex, contextPointer);
			}
		}
	}
}
