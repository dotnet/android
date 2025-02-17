// File must be "stand-alone"; it's included by
// `tools/remap-mam-json-to-xml`

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using ReplacementTypesDict      = System.Collections.Generic.Dictionary<string, string>;
using ReplacementMethodsDict    = System.Collections.Generic.Dictionary<
	(string SourceType, string SourceName, string? SourceSignature),
	(string? TargetType, string? TargetName, string? TargetSignature, int? ParamCount, bool IsStatic)
>;

namespace Xamarin.Android.Tasks
{
	class MamJsonParser
	{
		Action<TraceLevel, string>  Logger;

		public	readonly    ReplacementTypesDict    ReplacementTypes        = new ();
		public  readonly    ReplacementMethodsDict  ReplacementMethods      = new ();

		public MamJsonParser (Action<TraceLevel, string> logger)
		{
			Logger  = logger;
		}

		public void Load (string jsonPath)
		{
			var json    = ReadJson (jsonPath);

			var classRewrites = json["ClassRewrites"];
			if (classRewrites != null) {
				ReadClassRewrites (classRewrites);
			}

			var globalMethodCalls = json["GlobalMethodCalls"];
			if (globalMethodCalls != null) {
				ReadGlobalMethodCalls (globalMethodCalls);
			}
		}

		public XElement ToXml ()
		{
			return new XElement ("replacements",
				GetReplacementTypes (),
				GetReplacementMethods ());
		}

		static JsonObject ReadJson (string path)
		{
			using (var f = File.OpenRead (path)) {
				return JsonNode.Parse (f)!.AsObject ();
			}
		}

		void ReadClassRewrites (JsonNode classRewrites)
		{
			foreach (var classRewrite in classRewrites.AsArray ()) {
				if (classRewrite == null) {
					continue;
				}
				if (!TryReadClassFromTo (classRewrite!, out var from, out var to)) {
					Logger (TraceLevel.Verbose, $"No from or to! {classRewrite}");
					continue;
				}
				ReplacementTypes [from] = to;
				var methods = classRewrite ["Methods"];
				if (methods == null) {
					continue;
				}
				foreach (var method in methods.AsArray ()) {
					if (method == null) {
						continue;
					}
					var makeStatic = (bool?) method ["MakeStatic"] ?? false;
					var oldName = (string?) method ["OriginalName"];
					var newName = (string?) method ["NewName"];
					var oldSig  = ReadSignature (method ["OriginalParams"]);
					if (oldName == null || newName == null) {
						continue;
					}
					ReplacementMethods [(to, oldName, oldSig)] = (to, newName, null, null, makeStatic);
				}
			}
		}

		bool TryReadClassFromTo (JsonNode token, [NotNullWhen(true)] out string? from, [NotNullWhen(true)] out string? to)
		{
			from    = (string?) token ["Class"]? ["From"];
			to      = (string?) token ["Class"]? ["To"];
			if (from == null || to == null) {
				return false;
			}
			from = JavaToJniType (from);
			to = JavaToJniType (to);
			return true;
		}

		string? ReadSignature (JsonNode? token)
		{
			if (token == null)
				return null;
			var types = new List<string> ();
			foreach (var type in token.AsArray()) {
				if (type == null) {
					continue;
				}
				var javaType = ((string?) type) switch {
					"boolean"   => "Z",
					"byte"      => "B",
					"char"      => "C",
					"double"    => "D",
					"float"     => "F",
					"int"       => "I",
					"long"      => "J",
					"short"     => "S",
					"void"      => "V",
					var o       => JavaToJniTypeSignature (o),
				};
				if (javaType == null) {
					continue;
				}
				types.Add (javaType);
			}
			if (types.Count == 0)
				return null;
			var sig = new StringBuilder ();
			sig.Append ("(");
			foreach (var type in types) {
				sig.Append (type);
			}
			sig.Append (")");
			return sig.ToString ();
		}

		string JavaToJniType (string javaType)
		{
			return javaType.Replace (".", "/");
		}

		StringBuilder JavaToJniType (StringBuilder javaType)
		{
			return javaType.Replace ('.', '/');
		}

		string? JavaToJniTypeSignature (string? javaType)
		{
			if (javaType == null) {
				return null;
			}
			var jniType     = new StringBuilder (javaType);
			int arrayCount  = 0;
			while (jniType.Length > 2 && jniType [jniType.Length-2] == '[' && jniType [jniType.Length-1] == ']') {
				arrayCount++;
				jniType.Length -= 2;
			}
			JavaToJniType (jniType);
			jniType.Append (";");
			jniType.Insert (0, 'L');
			for (int i = 0; i < arrayCount; ++i) {
				jniType.Insert (0, '[');
			}
			return jniType.ToString ();
		}

		void ReadGlobalMethodCalls (JsonNode globalMethodCalls)
		{
			foreach (var globalMethodCall in globalMethodCalls.AsArray ()) {
				if (globalMethodCall == null) {
					continue;
				}
				if (!TryReadClassFromTo (globalMethodCall!, out var from, out var to)) {
					Logger (TraceLevel.Info, $"No from or to! {globalMethodCall}");
					continue;
				}
				var methods = globalMethodCall ["Methods"];
				if (methods == null) {
					continue;
				}
				foreach (var method in methods.AsArray ()) {
					if (method == null) {
						continue;
					}
					var makeStatic = (bool?) method ["MakeStatic"] ?? false;
					var oldName = (string?) method ["OriginalName"];
					var oldSig  = ReadSignature (method ["OriginalParams"]);
					if (oldSig != null) {
						throw new Exception ("huh?");
					}
					if (oldName == null || oldName.Length < 1) {
						continue;
					}
					var newName = oldName;
					ReplacementMethods [(from, oldName, null)] = (to, newName, null, null, makeStatic);
				}
			}
		}

		IEnumerable<XElement> GetReplacementTypes ()
		{
			foreach (var k in ReplacementTypes.Keys.OrderBy (k => k)) {
				yield return new XElement ("replace-type",
						new XAttribute ("from", k),
						new XAttribute ("to", ReplacementTypes [k]));
			}
		}

		IEnumerable<XElement> GetReplacementMethods ()
		{
			var entries = ReplacementMethods.Keys.OrderBy (e => e.SourceType)
				.ThenBy (e => e.SourceName)
				.ThenBy (e => e.SourceSignature);
			foreach (var k in entries) {
				var v = ReplacementMethods [k];
				yield return new XElement ("replace-method",
						new XAttribute ("source-type", k.SourceType),
						new XAttribute ("source-method-name", k.SourceName),
						CreateAttribute ("source-method-signature", k.SourceSignature),
						CreateAttribute ("target-type", v.TargetType),
						CreateAttribute ("target-method-name", v.TargetName),
						CreateAttribute ("target-method-signature", v.TargetSignature),
						CreateAttribute ("target-method-parameter-count", v.ParamCount.HasValue ? v.ParamCount.Value.ToString (CultureInfo.InvariantCulture) : null),
						CreateAttribute ("target-method-instance-to-static", v.IsStatic ? "true" : "false"));
			}
		}

		XAttribute? CreateAttribute (string name, string? value)
		{
			if (value == null) {
				return null;
			}
			return new XAttribute (name, value);
		}
	}
}
