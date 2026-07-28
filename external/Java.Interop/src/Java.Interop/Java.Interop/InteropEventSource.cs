#nullable enable

using System.Diagnostics.Tracing;

namespace Java.Interop
{
	public static class InteropEventSource
	{
		internal const string ProviderName = "Java.Interop";
		const string UnknownValue = "Unknown";

		static readonly InteropEventSourceImplementation source = new InteropEventSourceImplementation ();

		public static bool IsEnabled ()
		{
			return source.IsEnabled ();
		}

		public static void DotNetWrapperCreated (
				string? managedType,
				string? javaType,
				int jniIdentityHashCode,
				int managedObjectHashCode,
				string? runtimeMode)
		{
			source.DotNetWrapperCreated (
				GetPayloadValue (managedType),
				GetPayloadValue (javaType),
				jniIdentityHashCode,
				managedObjectHashCode,
				GetPayloadValue (runtimeMode));
		}

		public static void JavaWrapperCreated (
				string? managedType,
				string? javaType,
				int jniIdentityHashCode,
				int managedObjectHashCode,
				string? runtimeMode)
		{
			source.JavaWrapperCreated (
				GetPayloadValue (managedType),
				GetPayloadValue (javaType),
				jniIdentityHashCode,
				managedObjectHashCode,
				GetPayloadValue (runtimeMode));
		}

		public static void DotNetWrapperReleasedJavaReference (
				string? managedType,
				string? javaType,
				int jniIdentityHashCode,
				int managedObjectHashCode,
				string? runtimeMode)
		{
			source.DotNetWrapperReleasedJavaReference (
				GetPayloadValue (managedType),
				GetPayloadValue (javaType),
				jniIdentityHashCode,
				managedObjectHashCode,
				GetPayloadValue (runtimeMode));
		}

		public static void JavaWrapperReleasedDotNetReference (
				string? managedType,
				string? javaType,
				int jniIdentityHashCode,
				int managedObjectHashCode,
				string? runtimeMode)
		{
			source.JavaWrapperReleasedDotNetReference (
				GetPayloadValue (managedType),
				GetPayloadValue (javaType),
				jniIdentityHashCode,
				managedObjectHashCode,
				GetPayloadValue (runtimeMode));
		}

		public static void DotNetObjectOnlyReachableFromJava (
				string? managedType,
				string? javaType,
				int jniIdentityHashCode,
				int managedObjectHashCode,
				string? runtimeMode,
				int componentIndex,
				int contextIndex,
				long contextPointer)
		{
			source.DotNetObjectOnlyReachableFromJava (
				GetPayloadValue (managedType),
				GetPayloadValue (javaType),
				jniIdentityHashCode,
				managedObjectHashCode,
				GetPayloadValue (runtimeMode),
				componentIndex,
				contextIndex,
				contextPointer);
		}

		public static void JavaObjectOnlyReachableFromDotNet (
				string? managedType,
				string? javaType,
				int jniIdentityHashCode,
				int managedObjectHashCode,
				string? runtimeMode,
				int componentIndex,
				int contextIndex,
				long contextPointer)
		{
			source.JavaObjectOnlyReachableFromDotNet (
				GetPayloadValue (managedType),
				GetPayloadValue (javaType),
				jniIdentityHashCode,
				managedObjectHashCode,
				GetPayloadValue (runtimeMode),
				componentIndex,
				contextIndex,
				contextPointer);
		}

		static string GetPayloadValue (string? value)
		{
			return value ?? UnknownValue;
		}

		[EventSource (Name = ProviderName)]
		sealed class InteropEventSourceImplementation : EventSource
		{
			public static class Keywords
			{
				public const EventKeywords WrapperLifecycle = (EventKeywords) 0x1;
				public const EventKeywords Reachability = (EventKeywords) 0x2;
			}

			[Event (1, Level = EventLevel.Informational, Keywords = Keywords.WrapperLifecycle)]
			public void DotNetWrapperCreated (string managedType, string javaType, int jniIdentityHashCode, int managedObjectHashCode, string runtimeMode)
			{
				WriteEvent (1, managedType, javaType, jniIdentityHashCode, managedObjectHashCode, runtimeMode);
			}

			[Event (2, Level = EventLevel.Informational, Keywords = Keywords.WrapperLifecycle)]
			public void JavaWrapperCreated (string managedType, string javaType, int jniIdentityHashCode, int managedObjectHashCode, string runtimeMode)
			{
				WriteEvent (2, managedType, javaType, jniIdentityHashCode, managedObjectHashCode, runtimeMode);
			}

			[Event (3, Level = EventLevel.Informational, Keywords = Keywords.WrapperLifecycle)]
			public void DotNetWrapperReleasedJavaReference (string managedType, string javaType, int jniIdentityHashCode, int managedObjectHashCode, string runtimeMode)
			{
				WriteEvent (3, managedType, javaType, jniIdentityHashCode, managedObjectHashCode, runtimeMode);
			}

			[Event (4, Level = EventLevel.Informational, Keywords = Keywords.WrapperLifecycle)]
			public void JavaWrapperReleasedDotNetReference (string managedType, string javaType, int jniIdentityHashCode, int managedObjectHashCode, string runtimeMode)
			{
				WriteEvent (4, managedType, javaType, jniIdentityHashCode, managedObjectHashCode, runtimeMode);
			}

			[Event (5, Level = EventLevel.Informational, Keywords = Keywords.Reachability)]
			public void DotNetObjectOnlyReachableFromJava (string managedType, string javaType, int jniIdentityHashCode, int managedObjectHashCode, string runtimeMode, int componentIndex, int contextIndex, long contextPointer)
			{
				WriteEvent (5, managedType, javaType, jniIdentityHashCode, managedObjectHashCode, runtimeMode, componentIndex, contextIndex, contextPointer);
			}

			[Event (6, Level = EventLevel.Informational, Keywords = Keywords.Reachability)]
			public void JavaObjectOnlyReachableFromDotNet (string managedType, string javaType, int jniIdentityHashCode, int managedObjectHashCode, string runtimeMode, int componentIndex, int contextIndex, long contextPointer)
			{
				WriteEvent (6, managedType, javaType, jniIdentityHashCode, managedObjectHashCode, runtimeMode, componentIndex, contextIndex, contextPointer);
			}
		}
	}
}
