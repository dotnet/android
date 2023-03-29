using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Tasks.LLVMIR;

partial class LlvmIrGenerator
{
	public sealed class StringSymbolInfo
	{
		public readonly string SymbolName;
		public readonly ulong Size;
		public readonly string Value;

		public StringSymbolInfo (string symbolName, string value, ulong size)
		{
			SymbolName = symbolName;
			Value = value;
			Size = size;
		}
	}

	sealed class StringGroup
	{
		public ulong Count;
		public readonly string? Comment;
		public readonly List<StringSymbolInfo> Strings = new List<StringSymbolInfo> ();

		public StringGroup (string? comment = null)
		{
			Comment = comment;
			Count = 0;
		}
	}

	protected class LlvmIrStringManager
	{
		Dictionary<string, StringSymbolInfo> stringSymbolCache = new Dictionary<string, StringSymbolInfo> (StringComparer.Ordinal);
		Dictionary<string, StringGroup> stringGroupCache = new Dictionary<string, StringGroup> (StringComparer.Ordinal);
		List<StringGroup> stringGroups = new List<StringGroup> ();

		StringGroup defaultGroup;

		public LlvmIrStringManager ()
		{
			defaultGroup = new StringGroup ();
			stringGroupCache.Add (String.Empty, defaultGroup);
			stringGroups.Add (defaultGroup);
		}

		public StringSymbolInfo Add (string value, string? groupName = null, string? groupComment = null, string? symbolSuffix = null)
		{
			if (value == null) {
				throw new ArgumentNullException (nameof (value));
			}

			StringSymbolInfo? info;
			if (stringSymbolCache.TryGetValue (value, out info) && info != null) {
				return info;
			}

			StringGroup? group;
			string groupPrefix;
			if (String.IsNullOrEmpty (groupName) || String.Compare ("str", groupName, StringComparison.Ordinal) == 0) {
				group = defaultGroup;
				groupPrefix = "__str";
			} else if (!stringGroupCache.TryGetValue (groupName, out group) || group == null) {
				group = new StringGroup (groupComment ?? groupName);
				stringGroups.Add (group);
				stringGroupCache[groupName] = group;
				groupPrefix = $"__{groupName}";
			} else {
				groupPrefix = $"__{groupName}";
			}

			string quotedString = QuoteString (value, out ulong stringSize);
			string symbolName = $"{groupPrefix}.{group.Count++}";
			if (!String.IsNullOrEmpty (symbolSuffix)) {
				symbolName = $"{symbolName}_{symbolSuffix}";
			}

			info = new StringSymbolInfo (symbolName, quotedString, stringSize);
			group.Strings.Add (info);
			stringSymbolCache.Add (value, info);

			return info;
		}

		public void Flush (LlvmIrGenerator generator)
		{
			TextWriter output = generator.Output;

			generator.WriteEOL ("Strings");
			foreach (StringGroup group in stringGroups) {
				if (group != defaultGroup) {
					output.WriteLine ();
				}

				if (!String.IsNullOrEmpty (group.Comment)) {
					generator.WriteCommentLine (output, group.Comment);
				}

				foreach (StringSymbolInfo info in group.Strings) {
					generator.WriteGlobalSymbolStart (info.SymbolName, LlvmIrVariableOptions.LocalConstexprString);
					output.Write ('[');
					output.Write (info.Size);
					output.Write (" x i8] c");
					output.Write (info.Value);
					output.Write (", align ");
					output.WriteLine (generator.GetAggregateAlignment (1, info.Size));
				}
			}
		}
	}
}
