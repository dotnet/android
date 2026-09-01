#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection.Metadata;

namespace Xamarin.Android.Tasks.JniRemapping
{
	/// <summary>
	/// Walks a method body's IL, reporting each instruction's opcode together with the location
	/// and length of its operand. Every legal opcode has a fixed operand size except
	/// <c>switch</c>, whose length depends on its embedded case count.
	/// </summary>
	static class IlInstructionScanner
	{
		public delegate void InstructionVisitor (ushort opCode, int instructionOffset, int operandOffset, int operandSize);

		public static void Walk (byte [] il, InstructionVisitor visit)
		{
			int i = 0;
			while (i < il.Length) {
				int instructionOffset = i;
				byte b0 = il [i];
				ushort code;
				if (b0 == 0xFE) {
					if (i + 1 >= il.Length) {
						throw new JniRewriteException ("Malformed IL: truncated two-byte opcode.");
					}
					code = (ushort) (0xFE00 | il [i + 1]);
					i += 2;
				} else {
					code = b0;
					i += 1;
				}

				int operandOffset = i;
				int operandSize;
				if (code == (ushort) ILOpCode.Switch) {
					if (i + 4 > il.Length) {
						throw new JniRewriteException ("Malformed IL: truncated switch operand.");
					}
					uint caseCount = ReadUInt32 (il, i);
					operandSize = checked (4 + (int) caseCount * 4);
				} else if (!IlOpcodeTable.OperandSizes.TryGetValue (code, out operandSize)) {
					throw new JniRewriteException ($"Unrecognized IL opcode 0x{code:X} while scanning a method body.");
				}

				if (operandOffset + operandSize > il.Length) {
					throw new JniRewriteException ($"Malformed IL: operand of opcode 0x{code:X} at offset {instructionOffset} extends past the end of the method body.");
				}

				visit (code, instructionOffset, operandOffset, operandSize);
				i = operandOffset + operandSize;
			}
		}

		public static uint ReadUInt32 (byte [] data, int offset)
			=> (uint) (data [offset] | (data [offset + 1] << 8) | (data [offset + 2] << 16) | (data [offset + 3] << 24));

		public static void WriteUInt32 (byte [] data, int offset, uint value)
		{
			data [offset] = (byte) value;
			data [offset + 1] = (byte) (value >> 8);
			data [offset + 2] = (byte) (value >> 16);
			data [offset + 3] = (byte) (value >> 24);
		}
	}

	/// <summary>
	/// The exact set of edits the rebuilder must apply while cloning an assembly. Every entry is
	/// keyed by the *use site* rather than by the shared heap entry it happens to resolve to, so
	/// two <c>ldstr</c> instructions (or two custom attributes) that the compiler deduplicated
	/// into one <c>#US</c>/<c>#Blob</c> entry can still be given different replacements: the
	/// rebuilder emits a fresh handle per distinct value.
	/// </summary>
	sealed class JniRewritePlan
	{
		readonly Dictionary<CustomAttributeHandle, byte []> customAttributeBlobs = new ();
		readonly Dictionary<MethodDefinitionHandle, Dictionary<int, string>> userStrings = new ();
		readonly Dictionary<FieldDefinitionHandle, string> utf8FieldValues = new ();

		public int ReplacementCount { get; private set; }

		public void AddCustomAttributeBlob (CustomAttributeHandle handle, byte [] newValue)
		{
			customAttributeBlobs [handle] = newValue;
			ReplacementCount++;
		}

		/// <summary>
		/// Records that the <c>ldstr</c> whose 4-byte token operand starts at
		/// <paramref name="operandOffset"/> in <paramref name="method"/>'s body should load
		/// <paramref name="newValue"/> instead.
		/// </summary>
		public void AddUserString (MethodDefinitionHandle method, int operandOffset, string newValue)
		{
			if (!userStrings.TryGetValue (method, out var perMethod)) {
				userStrings [method] = perMethod = new Dictionary<int, string> ();
			}
			perMethod [operandOffset] = newValue;
			ReplacementCount++;
		}

		public void AddUtf8FieldValue (FieldDefinitionHandle field, string newValue)
		{
			utf8FieldValues [field] = newValue;
			ReplacementCount++;
		}

		public byte []? GetCustomAttributeBlob (CustomAttributeHandle handle)
			=> customAttributeBlobs.TryGetValue (handle, out byte []? newValue) ? newValue : null;

		public Dictionary<int, string>? GetUserStrings (MethodDefinitionHandle method)
			=> userStrings.TryGetValue (method, out Dictionary<int, string>? replacements) ? replacements : null;

		public string? GetUtf8FieldValue (FieldDefinitionHandle field)
			=> utf8FieldValues.TryGetValue (field, out string? newValue) ? newValue : null;
	}
}
