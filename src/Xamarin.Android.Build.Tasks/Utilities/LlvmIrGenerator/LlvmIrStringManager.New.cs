using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Tasks.LLVM.IR;

partial class LlvmIrModule
{
	protected class LlvmIrStringManager
	{
		Dictionary<string, LlvmIrStringVariable> stringSymbolCache = new Dictionary<string, LlvmIrStringVariable> (StringComparer.Ordinal);
		Dictionary<string, LlvmIrStringGroup> stringGroupCache = new Dictionary<string, LlvmIrStringGroup> (StringComparer.Ordinal);
		List<LlvmIrStringGroup> stringGroups = new List<LlvmIrStringGroup> ();

		LlvmIrStringGroup defaultGroup;

		public List<LlvmIrStringGroup> StringGroups => stringGroups;

		public LlvmIrStringManager ()
		{
			defaultGroup = new LlvmIrStringGroup ();
			stringGroupCache.Add (String.Empty, defaultGroup);
			stringGroups.Add (defaultGroup);
		}

		public LlvmIrStringVariable Add (string value, string? groupName = null, string? groupComment = null, string? symbolSuffix = null)
		{
			if (value == null) {
				throw new ArgumentNullException (nameof (value));
			}

			LlvmIrStringVariable? stringVar;
			if (stringSymbolCache.TryGetValue (value, out stringVar) && stringVar != null) {
				return stringVar;
			}

			LlvmIrStringGroup? group;
			string groupPrefix;
			if (String.IsNullOrEmpty (groupName) || String.Compare ("str", groupName, StringComparison.Ordinal) == 0) {
				group = defaultGroup;
				groupPrefix = ".str";
			} else if (!stringGroupCache.TryGetValue (groupName, out group) || group == null) {
				group = new LlvmIrStringGroup (groupComment ?? groupName);
				stringGroups.Add (group);
				stringGroupCache[groupName] = group;
				groupPrefix = $".{groupName}";
			} else {
				groupPrefix = $".{groupName}";
			}

			string symbolName = $"{groupPrefix}.{group.Count++}";
			if (!String.IsNullOrEmpty (symbolSuffix)) {
				symbolName = $"{symbolName}_{symbolSuffix}";
			}

			stringVar = new LlvmIrStringVariable (symbolName, value);
			group.Strings.Add (stringVar);
			stringSymbolCache.Add (value, stringVar);

			return stringVar;
		}

		public LlvmIrStringVariable? Lookup (string value)
		{
			if (stringSymbolCache.TryGetValue (value, out LlvmIrStringVariable? sv)) {
				return sv;
			}

			return null;
		}
	}
}
