using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Mono.Linker;
using Mono.Linker.Steps;

using Mono.Tuner;
using Mobile.Tuner;

using Mono.Cecil;

namespace MonoDroid.Tuner {

	public class RemoveAttributes : RemoveAttributesBase {

		protected virtual bool DebugBuild {
			get { return context.LinkSymbols; }
		}

		protected override bool IsRemovedAttribute (CustomAttribute attribute)
		{
			// note: this also avoid calling FullName (which allocates a string)
			var attr_type = attribute.Constructor.DeclaringType;
			switch (attr_type.Name) {
			case "IntDefinitionAttribute":
				return attr_type.Namespace == "Android.Runtime";
			case "ObsoleteAttribute":
			// System.Mono*Attribute from mono/mcs/build/common/MonoTODOAttribute.cs
			case "MonoDocumentationNoteAttribute":
			case "MonoExtensionAttribute":
			case "MonoInternalNoteAttribute":
			case "MonoLimitationAttribute":
			case "MonoNotSupportedAttribute":
			case "MonoTODOAttribute":
				return attr_type.Namespace == "System";
			case "MonoFIXAttribute":
				return attr_type.Namespace == "System.Xml";
			// remove debugging-related attributes if we're not linking symbols (i.e. we're building release builds)
			case "DebuggableAttribute":
			case "DebuggerBrowsableAttribute":
			case "DebuggerDisplayAttribute":
			case "DebuggerHiddenAttribute":
			case "DebuggerNonUserCodeAttribute":
			case "DebuggerStepperBoundaryAttribute":
			case "DebuggerStepThroughAttribute":
			case "DebuggerTypeProxyAttribute":
			case "DebuggerVisualizerAttribute":
				return !DebugBuild && attr_type.Namespace == "System.Diagnostics";
			case "NullableContextAttribute":
			case "NullableAttribute":
				return attr_type.Namespace == "System.Runtime.CompilerServices";
			default:
				return false;
			}
		}
	}
}
