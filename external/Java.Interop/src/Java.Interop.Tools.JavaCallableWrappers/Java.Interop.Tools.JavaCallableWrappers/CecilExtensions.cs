using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Android.Runtime;
using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.TypeNameMappings;
using Mono.Cecil;
using Mono.Cecil.Cil;
using static Java.Interop.Tools.TypeNameMappings.JavaNativeTypeManager;

namespace Java.Interop.Tools.JavaCallableWrappers.Utilities;

static class CecilExtensions
{
	public static IEnumerable<ExportFieldAttribute> GetExportFieldAttributes (Mono.Cecil.ICustomAttributeProvider p)
	{
		return GetAttributes<ExportFieldAttribute> (p, a => ToExportFieldAttribute (a));
	}

	public static IEnumerable<TAttribute> GetAttributes<TAttribute> (Mono.Cecil.ICustomAttributeProvider p, Func<CustomAttribute, TAttribute?> selector)
			where TAttribute : class
	{
		return GetAttributes (p, typeof (TAttribute).FullName!, selector);
	}

	public static IEnumerable<TAttribute> GetAttributes<TAttribute> (Mono.Cecil.ICustomAttributeProvider p, string attributeName, Func<CustomAttribute, TAttribute?> selector)
			where TAttribute : class
	{
		return p.GetCustomAttributes (attributeName)
			.Select (selector)
			.Where (v => v != null)
			.Select (v => v!);
	}

	internal static ExportFieldAttribute ToExportFieldAttribute (CustomAttribute attr)
	{
		return new ExportFieldAttribute ((string) attr.ConstructorArguments [0].Value);
	}

	public static MethodDefinition? GetBaseRegisteredMethod (MethodDefinition method, IMetadataResolver cache)
	{
		MethodDefinition bmethod;
		while ((bmethod = method.GetBaseDefinition (cache)) != method) {
			method = bmethod;

			if (HasMethodRegistrationAttributes (method)) {
				return method;
			}
		}
		return null;
	}

	internal static RegisterAttribute? ToRegisterAttribute (CustomAttribute attr)
	{
		// attr.Resolve ();
		RegisterAttribute? r = null;
		if (attr.ConstructorArguments.Count == 1)
			r = new RegisterAttribute ((string) attr.ConstructorArguments [0].Value, attr);
		else if (attr.ConstructorArguments.Count == 3)
			r = new RegisterAttribute (
					(string) attr.ConstructorArguments [0].Value,
					(string) attr.ConstructorArguments [1].Value,
					(string) attr.ConstructorArguments [2].Value,
					attr);
		if (r != null) {
			var v = attr.Properties.FirstOrDefault (p => p.Name == "DoNotGenerateAcw");
			r.DoNotGenerateAcw = v.Name == null ? false : (bool) v.Argument.Value;
		}
		return r;
	}


	internal static RegisterAttribute? RegisterFromJniTypeSignatureAttribute (CustomAttribute attr)
	{
		// attr.Resolve ();
		RegisterAttribute? r = null;
		if (attr.ConstructorArguments.Count == 1)
			r = new RegisterAttribute ((string) attr.ConstructorArguments [0].Value, attr);
		if (r != null) {
			var v = attr.Properties.FirstOrDefault (p => p.Name == "GenerateJavaPeer");
			if (v.Name == null) {
				r.DoNotGenerateAcw = false;
			} else if (v.Name == "GenerateJavaPeer") {
				r.DoNotGenerateAcw = !(bool) v.Argument.Value;
			}
			var isKeyProp = attr.Properties.FirstOrDefault (p => p.Name == "IsKeyword");
			var isKeyword = isKeyProp.Name != null && ((bool) isKeyProp.Argument.Value) == true;
			var arrRankProp = attr.Properties.FirstOrDefault (p => p.Name == "ArrayRank");
			if (arrRankProp.Name != null && arrRankProp.Argument.Value is int rank) {
				r.Name = new string ('[', rank) + (isKeyword ? r.Name : "L" + r.Name + ";");
			}
		}
		return r;
	}

	internal static RegisterAttribute? RegisterFromJniConstructorSignatureAttribute (CustomAttribute attr)
	{
		// attr.Resolve ();
		RegisterAttribute? r = null;
		if (attr.ConstructorArguments.Count == 1)
			r = new RegisterAttribute (
				name: ".ctor",
				signature: (string) attr.ConstructorArguments [0].Value,
				connector: "",
				originAttribute: attr);
		return r;
	}

	internal static RegisterAttribute? RegisterFromJniMethodSignatureAttribute (CustomAttribute attr)
	{
		// attr.Resolve ();
		RegisterAttribute? r = null;
		if (attr.ConstructorArguments.Count == 2)
			r = new RegisterAttribute ((string) attr.ConstructorArguments [0].Value,
				(string) attr.ConstructorArguments [1].Value,
				"",
				attr);
		return r;
	}

