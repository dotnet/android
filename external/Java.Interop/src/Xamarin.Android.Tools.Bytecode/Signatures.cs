using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Xamarin.Android.Tools.Bytecode {

	// http://docs.oracle.com/javase/specs/jvms/se7/html/jvms-4.html#jvms-4.3.4
	static class Signature {

		internal static string ExtractType (string signature, ref int index)
		{
			AssertSignatureIndex (signature, index);
			var i = index++;
			switch (signature [i]) {
			case '[':
				++i;
				if (i >= signature.Length)
					throw new InvalidOperationException (string.Format ("Missing array type after '[' at index {0} in: {1}", i, signature));
				string r = ExtractType (signature, ref index);
				return "[" + r;
			case 'B':
			case 'C':
			case 'D':
			case 'F':
			case 'I':
			case 'J':
			case 'S':
			case 'V':
			case 'Z':
				return signature [i].ToString ();
			case 'L':
			case 'T':
				int depth = 0;
				int e = index;
				while (e < signature.Length) {
					var c = signature [e++];
					if (depth == 0 && c == ';')
						break;

					if (c == '<')
						depth++;
					else if (c == '>')
						depth--;
				}
				if (e > signature.Length)
					throw new InvalidOperationException (string.Format ("Missing reference type after '{0}' at index {1} in: {2}", signature [i], i, signature));
				index = e;
				return signature.Substring (i, (e - i));
			default:
				throw new InvalidOperationException ("Unknown JNI Type '" + signature [i] + "' within: " + signature);
			}
		}

		internal static void AssertSignatureIndex (string signature, int index)
		{
			if (signature == null)
				throw new ArgumentNullException ("signature");
			if (signature.Length == 0)
				throw new ArgumentException ("Descriptor cannot be empty string", "descriptor");
			if (index >= signature.Length)
				throw new ArgumentException ("index >= descriptor.Length", "index");
		}

		internal static string ExtractIdentifier (string signature, ref int index)
		{
			int s = index;
			int e = s + 1;
			while (e < signature.Length && IsUnqualifiedChar (signature [e]) && signature [e] != ':')
				e++;
			index = e;
			return signature.Substring (s, e - s);
		}

		// http://docs.oracle.com/javase/specs/jvms/se7/html/jvms-4.html#jvms-4.2.2
		static bool IsUnqualifiedChar (char c)
		{
			switch (c) {
			case '.':
			case ';':
			case '[':
			case '/':
				return false;
			}
			return true;
		}

		internal static void ExtractFormalTypeParameters (ICollection<TypeParameterInfo> typeParameters, string signature, ref int index)
		{
			AssertSignatureIndex (signature, index);
			if (signature [index] != '<')
				return;
			index++;
			string t;
			while ((t = ExtractIdentifier (signature, ref index)) != null) {
				var bounds = new TypeParameterInfo {
					Identifier  = t,
				};
				typeParameters.Add (bounds);

				AssertSignatureIndex (signature, index);

				if (signature [index] == '>')
					break;

				if (signature [index] == ':') {
					index++;
					if (signature [index] != ':')
						bounds.ClassBound   = ExtractType (signature, ref index);
				}

				if (signature [index] == '>')
					break;

				AssertSignatureIndex (signature, index);

				while (signature [index] == ':') {
					index++;
					bounds.InterfaceBounds.Add (ExtractType (signature, ref index));
					AssertSignatureIndex (signature, index);
				}

				if (signature [index] == '>')
					break;
			}
			index++;
		}
	}

	public sealed class TypeParameterInfo {

		public  string          Identifier;
		public  string          ClassBound;
		public  List<string>    InterfaceBounds = new List<string> ();

		public TypeParameterInfo (string identifier = null, string classBound = null, string[] interfaceBounds = null)
		{
			Identifier  = identifier;
			ClassBound  = classBound;

			if (interfaceBounds != null)
				InterfaceBounds.AddRange (interfaceBounds);
		}

		public override string ToString ()
		{
			var sb = new StringBuilder ("TypeParameterInfo(")
				.Append (Identifier)
				.Append (" : ").Append (ClassBound);
			foreach (var iface in InterfaceBounds) {
				sb.Append (", ").Append (iface);
			}
			sb.Append (")");
			return sb.ToString ();
		}
	}

	public sealed class ClassSignature {

		public  TypeParameterInfoCollection     TypeParameters              = new TypeParameterInfoCollection ();
		public  string                          SuperclassSignature;
		public  Collection<string>              SuperinterfaceSignatures    = new Collection<string> ();

		public ClassSignature (string signature)
		{
			int index = 0;
			Signature.AssertSignatureIndex (signature, index);
			Signature.ExtractFormalTypeParameters (TypeParameters, signature, ref index);
			Signature.AssertSignatureIndex (signature, index);
			SuperclassSignature = Signature.ExtractType (signature, ref index);
			while (index < signature.Length) {
				var t = Signature.ExtractType (signature, ref index);
				SuperinterfaceSignatures.Add (t);
			}
		}

		public override string ToString ()
		{
			var sb = new StringBuilder ("ClassSignature");
			if (TypeParameters.Count > 0) {
				sb.Append ("<").Append (string.Join (", ", TypeParameters.Select (t => t.ToString ()))).Append (">");
			}
			sb.Append ("(").Append ("Superclass=").Append (SuperclassSignature);
			if (SuperinterfaceSignatures.Count > 0) {
				sb.Append (", Superinterfaces(")
					.Append (string.Join (", ", SuperinterfaceSignatures))
					.Append (")");
			}
			sb.Append (")");
			return sb.ToString ();
		}
	}

	public class TypeParameterInfoCollection : KeyedCollection<string, TypeParameterInfo> {

		protected override string GetKeyForItem (TypeParameterInfo item)
		{
			return item.Identifier;
		}
	}

	public sealed class MethodTypeSignature {

		public  TypeParameterInfoCollection     TypeParameters  = new TypeParameterInfoCollection ();
		public  Collection<string>              Parameters      = new Collection<string> ();
		public  Collection<string>              Throws          = new Collection<string> ();

		public  string                          ReturnTypeSignature;

		public MethodTypeSignature (string signature)
		{
			int index = 0;
			Signature.AssertSignatureIndex (signature, index);
			Signature.ExtractFormalTypeParameters (TypeParameters, signature, ref index);
			Signature.AssertSignatureIndex (signature, index);
			if (signature [index] != '(')
				throw new ArgumentException (string.Format ("Method signature needs to contain '(' at index {0} in: {1}", index, signature));
			index++;
			while (index < signature.Length && signature [index] != ')') {
				Parameters.Add (Signature.ExtractType (signature, ref index));
			}
			if (signature [index] != ')')
				throw new ArgumentException (string.Format ("Method signature needs to contain ')' at index {0} in: {1}", index, signature));
			index++;
			ReturnTypeSignature = Signature.ExtractType (signature, ref index);
			if (index == signature.Length)
				return;
			if (signature [index] != '^')
				throw new ArgumentException (string.Format ("Method signature should end with exception '^' types; found: '{0}' at {1} in: {2}",
						signature [index], index, signature));
			while (index < signature.Length && signature [index] == '^') {
				index++;
				var t = Signature.ExtractType (signature, ref index);
				Throws.Add (t);
			}
		}
	}
}

