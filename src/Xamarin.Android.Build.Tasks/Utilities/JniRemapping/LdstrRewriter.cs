#nullable enable

using System;
using System.Collections.Generic;

namespace Xamarin.Android.Tasks.JniRemapping
{
	/// <summary>
	/// Classifies and rewrites the various forms of JNI-name-bearing string literals that
	/// Java.Interop / the binding generator embed in IL as <c>ldstr</c> operands:
	///
	///  - RegisterNatives / FastRegisterNativeMembers blocks: one or more lines of
	///    "name:descriptor:connector[:callbackDeclaringType]", separated by '\n' (no trailing
	///    newline required).
	///  - JniPeerMembers encoded member ids: "name.descriptor" (e.g. "equals.(Ljava/lang/Object;)Z"
	///    or "eventTypes.I"), or a bare descriptor for constructors (e.g. "()V").
	///  - Bare JNI class names (e.g. "java/lang/Object").
	///
	/// Anything else (ordinary .NET strings that merely happen to contain '.' or ':') is left
	/// untouched.
	/// </summary>
	static class LdstrRewriter
	{
		public static bool TryRewrite (string value, string? ownerJniName, IJniNameMapping mapping, out string rewritten)
		{
			Func<string, string?> renameClass = className => mapping.TryMapClass (className, out string renamed) ? renamed : null;

			if (value.IndexOf ('\n') >= 0) {
				return TryRewriteMultilineRegisterNatives (value, ownerJniName, mapping, renameClass, out rewritten);
			}

			if (value.IndexOf (':') >= 0 && TryRewriteRegisterNativesLine (value, ownerJniName, mapping, renameClass, out rewritten)) {
				return true;
			}

			if (TryRewriteJniPeerMemberId (value, ownerJniName, mapping, renameClass, out rewritten)) {
				return true;
			}

			if (JniDescriptorText.IsValidMethodDescriptor (value) || JniDescriptorText.IsValidFieldDescriptor (value)) {
				return JniDescriptorText.TryRewriteDescriptor (value, renameClass, out rewritten);
			}

			if (mapping.TryMapClass (value, out string renamedWhole)) {
				rewritten = renamedWhole;
				return true;
			}

			rewritten = value;
			return false;
		}

		static bool TryRewriteMultilineRegisterNatives (string value, string? ownerJniName, IJniNameMapping mapping, Func<string, string?> renameClass, out string rewritten)
		{
			string [] lines = value.Split ('\n');
			bool changed = false;

			for (int i = 0; i < lines.Length; i++) {
				if (lines [i].Length == 0) {
					continue;
				}
				if (TryRewriteRegisterNativesLine (lines [i], ownerJniName, mapping, renameClass, out string newLine)) {
					lines [i] = newLine;
					changed = true;
				}
			}

			rewritten = changed ? string.Join ("\n", lines) : value;
			return changed;
		}

		/// <summary>
		/// Rewrites a single "name:descriptor:connector[:callbackDeclaringType]" line, as used by
		/// AndroidRuntime.RegisterNativeMembers / FastRegisterNativeMembers.
		/// </summary>
		static bool TryRewriteRegisterNativesLine (string line, string? ownerJniName, IJniNameMapping mapping, Func<string, string?> renameClass, out string rewritten)
		{
			rewritten = line;

			int firstColon = line.IndexOf (':');
			if (firstColon < 0) {
				return false;
			}
			int secondColon = line.IndexOf (':', firstColon + 1);
			if (secondColon < 0) {
				return false;
			}

			string name = line.Substring (0, firstColon);
			string descriptor = line.Substring (firstColon + 1, secondColon - firstColon - 1);
			string rest = line.Substring (secondColon); // Includes the leading ':'.

			if (!JniDescriptorText.IsValidMethodDescriptor (descriptor)) {
				return false;
			}

			bool changed = false;
			string newName = name;
			if (ownerJniName != null) {
				JniDescriptorText.MethodDescriptorToJavaTypes (descriptor, out var javaParams, out string javaReturnType);
				string mappingName = R8Mapping.JniMemberNameToMappingName (name);
				if (mapping.TryMapMethod (ownerJniName, mappingName, javaParams, javaReturnType, out string renamedMethod)) {
					newName = renamedMethod;
					changed = true;
				}
			}

			bool descriptorChanged = JniDescriptorText.TryRewriteDescriptor (descriptor, renameClass, out string newDescriptor);
			changed |= descriptorChanged;

			if (!changed) {
				return false;
			}

			rewritten = newName + ":" + newDescriptor + rest;
			return true;
		}

		/// <summary>
		/// Rewrites a JniPeerMembers encoded member id: "name.descriptor" for a method
		/// (descriptor starts with '(') or a field (descriptor is a single type token).
		/// </summary>
		static bool TryRewriteJniPeerMemberId (string value, string? ownerJniName, IJniNameMapping mapping, Func<string, string?> renameClass, out string rewritten)
		{
			rewritten = value;

			int dot = value.IndexOf ('.');
			if (dot <= 0 || dot == value.Length - 1) {
				return false;
			}

			string name = value.Substring (0, dot);
			string descriptor = value.Substring (dot + 1);

			bool isMethod = descriptor.Length > 0 && descriptor [0] == '(';
			if (isMethod ? !JniDescriptorText.IsValidMethodDescriptor (descriptor) : !JniDescriptorText.IsValidFieldDescriptor (descriptor)) {
				return false;
			}

			bool changed = false;
			string newName = name;

			if (ownerJniName != null) {
				if (isMethod) {
					JniDescriptorText.MethodDescriptorToJavaTypes (descriptor, out var javaParams, out string javaReturnType);
					string mappingName = R8Mapping.JniMemberNameToMappingName (name);
					if (mapping.TryMapMethod (ownerJniName, mappingName, javaParams, javaReturnType, out string renamedMethod)) {
						newName = renamedMethod;
						changed = true;
					}
				} else {
					if (mapping.TryMapField (ownerJniName, name, out string renamedField)) {
						newName = renamedField;
						changed = true;
					}
				}
			}

			bool descriptorChanged = JniDescriptorText.TryRewriteDescriptor (descriptor, renameClass, out string newDescriptor);
			changed |= descriptorChanged;

			if (!changed) {
				return false;
			}

			rewritten = newName + "." + newDescriptor;
			return true;
		}
	}
}