	static ExportAttribute ToExportAttribute (CustomAttribute attr, IMemberDefinition declaringMember, IMetadataResolver cache)
	{
		var name = attr.ConstructorArguments.Count > 0 ? (string) attr.ConstructorArguments [0].Value : declaringMember.Name;
		if (attr.Properties.Count == 0)
			return new ExportAttribute (name);
		var typeArgs = (CustomAttributeArgument []) attr.Properties.FirstOrDefault (p => p.Name == "Throws").Argument.Value;
		var thrown = typeArgs != null && typeArgs.Any ()
			? (from caa in typeArgs select JavaNativeTypeManager.Parse (GetJniTypeName ((TypeReference) caa.Value, cache))?.Type)
				.Where (v => v != null)
				.ToArray ()
			: null;
		var superArgs = (string) attr.Properties.FirstOrDefault (p => p.Name == "SuperArgumentsString").Argument.Value;
		return new ExportAttribute (name) { ThrownNames = thrown, SuperArgumentsString = superArgs };
	}

	static ExportAttribute ToExportAttributeFromJavaCallableAttribute (CustomAttribute attr, IMemberDefinition declaringMember)
	{
		var name = attr.ConstructorArguments.Count > 0
			? (string) attr.ConstructorArguments [0].Value
			: declaringMember.Name;
		return new ExportAttribute (name);
	}

	static ExportAttribute ToExportAttributeFromJavaCallableConstructorAttribute (CustomAttribute attr, IMemberDefinition declaringMember)
	{
		var superArgs = (string) attr.Properties
			.FirstOrDefault (p => p.Name == "SuperConstructorExpression")
			.Argument
			.Value;
		return new ExportAttribute (".ctor") {
			SuperArgumentsString = superArgs,
		};
	}

	internal static IEnumerable<RegisterAttribute> GetTypeRegistrationAttributes (Mono.Cecil.ICustomAttributeProvider p)
	{
		foreach (var a in CecilExtensions.GetAttributes<RegisterAttribute> (p, a => CecilExtensions.ToRegisterAttribute (a))) {
			yield return a;
		}
		foreach (var c in p.GetCustomAttributes ("Java.Interop.JniTypeSignatureAttribute")) {
			var r = RegisterFromJniTypeSignatureAttribute (c);
			if (r == null) {
				continue;
			}
			yield return r;
		}
	}

	// Keep in sync w/ HasMethodRegistrationAttributes()
	public static IEnumerable<RegisterAttribute> GetMethodRegistrationAttributes (Mono.Cecil.ICustomAttributeProvider p)
	{
		foreach (var a in CecilExtensions.GetAttributes<RegisterAttribute> (p, a => CecilExtensions.ToRegisterAttribute (a))) {
			yield return a;
		}
		foreach (var c in p.GetCustomAttributes ("Java.Interop.JniConstructorSignatureAttribute")) {
			var r = RegisterFromJniConstructorSignatureAttribute (c);
			if (r == null) {
				continue;
			}
			yield return r;
		}
		foreach (var c in p.GetCustomAttributes ("Java.Interop.JniMethodSignatureAttribute")) {
			var r = RegisterFromJniMethodSignatureAttribute (c);
			if (r == null) {
				continue;
			}
			yield return r;
		}
	}

	static readonly string[] MethodRegistrationAttributes = new[]{
		typeof (RegisterAttribute).FullName,
		"Java.Interop.JniConstructorSignatureAttribute",
		"Java.Interop.JniMethodSignatureAttribute",
	};

	// Keep in sync w/ GetMethodRegistrationAttributes()
	public static bool HasMethodRegistrationAttributes (Mono.Cecil.ICustomAttributeProvider p)
	{
		foreach (CustomAttribute custom_attribute in p.CustomAttributes) {
			var customAttrType  = custom_attribute.Constructor.DeclaringType.FullName;
			foreach (var t in MethodRegistrationAttributes) {
				if (customAttrType == t)
					return true;
			}
		}
		return false;
	}

	public static IEnumerable<ExportAttribute> GetExportAttributes (IMemberDefinition p, IMetadataResolver cache)
	{
		return CecilExtensions.GetAttributes<ExportAttribute> (p, a => CecilExtensions.ToExportAttribute (a, p, cache))
			.Concat (CecilExtensions.GetAttributes<ExportAttribute> (p, "Java.Interop.JavaCallableAttribute",
				a => CecilExtensions.ToExportAttributeFromJavaCallableAttribute (a, p)))
			.Concat (CecilExtensions.GetAttributes<ExportAttribute> (p, "Java.Interop.JavaCallableConstructorAttribute",
				a => CecilExtensions.ToExportAttributeFromJavaCallableConstructorAttribute (a, p)));
	}

	public static SequencePoint? LookupSource (MethodDefinition method)
	{
		if (!method.HasBody)
			return null;

		foreach (var ins in method.Body.Instructions) {
			var seqPoint = method.DebugInformation.GetSequencePoint (ins);
			if (seqPoint != null)
				return seqPoint;
		}

		return null;
	}

	public static SequencePoint? LookupSource (TypeDefinition type)
	{
		SequencePoint? candidate = null;
		foreach (var method in type.Methods) {
			if (!method.HasBody)
				continue;

			foreach (var ins in method.Body.Instructions) {
				var seq = method.DebugInformation.GetSequencePoint (ins);
				if (seq == null)
					continue;

				if (Regex.IsMatch (seq.Document.Url, ".+\\.(g|designer)\\..+"))
					break;
				if (candidate == null || seq.StartLine < candidate.StartLine)
					candidate = seq;
				break;
			}
		}

		return candidate;
	}
}
