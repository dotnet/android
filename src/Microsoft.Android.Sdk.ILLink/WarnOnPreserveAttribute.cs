using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Linker;
using Mono.Linker.Steps;
using Xamarin.Android.Tasks;

namespace Microsoft.Android.Sdk.ILLink
{
	/// <summary>
	/// This step provides warnings when an assembly references the obsolete PresrveAttribute.
	/// The PreserveAttribute used to indicate to the linker that a type or member should not be trimmed.
	/// It had similar functionality to the newer DynamicDependencyAttribute, but was Android-specific and is now obsolete.
	/// </summary>
	public class WarnOnPreserveAttribute : BaseStep {
		protected override void ProcessAssembly (AssemblyDefinition assembly)
		{
			foreach (var module in assembly.Modules) {
				foreach (var tr in module.GetTypeReferences ()) {
					if (tr is {
						Namespace: "Android.Runtime",
						Name: "PreserveAttribute",
					}) {
						Context.LogMessage (MessageContainer.CreateCustomWarningMessage(Context, $"Assembly '{assembly.Name.Name}' contains reference to obsolete attribute 'Android.Runtime.PreserveAttribute'. Members with this attribute may be trimmed. Please use System.Diagnostics.CodeAnalysis.DynamicDependencyAttribute instead", 6000, new MessageOrigin (), WarnVersion.Latest));
						return;
					}
				}
			}
		}
	}
}
