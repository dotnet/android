using System;

namespace Microsoft.Android.TrimmingTests.Expectations;

// Naming follows dotnet/runtime's Mono.Linker.Tests.Cases.Expectations.
//
// Expectations must not change what is trimmed: they only take strings, never `typeof`, because a type referenced
// from a kept attribute is kept as well. An argument-free attribute on a member does not keep that member.
//
// Signature formats:
//   * types: "Namespace.Name", e.g. "Android.Runtime.JavaDictionary`2", with '/' for nested types; *TypeInAssembly also
//     takes a generic instantiation, e.g. "Android.Runtime.JavaDictionary`2<System.Int32,System.Int64>", which only
//     NativeAOT can check (By = Tool.NativeAot): ILLink keeps or removes the generic definition;
//   * methods: "Name(ParameterType,ParameterType)" with parameter types in the same form, generic instances as
//     "System.Collections.Generic.IList`1<System.Int32>" and generic parameters by name, e.g.
//     ".ctor(System.IntPtr,Android.Runtime.JniHandleOwnership)";
//   * fields, properties and events: their name.

/// <summary>The trimming configurations an expectation applies to.</summary>
[Flags]
public enum Tool
{
	None = 0,
	/// <summary>ILLink with <c>TrimMode=full</c> (CoreCLR).</summary>
	FullTrimming = 1,
	/// <summary>ILC (NativeAOT).</summary>
	NativeAot = 2,
	/// <summary>
	/// ILLink with <c>TrimMode=partial</c> (CoreCLR), which trims only trimmable assemblies. Expectations on the other
	/// assemblies (such as the app's) don't need this: the harness checks that those assemblies are kept whole instead.
	/// </summary>
	PartialTrimming = 4,
	/// <summary>ILLink in either mode (CoreCLR).</summary>
	Trimmer = FullTrimming | PartialTrimming,
	All = Trimmer | NativeAot,
}

public abstract class BaseExpectationAttribute : Attribute
{
	/// <summary>The tools this expectation applies to. Use it only where the tools legitimately differ.</summary>
	public Tool By { get; set; } = Tool.All;
}

/// <summary>The annotated type or member must survive trimming.</summary>
[AttributeUsage (AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Enum | AttributeTargets.Delegate |
	AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Event, Inherited = false)]
public sealed class KeptAttribute : BaseExpectationAttribute
{
}

/// <summary>The annotated type or member must be removed by trimming.</summary>
[AttributeUsage (AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Enum | AttributeTargets.Delegate |
	AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Event, Inherited = false)]
public sealed class RemovedAttribute : BaseExpectationAttribute
{
}

/// <summary>A member of the annotated type that cannot carry <see cref="KeptAttribute"/> itself (for example a compiler-generated one) must survive.</summary>
[AttributeUsage (AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Enum, AllowMultiple = true, Inherited = false)]
public sealed class KeptMemberAttribute : BaseExpectationAttribute
{
	public KeptMemberAttribute (string signature) => Signature = signature;

	public string Signature { get; }
}

/// <summary>A type in another assembly of the app, such as Mono.Android, must survive.</summary>
[AttributeUsage (AttributeTargets.Assembly | AttributeTargets.Class, AllowMultiple = true)]
public sealed class KeptTypeInAssemblyAttribute : BaseExpectationAttribute
{
	public KeptTypeInAssemblyAttribute (string assemblyName, string typeName)
	{
		AssemblyName = assemblyName;
		TypeName = typeName;
	}

	public string AssemblyName { get; }
	public string TypeName { get; }
}

/// <summary>A type in another assembly of the app must be removed.</summary>
[AttributeUsage (AttributeTargets.Assembly | AttributeTargets.Class, AllowMultiple = true)]
public sealed class RemovedTypeInAssemblyAttribute : BaseExpectationAttribute
{
	public RemovedTypeInAssemblyAttribute (string assemblyName, string typeName)
	{
		AssemblyName = assemblyName;
		TypeName = typeName;
	}

	public string AssemblyName { get; }
	public string TypeName { get; }
}

/// <summary>Members of a type in another assembly of the app, such as Mono.Android, must survive.</summary>
[AttributeUsage (AttributeTargets.Assembly | AttributeTargets.Class, AllowMultiple = true)]
public sealed class KeptMemberInAssemblyAttribute : BaseExpectationAttribute
{
	public KeptMemberInAssemblyAttribute (string assemblyName, string typeName, params string [] memberSignatures)
	{
		AssemblyName = assemblyName;
		TypeName = typeName;
		MemberSignatures = memberSignatures;
	}

	public string AssemblyName { get; }
	public string TypeName { get; }
	public string [] MemberSignatures { get; }
}

/// <summary>Members of a type in another assembly of the app must be removed.</summary>
[AttributeUsage (AttributeTargets.Assembly | AttributeTargets.Class, AllowMultiple = true)]
public sealed class RemovedMemberInAssemblyAttribute : BaseExpectationAttribute
{
	public RemovedMemberInAssemblyAttribute (string assemblyName, string typeName, params string [] memberSignatures)
	{
		AssemblyName = assemblyName;
		TypeName = typeName;
		MemberSignatures = memberSignatures;
	}

	public string AssemblyName { get; }
	public string TypeName { get; }
	public string [] MemberSignatures { get; }
}
