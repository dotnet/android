using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace Xamarin.Android.Tasks
{
	class ManagedResourceParser : ResourceParser
	{
		CodeTypeDeclaration resources;
		CodeTypeDeclaration layout, ids, drawable, strings, colors, dimension, raw, animation, attrib, boolean, ints, styleable, style;
		Dictionary<string, string> map;

		void SortMembers(CodeTypeDeclaration decl)
		{
			CodeTypeMember [] members = new CodeTypeMember [decl.Members.Count];
			decl.Members.CopyTo (members, 0);
			decl.Members.Clear ();
			Array.Sort (members, (x, y) => string.Compare (x.Name, y.Name, StringComparison.OrdinalIgnoreCase));
			decl.Members.AddRange (members);
		}

		public CodeTypeDeclaration Parse (string resourceDirectory, IEnumerable<string> additionalResourceDirectories, bool isApp, Dictionary<string, string> resourceMap)
		{
			if (!Directory.Exists (resourceDirectory))
				throw new ArgumentException ("Specified resource directory was not found: " + resourceDirectory);

			map = resourceMap ?? new Dictionary<string, string> ();
			resources = CreateResourceClass ();
			animation = CreateClass ("Animation");
			attrib = CreateClass ("Attribute");
			boolean = CreateClass ("Boolean");
			layout = CreateClass ("Layout");
			ids = CreateClass ("Id");
			ints = CreateClass ("Integer");
			drawable = CreateClass ("Drawable");
			strings = CreateClass ("String");
			colors = CreateClass ("Color");
			dimension = CreateClass ("Dimension");
			raw = CreateClass ("Raw");
			styleable = CreateClass ("Styleable");
			style = CreateClass ("Style");

			foreach (var dir in Directory.GetDirectories (resourceDirectory, "*", SearchOption.TopDirectoryOnly)) {
				foreach (var file in Directory.GetFiles (dir, "*.*", SearchOption.AllDirectories)) {
					ProcessResourceFile (file);
				}
			}
			if (additionalResourceDirectories != null) {
				foreach (var dir in additionalResourceDirectories) {
					foreach (var file in Directory.GetFiles (dir, "*.*", SearchOption.AllDirectories)) {
						ProcessResourceFile (file);
					}
				}
			}

			SortMembers (animation);
			SortMembers (ids);
			SortMembers (attrib);
			SortMembers (boolean);
			SortMembers (colors);
			SortMembers (dimension);
			SortMembers (drawable);
			SortMembers (ints);
			SortMembers (layout);
			SortMembers (raw);
			SortMembers (strings);
			SortMembers (style);
			SortMembers (styleable);


			if (animation.Members.Count > 1)
				resources.Members.Add (animation);
			if (attrib.Members.Count > 1)
				resources.Members.Add (attrib);
			if (boolean.Members.Count > 1)
				resources.Members.Add (boolean);
			if (colors.Members.Count > 1)
				resources.Members.Add (colors);
			if (dimension.Members.Count > 1)
				resources.Members.Add (dimension);
			if (drawable.Members.Count > 1)
				resources.Members.Add (drawable);
			if (ids.Members.Count > 1)
				resources.Members.Add (ids);
			if (ints.Members.Count > 1)
				resources.Members.Add (ints);
			if (layout.Members.Count > 1)
				resources.Members.Add (layout);
			if (raw.Members.Count > 1)
				resources.Members.Add (raw);
			if (strings.Members.Count > 1)
				resources.Members.Add (strings);
			if (style.Members.Count > 1)
				resources.Members.Add (style);
			if (styleable.Members.Count > 1)
				resources.Members.Add (styleable);

			return resources;
		}

		void ProcessResourceFile (string file)
		{
			var fileName = Path.GetFileNameWithoutExtension (file);
			if (string.IsNullOrEmpty (fileName))
				return;
			if (fileName.EndsWith (".9", StringComparison.OrdinalIgnoreCase))
				fileName = Path.GetFileNameWithoutExtension (fileName);
			var path = Directory.GetParent (file).Name;
			var ext = Path.GetExtension (file);
			switch (ext) {
				case ".xml":
				case ".axml":
					if (string.Compare (path, "raw", StringComparison.OrdinalIgnoreCase) == 0)
						goto default;
					ProcessXmlFile (file);
					break;
				default:
					break;
			}
			CreateResourceField (path, fileName);
		}

		CodeTypeDeclaration CreateResourceClass ()
		{
			var decl = new CodeTypeDeclaration ("Resource") {
				IsPartial = true,
			};
			var asm = Assembly.GetExecutingAssembly ().GetName ();
			var codeAttrDecl =
				new CodeAttributeDeclaration ("System.CodeDom.Compiler.GeneratedCodeAttribute",
					new CodeAttributeArgument (
						new CodePrimitiveExpression (asm.Name)),
					new CodeAttributeArgument (
						new CodePrimitiveExpression (asm.Version.ToString ()))
				);
			decl.CustomAttributes.Add (codeAttrDecl);
			return decl;
		}

		CodeTypeDeclaration CreateClass (string type)
		{
			var t = new CodeTypeDeclaration (JavaResourceParser.GetNestedTypeName (type)) {
				IsPartial = true,
				TypeAttributes = TypeAttributes.Public,
			};
			t.Members.Add (new CodeConstructor () {
				Attributes = MemberAttributes.Private,
			});
			return t;
		}

		void CreateField (CodeTypeDeclaration parentType, string name, Type type)
		{
			var f = new CodeMemberField (type, name) {
				// pity I can't make the member readonly...
				Attributes = MemberAttributes.Public | MemberAttributes.Static,
			};
			parentType.Members.Add (f);
		}

		void CreateIntField (CodeTypeDeclaration parentType, string name)
		{
			string mappedName = GetResourceName (parentType.Name, name, map);
			if (parentType.Members.OfType<CodeTypeMember> ().Any (x => string.Compare (x.Name, mappedName, StringComparison.OrdinalIgnoreCase) == 0))
				return;
			var f = new CodeMemberField (typeof (int), mappedName) {
				// pity I can't make the member readonly...
				Attributes = MemberAttributes.Static | MemberAttributes.Public,
				InitExpression = new CodePrimitiveExpression (0),
				Comments = {
					new CodeCommentStatement ("aapt resource value: 0"),
				},
			};
			parentType.Members.Add (f);
		}

		void CreateIntArrayField (CodeTypeDeclaration parentType, string name, int count)
		{
			string mappedName = GetResourceName (parentType.Name, name, map);
			if (parentType.Members.OfType<CodeTypeMember> ().Any (x => string.Compare (x.Name, mappedName, StringComparison.OrdinalIgnoreCase) == 0))
				return;
			var f = new CodeMemberField (typeof (int[]), name) {
				// pity I can't make the member readonly...
				Attributes = MemberAttributes.Public | MemberAttributes.Static,
			};
			CodeArrayCreateExpression c = (CodeArrayCreateExpression)f.InitExpression;
			if (c == null) {
				f.InitExpression = c = new CodeArrayCreateExpression (typeof (int []));
			}
			for (int i = 0; i < count;i++)
				c.Initializers.Add (new CodePrimitiveExpression (0));
			
			parentType.Members.Add (f);
		}

		HashSet<string> itemSubTypes = new HashSet<string> () {
			"integer-array",
			"string-array",
			"declare-styleable",
			"add-resource",
		};

		void CreateResourceField (string root, string fieldName, XElement element = null)
		{
			var i = root.IndexOf ('-');
			var item = i < 0 ? root : root.Substring (0, i);
			item = itemSubTypes.Contains (root) ? root : item;
			switch (item.ToLower ()) {
			case "bool":
				CreateIntField (boolean, fieldName);
				break;
			case "color":
				CreateIntField (colors, fieldName);
				break;
			case "drawable":
				CreateIntField (drawable, fieldName);
				break;
			case "dimen":
			case "fraction":
				CreateIntField (dimension, fieldName);
				break;
			case "integer":
				CreateIntField (ints, fieldName);
				break;
			case "anim":
				CreateIntField (animation, fieldName);
				break;
			case "attr":
				CreateIntField (attrib, fieldName);
				break;
			case "layout":
				CreateIntField (layout, fieldName);
				break;
			case "raw":
				CreateIntField (raw, fieldName);
				break;
			case "string":
				CreateIntField (strings, fieldName);
				break;
			case "enum":
			case "flag":
				CreateIntField (ids, fieldName);
				break;
			case "configVarying":
			case "integer-array":
			case "string-array":
			case "add-resource":
			case "declare-styleable":
				ProcessStyleable (element);
				break;
			case "style":
				// special case Style 
				//ProcessStyle (element);
				CreateIntField (style, fieldName.Replace (".", "_"));
				break;
			default:
				//Log.LogDebugMessage ($"default {root} => {fieldName}");
				break;
			}
		}

		void ProcessStyleable (XElement element)
		{
			var topName = element.Attribute ("name").Value;
			var items = element.Descendants ().Where (x => x.Name.LocalName == "attr");
			CreateIntArrayField (styleable, topName, items.Count ());
			foreach (var item in items) {
				if (item.Name.LocalName == "attr") {
					CreateIntField (styleable, $"{topName}_{item.Attribute ("name").Value.Replace (":", "_")}");
				}
			}
		}

		void ProcessXmlFile (string file) {
			var doc = XDocument.Load (file);
			var ns = XNamespace.Get ("http://schemas.android.com/apk/res/android");
			var nameAttributes = doc.Descendants ().Attributes (ns + "name")
					.Concat (doc.Descendants ().Attributes ("name"));
			foreach (var attr in nameAttributes) {
				// skip android: prefixed items
				if (attr.Value.Contains ("android:"))
					continue;
				if (attr.Parent.Name.LocalName == "item") {
					if (attr.Parent.Attribute ("type") != null)
						CreateResourceField (attr.Parent.Attribute ("type").Value, attr.Name.LocalName, attr.Parent);
					else {
						var f = attr.Parent.Parent.Name;
						Log.LogDebugMessage ($"Item {attr.Name} {attr.Value} {attr.Parent.Attribute ("type")?.Value ?? "noval"} {f}");
					}
				} else
					CreateResourceField (attr.Parent.Name.LocalName, attr.Value, attr.Parent);
			}
			var attrs = doc.Descendants ().Attributes (ns + "id")
					.Concat (doc.Descendants ().Attributes ("id"));
			foreach (var attr in attrs) {
				var name = attr.Value.Replace ("@+id/", "").Replace ("@id/", "");
				// skip android prefixed items
				if (attr.Value.Contains ("android:"))
					continue;
				CreateIntField (ids, name);
			}
		}
	}
}
