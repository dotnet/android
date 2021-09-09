using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml;

using Xamarin.Android;
using Java.Interop.Tools.Generator;

namespace MonoDroid.Generation
{
	partial class EnumMappings
	{
		private string output_dir;
		private string output_metadata;
		private List<KeyValuePair<string, string>> remove_nodes;
		private int version;
		private bool fix_constants_instead_of_removing;

		public EnumMappings (string outputDir, string outputMetadata, string version, bool fixConstantsInsteadOfRemove)
		{
			output_dir = outputDir;
			output_metadata = outputMetadata;
			this.version = version == null ? 0 : int.Parse (version);
			fix_constants_instead_of_removing = fixConstantsInsteadOfRemove;
		}

		internal Dictionary<string, EnumDescription> Process (string fieldMap, string flagsFile, string methodMap)
		{
			remove_nodes = new List<KeyValuePair<string, string>> ();
			var enums = (fieldMap ?? "").EndsWith (".csv", StringComparison.OrdinalIgnoreCase)
				? ParseFieldMappings (fieldMap, flagsFile, version, remove_nodes)
				: ParseXmlFieldMappings (fieldMap, version, remove_nodes);

			var methods = (methodMap ?? "").EndsWith (".csv", StringComparison.OrdinalIgnoreCase)
				? ParseMethodMappings (methodMap, version)
				: ParseXmlMethodMappings (methodMap, version);

			// Create output metadata file
			Directory.CreateDirectory (output_dir);
			using (StreamWriter sw = new StreamWriter (output_metadata, false)) {
				sw.WriteLine ("<metadata>");
				WriteEnumerationRegistrations (sw, enums);

				foreach (var m in methods)
					m.WriteTransform (sw);

				if (fix_constants_instead_of_removing)
					FixOldConstants (sw);
				else
					RemoveOldConstants (sw);

				sw.WriteLine ("</metadata>");
			}
			return enums;
		}

		//  <remove-node path="/api/package[@name='java.lang']/class[@name='Float']/field[@name='MAX_VALUE']" />
		void RemoveOldConstants (StreamWriter sw)
		{
			foreach (var e in remove_nodes) {
				string enu = e.Key;
				string package, type, member;
				ParseJniMember (enu, out package, out type, out member);
				try {
					sw.WriteLine ("  <remove-node path=\"/api/package[@name='{0}']/{3}[@name='{1}']/field[@name='{2}']\" />",
							package, type, member, enu.StartsWith ("I:", StringComparison.Ordinal) ? "interface" : "class");
				} catch (Exception ex) {
					Report.LogCodedErrorAndExit (Report.ErrorFailedToRemoveConstants, ex, enu);
					throw;
				}
			}
		}

		void FixOldConstants (StreamWriter sw)
		{
			foreach (var pair in remove_nodes) {
				var enu = pair.Key;

				string package, type, member;
				ParseJniMember (enu, out package, out type, out member);

				if (pair.Value != null) {
					sw.WriteLine ("  <attr path=\"/api/package[@name='{0}']/{3}[@name='{1}']/field[@name='{2}']\" name=\"type\">{4}</attr>",
						      package, type, member, enu.StartsWith ("I:", StringComparison.Ordinal) ? "interface" : "class", pair.Value);
					sw.WriteLine ("  <attr path=\"/api/package[@name='{0}']/{3}[@name='{1}']/field[@name='{2}']\" name=\"deprecated\">This constant will be removed in the future version. Use {4} enum directly instead of this field.</attr>",
						      package, type, member, enu.StartsWith ("I:", StringComparison.Ordinal) ? "interface" : "class", pair.Value);
					sw.WriteLine ("  <attr path=\"/api/package[@name='{0}']/{3}[@name='{1}']/field[@name='{2}']\" name=\"deprecated-error\">true</attr>",
						      package, type, member, enu.StartsWith ("I:", StringComparison.Ordinal) ? "interface" : "class", pair.Value);
					continue;
				}
				try {
					sw.WriteLine ("  <remove-node path=\"/api/package[@name='{0}']/{3}[@name='{1}']/field[@name='{2}']\" />",
						      package, type, member, enu.StartsWith ("I:", StringComparison.Ordinal) ? "interface" : "class");
				} catch (Exception ex) {
					Report.LogCodedErrorAndExit (Report.ErrorFailedToRemoveConstants, ex, enu);
					throw;
				}
			}
		}
	}
}
