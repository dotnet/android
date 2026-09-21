#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Reflection;

using NUnit.Framework;

namespace Android.RuntimeTests
{
	[TestFixture]
	public class RuntimeEventSourceTests
	{
		[Test]
		[DynamicDependency (DynamicallyAccessedMemberTypes.All, "Microsoft.Android.Runtime.RuntimeEventSource", "Mono.Android")]
		public void ProviderFoundation ()
		{
			Assert.IsTrue (AppContext.TryGetSwitch ("System.Diagnostics.Tracing.EventSource.IsSupported", out bool enabled) && enabled,
				"The runtime EventSource feature switch should be enabled for this test application.");

			var eventSourceType = Type.GetType ("Microsoft.Android.Runtime.RuntimeEventSource, Mono.Android", throwOnError: true)
				?? throw new InvalidOperationException ("Could not find the runtime EventSource.");
			Assert.AreEqual ("Microsoft.Android.Runtime", GetConstant<string> (eventSourceType, "ProviderName"));
			var implementationType = eventSourceType.GetNestedType ("RuntimeEventSourceImplementation", BindingFlags.NonPublic)
				?? throw new InvalidOperationException ("Could not find the runtime EventSource implementation.");
			var eventSourceAttribute = implementationType.GetCustomAttribute<EventSourceAttribute> ()
				?? throw new InvalidOperationException ("The runtime EventSource implementation did not have EventSourceAttribute.");
			Assert.AreEqual ("Microsoft.Android.Runtime", eventSourceAttribute.Name);
			Assert.IsFalse (
				implementationType.GetMethods (BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
					.Any (method => method.GetCustomAttribute<EventAttribute> () is not null),
				"The foundation should not define events owned by later instrumentation layers.");

			using var listener = new CapturingEventListener ();
			var holderType = eventSourceType.GetNestedType ("RuntimeEventSourceHolder", BindingFlags.NonPublic)
				?? throw new InvalidOperationException ("Could not find the runtime EventSource holder.");
			var instanceField = holderType.GetField ("Instance", BindingFlags.NonPublic | BindingFlags.Static)
				?? throw new InvalidOperationException ("Could not find the runtime EventSource instance.");
			var eventSource = instanceField.GetValue (null) as EventSource
				?? throw new InvalidOperationException ("Could not create the runtime EventSource.");
			Assert.IsTrue (listener.ProviderCreated, "The EventListener should observe provider creation.");
			GC.KeepAlive (eventSource);
		}

		static T GetConstant<T> (Type type, string name)
		{
			var field = type.GetField (name, BindingFlags.NonPublic | BindingFlags.Static)
				?? throw new InvalidOperationException ($"Could not find {type.FullName}.{name}.");
			var value = field.GetRawConstantValue ()
				?? throw new InvalidOperationException ($"{type.FullName}.{name} did not have a constant value.");
			return (T) value;
		}

		sealed class CapturingEventListener : EventListener
		{
			public bool ProviderCreated { get; private set; }

			protected override void OnEventSourceCreated (EventSource eventSource)
			{
				if (eventSource.Name == "Microsoft.Android.Runtime") {
					ProviderCreated = true;
					EnableEvents (eventSource, EventLevel.Informational, EventKeywords.All);
				}
			}
		}
	}
}
