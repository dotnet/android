namespace Xamarin.Android.Tasks.LLVMIR
{
	/// <summary>
	/// Represents comparison conditions for the LLVM IR icmp instruction.
	/// </summary>
	enum LlvmIrIcmpCond
	{
		/// <summary>
		/// Equal comparison (eq).
		/// </summary>
		Equal,
		/// <summary>
		/// Not equal comparison (ne).
		/// </summary>
		NotEqual,
		/// <summary>
		/// Unsigned greater than comparison (ugt).
		/// </summary>
		UnsignedGreaterThan,
		/// <summary>
		/// Unsigned greater than or equal comparison (uge).
		/// </summary>
		UnsignedGreaterOrEqual,
		/// <summary>
		/// Unsigned less than comparison (ult).
		/// </summary>
		UnsignedLessThan,
		/// <summary>
		/// Unsigned less than or equal comparison (ule).
		/// </summary>
		UnsignedLessOrEqual,
		/// <summary>
		/// Signed greater than comparison (sgt).
		/// </summary>
		SignedGreaterThan,
		/// <summary>
		/// Signed greater than or equal comparison (sge).
		/// </summary>
		SignedGreaterOrEqual,
		/// <summary>
		/// Signed less than comparison (slt).
		/// </summary>
		SignedLessThan,
		/// <summary>
		/// Signed less than or equal comparison (sle).
		/// </summary>
		SignedLessOrEqual,
	}
}
