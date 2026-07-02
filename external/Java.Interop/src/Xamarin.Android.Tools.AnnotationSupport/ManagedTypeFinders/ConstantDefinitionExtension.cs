using System;
using System.Collections.Generic;

namespace Xamarin.AndroidTools.AnnotationSupport
{
	public enum ConstantKind
	{
		IntDef,
		StringDef,
		Other
	}

	public class ConstantDefinitionExtension
	{
		public bool Flag { get; set; }
		public ConstantKind ConstantKind { get; set; }
		public IList<ManagedMemberInfo> ManagedConstants { get; set; }
		public string TargetManagedTypeName { get; set; }
		public bool IsTargetAlreadyEnumified {
			get { return ConstantKind == ConstantKind.IntDef && TargetManagedTypeName != "int" && TargetManagedTypeName != "System.Int32"; }
		}
	}
}

