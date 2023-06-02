using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public enum RType {
		Integer,
		Array,
	}

	public enum ResourceType {
		System,
		Custom,
	}

	public struct R : IComparable<R> {
		public RType Type;
		public int Id;
		public int [] Ids;
		public string Identifier;
		public string ResourceTypeName;
		public ResourceType ResourceType;

		public string Key => $"{ResourceTypeName}:{Identifier}";

		public override string ToString ()
		{
			if (Type == RType.Integer) {
				if (ResourceTypeName == "styleable")
					return $"int {ResourceTypeName} {Identifier} {Id}";
				return $"int {ResourceTypeName} {Identifier} 0x{Id.ToString ("x8")}";
			}
			return $"int[] {ResourceTypeName} {Identifier} {{ {String.Join (", ", Ids.Select (x => $"0x{x.ToString ("x8")}"))} }}";
		}

		public string ToSortedString ()
		{
			return $"{ResourceTypeName}_{Identifier}";
		}

		public int CompareTo(R other)
		{
			return String.Compare (ToSortedString (), other.ToSortedString (), StringComparison.OrdinalIgnoreCase);
		}

		public void UpdateId (int newId)
		{
			Id = newId;
		}

		public void UpdateIds (int [] newIds)
		{
			Ids = newIds;
		}
	}

	public class RtxtParser {

		static readonly char[] EmptyChar = new char [] { ' ' };
		static readonly char[] CurlyBracketsChar = new char [] { '{', '}' };
		static readonly char[] CommaChar = new char [] { ',' };
		static readonly Regex ValidChars = new Regex (@"([^a-f0-9x, \{\}])+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		TaskLoggingHelper log;
		Dictionary<string, string> map;

		public static HashSet<string> knownTypes = new HashSet<string> () {
			"anim",
			"animator",
			"attr",
			"array",
			"bool",
			"color",
			"dimen",
			"drawable",
			"font",
			"id",
			"integer",
			"interpolator",
			"layout",
			"menu",
			"mipmap",
			"plurals",
			"raw",
			"string",
			"style",
			"styleable",
			"transition",
			"xml",
		};

		public IEnumerable<R> Parse (string file, TaskLoggingHelper logger, Dictionary<string, string> mapping)
		{
			log = logger;
			map = mapping;
			var result = new List<R> ();
			if (File.Exists (file))
				ProcessRtxtFile (file, result);
			return result;
		}

		void ProcessRtxtFile (string file, IList<R> result)
		{
			int lineNumber = 0;
			foreach (var line in File.ReadLines (file)) {
				lineNumber++;
				var items = line.Split (EmptyChar, 4);
				if (items.Length < 4) {
					log.LogDebugMessage ($"'{file}:{lineNumber}' ignoring contents '{line}', it does not have the correct number of elements.");
					continue;
				}
				if (ValidChars.IsMatch (items [3])) {
					log.LogDebugMessage ($"'{file}:{lineNumber}' ignoring contents '{line}', it contains invalid characters.");
					continue;
				}
				int value = items [1] != "styleable" ? Convert.ToInt32 (items [3].Trim (), 16) : -1;
				string itemName = ResourceIdentifier.GetResourceName(items [1], items [2], map, log);
				if (knownTypes.Contains (items [1])) {
					if (items [1] != "styleable") {
						result.Add (new R () {
							ResourceTypeName = items [1],
							Identifier = itemName,
							Id = value,
						});
						continue;
					}
					switch (items [0]) {
						case "int":
							result.Add (new R () {
								ResourceTypeName = items [1],
								Identifier = itemName,
								Id = Convert.ToInt32 (items [3].Trim (), 10),
							});
							break;
						case "int[]":
							var arrayValues = items [3].Trim (CurlyBracketsChar)
								.Replace (" ", "")
								.Split (CommaChar);

							result.Add (new R () {
								ResourceTypeName = items [1],
								Type = RType.Array,
								Identifier = itemName,
								Ids = arrayValues.Select (x => string.IsNullOrEmpty (x) ? -1 : Convert.ToInt32 (x, 16)).ToArray (),
							});
							break;
					}
					continue;
				}
				result.Add (new R () {
					ResourceTypeName = items[1],
					ResourceType = ResourceType.Custom,
					Identifier = itemName,
					Id = value,
				});
			}
		}
	}
}
