using System;
using System.Collections.Generic;

using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks.LLVMIR;

partial class LlvmIrModule
{
	protected class LlvmIrStringManager
	{
		Dictionary<StringHolder, LlvmIrStringVariable> stringSymbolCache = new Dictionary<StringHolder, LlvmIrStringVariable> ();
		Dictionary<string, LlvmIrStringGroup> stringGroupCache = new Dictionary<string, LlvmIrStringGroup> (StringComparer.Ordinal);
		List<LlvmIrStringGroup> stringGroups = new List<LlvmIrStringGroup> ();

		LlvmIrStringGroup defaultGroup;
		TaskLoggingHelper log;

		public List<LlvmIrStringGroup> StringGroups => stringGroups;

		public LlvmIrStringManager (TaskLoggingHelper log)
		{
			this.log = log;
			defaultGroup = new LlvmIrStringGroup ();
			stringGroupCache.Add (String.Empty, defaultGroup);
			stringGroups.Add (defaultGroup);
		}

		public LlvmIrStringVariable Add (LlvmIrStringVariable variable, string? groupName = null, string? groupComment = null, string? symbolSuffix = null)
		{
			// Let it throw if Value isn't a StringHolder, it must be.
			return Add((StringHolder)variable.Value, groupName, groupComment, symbolSuffix);
		}

		public LlvmIrStringVariable Add (string value, string? groupName = null, string? groupComment = null, string? symbolSuffix = null,
			LlvmIrStringEncoding encoding = LlvmIrStringEncoding.UTF8, StringComparison comparison = StringComparison.Ordinal)
		{
			if (value == null) {
				throw new ArgumentNullException (nameof (value));
			}

			return Add (new StringHolder (value, encoding, comparison), groupName, groupComment, symbolSuffix);
		}

		LlvmIrStringVariable Add (StringHolder holder, string? groupName = null, string? groupComment = null, string? symbolSuffix = null)
		{
			if (stringSymbolCache.TryGetValue (holder, out LlvmIrStringVariable? stringVar) && stringVar != null) {
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

			stringVar = new LlvmIrStringVariable (symbolName, holder);
			group.Strings.Add (stringVar);
			stringSymbolCache.Add (holder, stringVar);

			return stringVar;
		}

		public LlvmIrStringVariable? Lookup (StringHolder value)
		{
			if (stringSymbolCache.TryGetValue (value, out LlvmIrStringVariable? sv)) {
				return sv;
			}

			return null;
		}
	}
}
