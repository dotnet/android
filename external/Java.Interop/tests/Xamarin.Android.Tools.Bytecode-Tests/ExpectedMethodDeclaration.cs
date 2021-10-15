using System;
using System.Collections.Generic;
using System.Linq;

using Xamarin.Android.Tools.Bytecode;

using NAssert   = NUnit.Framework.Assert;

namespace Xamarin.Android.Tools.BytecodeTests {

	class ExpectedMethodDeclaration {

		public  string                      Name;
		public  MethodAccessFlags           AccessFlags;
		public  bool                        Deprecated;
		public  List<TypeParameterInfo>     TypeParameters              = new List<TypeParameterInfo> ();
		public  string                      ReturnDescriptor;
		public  string                      ReturnGenericDescriptor;
		public  List<ParameterInfo>         Parameters                  = new List<ParameterInfo> ();
		public  List<TypeInfo>              Throws                      = new List<TypeInfo> ();

		public void Assert (MethodInfo method)
		{
			string message = Name +
				"(" + string.Join ("", Parameters.Select (p => p.Type.TypeSignature ?? p.Type.BinaryName)) + ")" +
				ReturnDescriptor;

			NAssert.AreEqual (AccessFlags,       method.AccessFlags,            message);
			NAssert.AreEqual (Name,              method.Name,                   message);
			NAssert.AreEqual (ReturnDescriptor,  method.ReturnType.BinaryName,  message);
			if (ReturnGenericDescriptor != null) {
				NAssert.AreEqual (ReturnGenericDescriptor,  method.ReturnType.TypeSignature,    message);
			}

			NAssert.AreEqual (Deprecated,   method.Attributes.Get<DeprecatedAttribute>() != null,   message);

			var signature = method.GetSignature ();
			if (signature != null) {
				NAssert.AreEqual (TypeParameters.Count, signature.TypeParameters.Count,     message);
				for (int i = 0; i < TypeParameters.Count; ++i) {
					NAssert.AreEqual (TypeParameters [i].ToString (),   signature.TypeParameters [i].ToString (),   message);
				}
			}

			var parameters  = method.GetParameters ();
			NAssert.AreEqual (Parameters.Count, parameters.Length,  $"Method {Name} Parameter Count\n" +
				$"Expected signature: {string.Join (", ", Parameters.Select (p => $"{p.Type.TypeSignature}: {p.Name}"))}\n" +
				$"  Actual signature: {string.Join (", ", parameters.Select (p => $"{p.Type.TypeSignature}: {p.Name}"))}");
			for (int i = 0; i < Parameters.Count; ++i) {
				NAssert.AreEqual (Parameters [i].Name,                  parameters [i].Name,                message + $": parameter {i} name");
				NAssert.AreEqual (i,                                    parameters [i].Position,            message + $": parameter {i} position");
				NAssert.AreEqual (Parameters [i].Type.BinaryName,       parameters [i].Type.BinaryName,     message + $": parameter {i} binary name");
				NAssert.AreEqual (Parameters [i].Type.TypeSignature,    parameters [i].Type.TypeSignature,  message + $": parameter {i} type signature");
			}

			var exceptions  = method.GetThrows ();
			NAssert.AreEqual (Throws.Count, exceptions.Count);
			for (int i = 0; i < Throws.Count; ++i) {
				NAssert.AreEqual (Throws [i].BinaryName,    exceptions [i].BinaryName,      message + $": throws {i} binary name");
				NAssert.AreEqual (Throws [i].TypeSignature, exceptions [i].TypeSignature,   message + $": throws {i} type signature");
			}
		}
	}
}

