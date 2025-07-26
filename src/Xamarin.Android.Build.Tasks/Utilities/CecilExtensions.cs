#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Android.Runtime;
using Java.Interop.Tools.Cecil;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Xamarin.Android.Tasks;

static class CecilExtensions
{
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

	static readonly string [] MethodRegistrationAttributes = new []{
		typeof (RegisterAttribute).FullName,
		"Java.Interop.JniConstructorSignatureAttribute",
		"Java.Interop.JniMethodSignatureAttribute",
	};

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

	// Keep in sync w/ GetMethodRegistrationAttributes()
	public static bool HasMethodRegistrationAttributes (Mono.Cecil.ICustomAttributeProvider p)
	{
		foreach (CustomAttribute custom_attribute in p.CustomAttributes) {
			var customAttrType = custom_attribute.Constructor.DeclaringType.FullName;
			foreach (var t in MethodRegistrationAttributes) {
				if (customAttrType == t)
					return true;
			}
		}
		return false;
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
