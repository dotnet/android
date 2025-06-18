using System;
using System.Collections.Generic;

namespace Xamarin.Android.Tasks.LLVMIR;

/// <summary>
/// Represents a uniform array which is composed of several sections. Each section must contain
/// entries of the same type (or derived from the same base type), has its own header but is otherwise
/// treated as a separate entity to other sections. The resulting output array will look as a flattened
/// array of arrays (sections).
/// </summary>
class LlvmIrSectionedArray
{
	readonly Type containedType;
	readonly List<LlvmIrArraySection> sections = new ();

	public List<LlvmIrArraySection> Sections => sections;

	public LlvmIrSectionedArray (Type containedType)
	{
		this.containedType = containedType;
	}

	public void Add (LlvmIrArraySection section)
	{
		if (!containedType.IsAssignableFrom (section.DataType)) {
			throw new ArgumentException ("must be of type {containedType} or derived from it", nameof (section));
		}

		sections.Add (section);
	}
}
