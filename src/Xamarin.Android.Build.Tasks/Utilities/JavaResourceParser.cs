using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	class JavaResourceParser
	{
		public static TaskLoggingHelper Log { get; set; }

		public static CodeTypeDeclaration Parse (string file, bool isApp, Dictionary<string, string> resourceMap)
		{
			if (!File.Exists (file))
				throw new InvalidOperationException ("Specified Java resource file was not found: " + file);

			CodeTypeDeclaration resources = null;

			using (var reader = File.OpenText (file)) {
				string line;

				while ((line = reader.ReadLine ()) != null) {
					var info = Parser.Select (p => new { Match = p.Key.Match (line), Handler = p.Value }).FirstOrDefault (x => x.Match.Success);
					
					if (info == null)
						continue;

					resources = info.Handler (info.Match, isApp, resources, resourceMap);
				}
			}

			return resources;
		}

		static KeyValuePair<Regex, Func<Match, bool, CodeTypeDeclaration, Dictionary<string, string>, CodeTypeDeclaration>> Parse (string regex, Func<Match, bool, CodeTypeDeclaration, Dictionary<string, string>, CodeTypeDeclaration> f)
		{
			return new KeyValuePair<Regex, Func<Match, bool, CodeTypeDeclaration, Dictionary<string, string>, CodeTypeDeclaration>> (new Regex (regex), f);
		}

		// public finall class R {
		//	 public static fnal class string|anim|styleable|etc. {
		//     public static final int field = 0xZZ;
		//     public static final int [] array = {
		//       0xXX, 0xYY, 0xZZ
		//     }
		//   }
		// }
		static List<KeyValuePair<Regex, Func<Match, bool, CodeTypeDeclaration, Dictionary<string, string>, CodeTypeDeclaration>>> Parser = new List<KeyValuePair<Regex, Func<Match, bool, CodeTypeDeclaration, Dictionary<string, string>, CodeTypeDeclaration>>> () {
			Parse ("^public final class R {",
					(m, app, _, map) => {
						var decl = new CodeTypeDeclaration ("Resource") {
							IsPartial = true,
						};
						var asm = Assembly.GetExecutingAssembly().GetName();
						var codeAttrDecl = 
							new CodeAttributeDeclaration("System.CodeDom.Compiler.GeneratedCodeAttribute",
			                        		new CodeAttributeArgument(
									new CodePrimitiveExpression(asm.Name)),
			                			new CodeAttributeArgument(
									new CodePrimitiveExpression(asm.Version.ToString()))
							);
						decl.CustomAttributes.Add(codeAttrDecl);
						return decl;

					}),
			Parse ("^    public static final class ([^ ]+) {$",
					(m, app, g, map) => {
						var t = new CodeTypeDeclaration (GetNestedTypeName (m.Groups [1].Value)) {
							IsPartial       = true,
							TypeAttributes  = TypeAttributes.Public,
						};
						t.Members.Add (new CodeConstructor () {
								Attributes  = MemberAttributes.Family,
						});
						g.Members.Add (t);
						return g;
					}),
			Parse (@"^        public static final int ([^ =]+)\s*=\s*([^;]+);$",
					(m, app, g, map) => {
						var name = ((CodeTypeDeclaration) g.Members [g.Members.Count-1]).Name;
						var f = new CodeMemberField (typeof (int), GetResourceName (name, m.Groups[1].Value, map)) {
								Attributes      = app ? MemberAttributes.Const | MemberAttributes.Public : MemberAttributes.Static | MemberAttributes.Public,
								InitExpression  = new CodePrimitiveExpression (ToInt32 (m.Groups [2].Value, m.Groups [2].Value.IndexOf ("0x") == 0 ? 16 : 10)),
								Comments = {
									new CodeCommentStatement ("aapt resource value: " + m.Groups [2].Value),
								},
						};
						((CodeTypeDeclaration) g.Members [g.Members.Count-1]).Members.Add (f);
						return g;
					}),
			Parse (@"^        public static final int\[\] ([^ =]+) = {",
					(m, app, g, map) => {
						var name = ((CodeTypeDeclaration) g.Members [g.Members.Count-1]).Name;
						var f = new CodeMemberField (typeof (int[]), GetResourceName (name, m.Groups[1].Value, map)) {
								// pity I can't make the member readonly...
								Attributes      = MemberAttributes.Public | MemberAttributes.Static,
						};
						((CodeTypeDeclaration) g.Members [g.Members.Count-1]).Members.Add (f);
						return g;
					}),
			Parse (@"^            (0x[xa-fA-F0-9, ]+)$",
					(m, app, g, map) => {
						var t = (CodeTypeDeclaration) g.Members [g.Members.Count-1];
						var f = (CodeMemberField) t.Members [t.Members.Count-1];
						string[] values = m.Groups [1].Value.Split (new[]{','}, StringSplitOptions.RemoveEmptyEntries);
						CodeArrayCreateExpression c = (CodeArrayCreateExpression) f.InitExpression;
						if (c == null) {
							f.InitExpression = c = new CodeArrayCreateExpression (typeof (int[]));
						}
						foreach (string value in values)
							c.Initializers.Add (new CodePrimitiveExpression (ToInt32 (value.Trim (), 16)));
						return g;
					}),
		};

		internal static int ToInt32 (string value, int @base)
		{
			try {
				return Convert.ToInt32 (value, @base);
			} catch (Exception e) {
				throw new NotSupportedException (
						string.Format ("Could not convert value '{0}' (base '{1}') into an Int32.",
							value, @base),
						e);
			}
		}

		internal static string GetNestedTypeName (string name)
		{
			switch (name) {
				case "anim": return "Animation";
				case "attr": return "Attribute";
				case "bool": return "Boolean";
				case "dimen": return "Dimension";
				default: return char.ToUpperInvariant (name[0]) + name.Substring (1);
			}
		}

		static string GetResourceName (string type, string name, Dictionary<string, string> map)
		{
			string mappedValue;
			string key = string.Format ("{0}{1}{2}", type, Path.DirectorySeparatorChar, name).ToLowerInvariant ();

			if (map.TryGetValue (key, out mappedValue)) {
				Log.LogMessage ("  - Remapping resource: {0}.{1} -> {2}", type, name, mappedValue);
				return mappedValue.Substring (mappedValue.LastIndexOf (Path.DirectorySeparatorChar) + 1);
			}

			Log.LogMessage ("  - Not remapping resource: {0}.{1}", type, name);

			return name;
		}

	}
}
