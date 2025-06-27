using System;
using System.Collections.Generic;

namespace Xamarin.Android.Tasks.LLVMIR;

/// <summary>
/// Represents a uniform array which is composed of several sections. Each section must contain
/// entries of the same type (or derived from the same base type), has its own header but is otherwise
/// treated as a separate entity to other sections. The resulting output array will look as a flattened
/// array of arrays (sections).
/// </summary>
abstract class LlvmIrSectionedArrayBase
{
	readonly Type containedType;
	readonly List<LlvmIrArraySectionBase> sections = new ();

	public List<LlvmIrArraySectionBase> Sections => sections;
	public Type ContainedType => containedType;
	public ulong Count => GetItemCount ();

	protected LlvmIrSectionedArrayBase (Type containedType)
	{
		this.containedType = containedType;
	}

	protected void Add (LlvmIrArraySectionBase section)
	{
		if (!containedType.IsAssignableFrom (section.DataType)) {
			throw new ArgumentException ("must be of type {containedType} or derived from it", nameof (section));
		}

		sections.Add (section);
	}

	ulong GetItemCount ()
	{
		ulong ret = 0;
		foreach (LlvmIrArraySectionBase section in sections) {
			ret += (ulong)section.Data.Count;
		}
		return ret;
	}
}

class LlvmIrSectionedArray<T> : LlvmIrSectionedArrayBase
{
	public LlvmIrSectionedArray ()
		: base (typeof (T))
	{}

	public void Add (LlvmIrArraySection<T> section) => base.Add (section);
}
