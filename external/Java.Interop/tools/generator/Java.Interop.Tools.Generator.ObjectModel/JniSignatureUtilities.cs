using System;
using System.Collections.Generic;

namespace MonoDroid.Generation
{
	static class JniSignatureUtilities
	{
		public static bool AreAbiCompatible (string expected, string actual)
		{
			if (!TryParseType (expected, allowVoid: true, allowTypeVariable: true, out var expectedType) ||
					!TryParseType (actual, allowVoid: true, allowTypeVariable: false, out var actualType))
				return false;

			return GetAbiType (expectedType) == GetAbiType (actualType);
		}

		public static bool TryParseMethodSignature (string signature, out string [] parameters, out string returnType)
		{
			var types = new List<string> ();
			parameters = [];
			returnType = "";

			if (signature.Length < 3 || signature [0] != '(')
				return false;

			int index = 1;
			while (index < signature.Length && signature [index] != ')') {
				if (!TryReadType (signature, ref index, allowVoid: false, allowTypeVariable: false, out var parameter))
					return false;
				types.Add (parameter);
			}
			if (index >= signature.Length || signature [index] != ')')
				return false;

			index++;
			if (!TryReadType (signature, ref index, allowVoid: true, allowTypeVariable: false, out returnType) || index != signature.Length)
				return false;

			parameters = types.ToArray ();
			return true;
		}

		static string GetAbiType (string type)
		{
			return type [0] == 'L' || type [0] == '[' || type [0] == 'T' ? "L" : type;
		}

		static bool TryParseType (string signature, bool allowVoid, bool allowTypeVariable, out string type)
		{
			int index = 0;
			return TryReadType (signature, ref index, allowVoid, allowTypeVariable, out type) && index == signature.Length;
		}

		static bool TryReadType (string signature, ref int index, bool allowVoid, bool allowTypeVariable, out string type)
		{
			type = "";
			int start = index;

			while (index < signature.Length && signature [index] == '[')
				index++;
			if (index >= signature.Length)
				return false;

			bool isArray = index > start;
			char kind = signature [index++];
			if (kind == 'L' || (kind == 'T' && allowTypeVariable)) {
				int end = signature.IndexOf (';', index);
				if (end <= index)
					return false;
				index = end + 1;
			} else if ("ZBCSIJFD".IndexOf (kind) < 0 && (kind != 'V' || !allowVoid || isArray)) {
				return false;
			}

			type = signature.Substring (start, index - start);
			return true;
		}
	}
}
