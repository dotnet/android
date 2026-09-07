#nullable enable

using System.Collections.Generic;
using System.Reflection.Metadata;

namespace Xamarin.Android.Tasks.JniRemapping
{
	/// <summary>
	/// A minimal ECMA-335 (III.3-4) IL instruction operand-size table, used to walk a method
	/// body's IL bytes (looking for <c>ldstr</c> instructions) without any IL-parsing library.
	/// Every legal opcode has a fixed operand size, except <c>switch</c> whose operand length
	/// depends on the embedded case count, which is handled specially by the caller.
	/// </summary>
	static class IlOpcodeTable
	{
		public static readonly Dictionary<ushort, int> OperandSizes = BuildTable ();

		static Dictionary<ushort, int> BuildTable ()
		{
			var table = new Dictionary<ushort, int> ();

			// No operand.
			Add (table, 0, ILOpCode.Nop, ILOpCode.Break,
				ILOpCode.Ldarg_0, ILOpCode.Ldarg_1, ILOpCode.Ldarg_2, ILOpCode.Ldarg_3,
				ILOpCode.Ldloc_0, ILOpCode.Ldloc_1, ILOpCode.Ldloc_2, ILOpCode.Ldloc_3,
				ILOpCode.Stloc_0, ILOpCode.Stloc_1, ILOpCode.Stloc_2, ILOpCode.Stloc_3,
				ILOpCode.Ldnull,
				ILOpCode.Ldc_i4_m1, ILOpCode.Ldc_i4_0, ILOpCode.Ldc_i4_1, ILOpCode.Ldc_i4_2, ILOpCode.Ldc_i4_3,
				ILOpCode.Ldc_i4_4, ILOpCode.Ldc_i4_5, ILOpCode.Ldc_i4_6, ILOpCode.Ldc_i4_7, ILOpCode.Ldc_i4_8,
				ILOpCode.Dup, ILOpCode.Pop, ILOpCode.Ret,
				ILOpCode.Ldind_i1, ILOpCode.Ldind_u1, ILOpCode.Ldind_i2, ILOpCode.Ldind_u2,
				ILOpCode.Ldind_i4, ILOpCode.Ldind_u4, ILOpCode.Ldind_i8, ILOpCode.Ldind_i,
				ILOpCode.Ldind_r4, ILOpCode.Ldind_r8, ILOpCode.Ldind_ref,
				ILOpCode.Stind_ref, ILOpCode.Stind_i1, ILOpCode.Stind_i2, ILOpCode.Stind_i4,
				ILOpCode.Stind_i8, ILOpCode.Stind_r4, ILOpCode.Stind_r8, ILOpCode.Stind_i,
				ILOpCode.Add, ILOpCode.Sub, ILOpCode.Mul, ILOpCode.Div, ILOpCode.Div_un,
				ILOpCode.Rem, ILOpCode.Rem_un, ILOpCode.And, ILOpCode.Or, ILOpCode.Xor,
				ILOpCode.Shl, ILOpCode.Shr, ILOpCode.Shr_un, ILOpCode.Neg, ILOpCode.Not,
				ILOpCode.Conv_i1, ILOpCode.Conv_i2, ILOpCode.Conv_i4, ILOpCode.Conv_i8,
				ILOpCode.Conv_r4, ILOpCode.Conv_r8, ILOpCode.Conv_u4, ILOpCode.Conv_u8, ILOpCode.Conv_r_un,
				ILOpCode.Throw,
				ILOpCode.Ldlen,
				ILOpCode.Ldelem_i1, ILOpCode.Ldelem_u1, ILOpCode.Ldelem_i2, ILOpCode.Ldelem_u2,
				ILOpCode.Ldelem_i4, ILOpCode.Ldelem_u4, ILOpCode.Ldelem_i8, ILOpCode.Ldelem_i,
				ILOpCode.Ldelem_r4, ILOpCode.Ldelem_r8, ILOpCode.Ldelem_ref,
				ILOpCode.Stelem_i, ILOpCode.Stelem_i1, ILOpCode.Stelem_i2, ILOpCode.Stelem_i4,
				ILOpCode.Stelem_i8, ILOpCode.Stelem_r4, ILOpCode.Stelem_r8, ILOpCode.Stelem_ref,
				ILOpCode.Conv_ovf_i1_un, ILOpCode.Conv_ovf_u1_un, ILOpCode.Conv_ovf_i2_un, ILOpCode.Conv_ovf_u2_un,
				ILOpCode.Conv_ovf_i4_un, ILOpCode.Conv_ovf_u4_un, ILOpCode.Conv_ovf_i8_un, ILOpCode.Conv_ovf_u8_un,
				ILOpCode.Conv_ovf_i_un, ILOpCode.Conv_ovf_u_un,
				ILOpCode.Conv_ovf_i1, ILOpCode.Conv_ovf_u1, ILOpCode.Conv_ovf_i2, ILOpCode.Conv_ovf_u2,
				ILOpCode.Conv_ovf_i4, ILOpCode.Conv_ovf_u4, ILOpCode.Conv_ovf_i8, ILOpCode.Conv_ovf_u8,
				ILOpCode.Ckfinite,
				ILOpCode.Conv_u2, ILOpCode.Conv_u1, ILOpCode.Conv_i, ILOpCode.Conv_ovf_i, ILOpCode.Conv_ovf_u, ILOpCode.Conv_u,
				ILOpCode.Add_ovf, ILOpCode.Add_ovf_un, ILOpCode.Mul_ovf, ILOpCode.Mul_ovf_un, ILOpCode.Sub_ovf, ILOpCode.Sub_ovf_un,
				ILOpCode.Endfinally,
				ILOpCode.Arglist, ILOpCode.Ceq, ILOpCode.Cgt, ILOpCode.Cgt_un, ILOpCode.Clt, ILOpCode.Clt_un,
				ILOpCode.Localloc, ILOpCode.Endfilter,
				ILOpCode.Volatile, ILOpCode.Tail, ILOpCode.Cpblk, ILOpCode.Initblk,
				ILOpCode.Rethrow, ILOpCode.Refanytype, ILOpCode.Readonly);

			// 1-byte operand.
			Add (table, 1, ILOpCode.Starg_s, ILOpCode.Ldloc_s, ILOpCode.Ldloca_s, ILOpCode.Stloc_s,
				ILOpCode.Ldarg_s, ILOpCode.Ldarga_s, ILOpCode.Ldc_i4_s, ILOpCode.Unaligned,
				ILOpCode.Br_s, ILOpCode.Brfalse_s, ILOpCode.Brtrue_s,
				ILOpCode.Beq_s, ILOpCode.Bge_s, ILOpCode.Bgt_s, ILOpCode.Ble_s, ILOpCode.Blt_s,
				ILOpCode.Bne_un_s, ILOpCode.Bge_un_s, ILOpCode.Bgt_un_s, ILOpCode.Ble_un_s, ILOpCode.Blt_un_s,
				ILOpCode.Leave_s);

			// 2-byte operand.
			Add (table, 2, ILOpCode.Starg, ILOpCode.Ldloc, ILOpCode.Ldloca, ILOpCode.Stloc, ILOpCode.Ldarg, ILOpCode.Ldarga);

			// 4-byte operand.
			Add (table, 4, ILOpCode.Ldc_i4, ILOpCode.Ldc_r4,
				ILOpCode.Br, ILOpCode.Brfalse, ILOpCode.Brtrue,
				ILOpCode.Beq, ILOpCode.Bge, ILOpCode.Bgt, ILOpCode.Ble, ILOpCode.Blt,
				ILOpCode.Bne_un, ILOpCode.Bge_un, ILOpCode.Bgt_un, ILOpCode.Ble_un, ILOpCode.Blt_un,
				ILOpCode.Leave,
				ILOpCode.Jmp, ILOpCode.Call, ILOpCode.Callvirt, ILOpCode.Newobj, ILOpCode.Ldftn, ILOpCode.Ldvirtftn,
				ILOpCode.Calli,
				ILOpCode.Ldfld, ILOpCode.Ldflda, ILOpCode.Stfld, ILOpCode.Ldsfld, ILOpCode.Ldsflda, ILOpCode.Stsfld,
				ILOpCode.Cpobj, ILOpCode.Ldobj, ILOpCode.Stobj, ILOpCode.Castclass, ILOpCode.Isinst,
				ILOpCode.Box, ILOpCode.Newarr, ILOpCode.Ldelema, ILOpCode.Ldelem, ILOpCode.Stelem,
				ILOpCode.Unbox, ILOpCode.Unbox_any, ILOpCode.Refanyval, ILOpCode.Mkrefany,
				ILOpCode.Initobj, ILOpCode.Constrained, ILOpCode.Sizeof,
				ILOpCode.Ldstr, ILOpCode.Ldtoken);

			// 8-byte operand.
			Add (table, 8, ILOpCode.Ldc_i8, ILOpCode.Ldc_r8);

			return table;
		}

		static void Add (Dictionary<ushort, int> table, int size, params ILOpCode [] codes)
		{
			foreach (var code in codes) {
				table [(ushort) code] = size;
			}
		}
	}
}
