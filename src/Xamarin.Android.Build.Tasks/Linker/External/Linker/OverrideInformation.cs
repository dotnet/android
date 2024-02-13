using System.Diagnostics;
using Mono.Cecil;

namespace Mono.Linker {
	[DebuggerDisplay ("{Override}")]
	public class OverrideInformation {
		public readonly MethodDefinition Base;
		public readonly MethodDefinition Override;
		public readonly InterfaceImplementation MatchingInterfaceImplementation;

		public OverrideInformation (MethodDefinition @base, MethodDefinition @override, InterfaceImplementation matchingInterfaceImplementation = null)
		{
			Base = @base;
			Override = @override;
			MatchingInterfaceImplementation = matchingInterfaceImplementation;
		}

		public bool IsOverrideOfInterfaceMember
		{
			get
			{
				if (MatchingInterfaceImplementation != null)
					return true;

				return Base.DeclaringType.IsInterface;
			}
		}
	}
}
