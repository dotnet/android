﻿using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Text.RegularExpressions;
using System.Text;

namespace Xamarin.Android.Tasks
{
	class ManagedResourceParser : ResourceParser
	{
		CodeTypeDeclaration resources;
		CodeTypeDeclaration layout, ids, drawable, strings, colors, dimension, raw, animator, animation, attrib, boolean, font, ints, interpolators, menu, mipmaps, plurals, styleable, style, arrays, xml, transition;
		Dictionary<string, string> map;
		bool app;
		SortedDictionary<string, CodeTypeDeclaration> custom_types = new SortedDictionary<string, CodeTypeDeclaration> ();
		List<CodeTypeDeclaration> declarationIds = new List<CodeTypeDeclaration> ();
		List<CodeTypeDeclaration> typeIds = new List<CodeTypeDeclaration> ();
		Dictionary<CodeMemberField, CodeMemberField []> arrayMapping = new Dictionary<CodeMemberField, CodeMemberField []> ();
		const string itemPackageId = "0x7f";

		XDocument publicXml;

		public string JavaPlatformDirectory { get; set; }

		public string ResourceFlagFile { get; set; }

		void SortMembers (CodeTypeDeclaration decl, StringComparison stringComparison = StringComparison.OrdinalIgnoreCase)
		{
			CodeTypeMember [] members = new CodeTypeMember [decl.Members.Count];
			decl.Members.CopyTo (members, 0);
			decl.Members.Clear ();
			Array.Sort (members, (x, y) => string.Compare (x.Name, y.Name, stringComparison));
			decl.Members.AddRange (members);
		}

		IEnumerable<CodeTypeMember> SortedMembers (CodeTypeDeclaration decl, StringComparer comparer)
		{
			return decl.Members.Cast<CodeTypeMember> ().OrderBy (x => x.Name, comparer);
		}

		public CodeTypeDeclaration Parse (string resourceDirectory, IEnumerable<string> additionalResourceDirectories, bool isApp, Dictionary<string, string> resourceMap)
		{
			app = isApp;
			if (!Directory.Exists (resourceDirectory))
				throw new ArgumentException ("Specified resource directory was not found: " + resourceDirectory);

			app = isApp;
			map = resourceMap ?? new Dictionary<string, string> ();
			resources = CreateResourceClass ();
			animation = CreateClass ("Animation");
			animator = CreateClass ("Animator");
			arrays = CreateClass ("Array");
			attrib = CreateClass ("Attribute");
			boolean = CreateClass ("Boolean");
			font = CreateClass ("Font");
			layout = CreateClass ("Layout");
			ids = CreateClass ("Id");
			ints = CreateClass ("Integer");
			interpolators = CreateClass ("Interpolator");
			menu = CreateClass ("Menu");
			mipmaps = CreateClass ("Mipmap");
			drawable = CreateClass ("Drawable");
			strings = CreateClass ("String");
			colors = CreateClass ("Color");
			dimension = CreateClass ("Dimension");
			raw = CreateClass ("Raw");
			plurals = CreateClass ("Plurals");
			styleable = CreateClass ("Styleable");
			style = CreateClass ("Style");
			transition = CreateClass ("Transition");
			xml = CreateClass ("Xml");

			string publicXmlPath = Path.Combine (JavaPlatformDirectory, "data", "res", "values", "public.xml");
			if (File.Exists (publicXmlPath)) {
				publicXml = XDocument.Load (publicXmlPath);
			}

			var resModifiedDate = !string.IsNullOrEmpty (ResourceFlagFile) && File.Exists (ResourceFlagFile)
				? File.GetLastWriteTimeUtc (ResourceFlagFile)
				: DateTime.MinValue;
			// This top most R.txt will contain EVERYTHING we need. including library resources since it represents
			// the final build.
			var rTxt = Path.Combine(resourceDirectory, "..", "R.txt");
			Log.LogDebugMessage ($"Checking {rTxt} {File.Exists (rTxt)} {File.GetLastWriteTimeUtc (rTxt)} {resModifiedDate}");
			if (File.Exists (rTxt) && File.GetLastWriteTimeUtc (rTxt) > resModifiedDate) {
				Log.LogDebugMessage ($"Processing File {rTxt}");
				ProcessRtxtFile (rTxt);
			} else {
				Log.LogDebugMessage ($"Processing Directory {resourceDirectory}");
				foreach (var dir in Directory.EnumerateDirectories (resourceDirectory, "*", SearchOption.TopDirectoryOnly)) {
					foreach (var file in Directory.EnumerateFiles (dir, "*.*", SearchOption.AllDirectories)) {
						ProcessResourceFile (file);
					}
				}
				if (additionalResourceDirectories != null) {
					foreach (var dir in additionalResourceDirectories) {
						Log.LogDebugMessage ($"Processing Directory {dir}");
						if (Directory.Exists (dir)) {
							foreach (var file in Directory.EnumerateFiles (dir, "*.*", SearchOption.AllDirectories)) {
								ProcessResourceFile (file);
							}
						} else {
							Log.LogDebugMessage ($"Skipping non-existent directory: {dir}");
						}
					}
				}
			}

			SortMembers (animation);
			SortMembers (animator);
			SortMembers (ids);
			SortMembers (attrib);
			SortMembers (arrays);
			SortMembers (boolean);
			SortMembers (colors);
			SortMembers (dimension);
			SortMembers (drawable);
			SortMembers (font);
			SortMembers (ints);
			SortMembers (interpolators);
			SortMembers (layout);
			SortMembers (mipmaps);
			SortMembers (menu);
			SortMembers (raw);
			SortMembers (plurals);
			SortMembers (strings);
			SortMembers (style);
			SortMembers (transition);
			SortMembers (xml);

			declarationIds.Add (attrib);
			declarationIds.Add (drawable);
			declarationIds.Add (mipmaps);
			declarationIds.Add (font);
			declarationIds.Add (layout);
			declarationIds.Add (animation);
			declarationIds.Add (animator);
			declarationIds.Add (transition);
			declarationIds.Add (xml);
			declarationIds.Add (raw);
			declarationIds.Add (dimension);
			declarationIds.Add (strings);
			declarationIds.Add (arrays);
			declarationIds.Add (plurals);
			declarationIds.Add (boolean);
			declarationIds.Add (colors);
			declarationIds.Add (ints);
			declarationIds.Add (menu);
			declarationIds.Add (ids);

			foreach (var customClass in custom_types) {
				SortMembers (customClass.Value);
				declarationIds.Add (customClass.Value);
			}

			declarationIds.Add (interpolators);
			declarationIds.Add (style);
			declarationIds.Add (styleable);

			declarationIds.Sort ((a, b) => {
				return string.Compare (a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
			});
			
			foreach (var codeDeclaration in declarationIds) {
				int itemid = 0;
				Log.LogDebugMessage ($"Processing {codeDeclaration.Name}");

				// We have to get the members in a different store order here becuase
				// aapt2 generates the ids based on an `Ordinal` order, but the
				// field output is in an `OrdinalIgnoreCase`.
				foreach (var fieldDeclaration in SortedMembers(codeDeclaration, StringComparer.Ordinal)) {
					CodeMemberField field = fieldDeclaration as CodeMemberField;
					if (field == null) {
						continue;
					}
					int typeid = typeIds.IndexOf (codeDeclaration) + 1;
					if (typeid == 0) {
						Log.LogDebugMessage ($"Adding code declaration for {codeDeclaration.Name} {typeid}");
						typeIds.Add (codeDeclaration);
						typeid = typeIds.Count;
					}
					Log.LogDebugMessage ($"code declaration {codeDeclaration.Name} typeid = {typeid} {field.Name}");
					if (field.InitExpression == null) {
						Log.LogDebugMessage ($"Adding InitExpression for {field.Name}");
						int id = Convert.ToInt32 (itemPackageId + typeid.ToString ("X2") + itemid.ToString ("X4"), fromBase: 16);
						Log.LogDebugMessage ($"{itemPackageId} {id} {itemid} = {id.ToString ("X")}");
						field.InitExpression = new CodePrimitiveExpression (id);
						field.Comments.Add (new CodeCommentStatement ($"aapt resource value: 0x{id.ToString ("X")}"));
						itemid++;
					}
				}
			}

			var sb = new StringBuilder ();
			SortedDictionary<int, CodeMemberField> arrayValues = new SortedDictionary<int, CodeMemberField> ();
			int value;
			foreach (var kvp in arrayMapping) {
				CodeMemberField field = kvp.Key;
				CodeArrayCreateExpression expression = field.InitExpression as CodeArrayCreateExpression;
				CodeMemberField [] fields = kvp.Value;

				int count = expression.Initializers.Count;
				sb.Clear ();
				arrayValues.Clear ();
				for (int i = 0; i < count ; i++) {
					string name = fields [i].Name;
					if (name.StartsWith ("android:", StringComparison.OrdinalIgnoreCase)) {
						name = name.Replace ("android:", string.Empty);
						var element = publicXml?.XPathSelectElement ($"/resources/public[@name='{name}']") ?? null;
						value = Convert.ToInt32 (element?.Attribute ("id")?.Value ?? "0x0", fromBase: 16);
						
					} else {
						CodePrimitiveExpression initExpression = fields [i].InitExpression as CodePrimitiveExpression;
						value = Convert.ToInt32 (initExpression.Value);
					}
					arrayValues.Add (value, fields [i]);
				}
				int index = 0;
				foreach (var arrayValue in arrayValues) {
					value = arrayValue.Key;
					CodeMemberField f = arrayValue.Value;
					CodePrimitiveExpression code = expression.Initializers [index] as CodePrimitiveExpression;
					code.Value = value;
					CreateIntField (styleable, $"{field.Name}_{f.Name}", index);
					sb.Append ($"0x{value.ToString ("X")}");
					if (index < count - 1)
						sb.Append (",");
					index++;
				}
				field.Comments.Add (new CodeCommentStatement ($"aapt resource value: {{ {sb} }}"));
			}

			SortMembers (styleable);

			foreach (var customClass in custom_types)
				resources.Members.Add (customClass.Value);

			if (animation.Members.Count > 1)
				resources.Members.Add (animation);
			if (animator.Members.Count > 1)
				resources.Members.Add (animator);
			if (arrays.Members.Count > 1)
				resources.Members.Add (arrays);
			//NOTE: aapt always emits Resource.Attribute, so we are replicating that
			resources.Members.Add (attrib);
			if (boolean.Members.Count > 1)
				resources.Members.Add (boolean);
			if (colors.Members.Count > 1)
				resources.Members.Add (colors);
			if (dimension.Members.Count > 1)
				resources.Members.Add (dimension);
			if (drawable.Members.Count > 1)
				resources.Members.Add (drawable);
			if (font.Members.Count > 1)
				resources.Members.Add (font);
			if (ids.Members.Count > 1)
				resources.Members.Add (ids);
			if (ints.Members.Count > 1)
				resources.Members.Add (ints);
			if (interpolators.Members.Count > 1)
				resources.Members.Add (interpolators);
			if (layout.Members.Count > 1)
				resources.Members.Add (layout);
			if (menu.Members.Count > 1)
				resources.Members.Add (menu);
			if (mipmaps.Members.Count > 1)
				resources.Members.Add (mipmaps);
			if (raw.Members.Count > 1)
				resources.Members.Add (raw);
			if (plurals.Members.Count > 1)
				resources.Members.Add (plurals);
			if (strings.Members.Count > 1)
				resources.Members.Add (strings);
			if (style.Members.Count > 1)
				resources.Members.Add (style);
			if (styleable.Members.Count > 1)
				resources.Members.Add (styleable);
			if (transition.Members.Count > 1)
				resources.Members.Add (transition);
			if (xml.Members.Count > 1)
				resources.Members.Add (xml);

			return resources;
		}

		void ProcessRtxtFile (string file)
		{
			var lines = System.IO.File.ReadLines (file);
			foreach (var line in lines) {
				var items = line.Split (new char [] { ' ' }, 4);
				int value = items [1] != "styleable" ? Convert.ToInt32 (items [3], 16) : -1;
				string itemName = items [2];
				switch (items [1]) {
				case "anim":
					CreateIntField (animation, itemName, value);
					break;
				case "animator":
					CreateIntField (animator, itemName, value);
					break;
				case "attr":
					CreateIntField (attrib, itemName, value);
					break;
				case "array":
					CreateIntField (arrays, itemName, value);
					break;
				case "bool":
					CreateIntField (boolean, itemName, value);
					break;
				case "color":
					CreateIntField (colors, itemName, value);
					break;
				case "dimen":
					CreateIntField (dimension, itemName, value);
					break;
				case "drawable":
					CreateIntField (drawable, itemName, value);
					break;
				case "font":
					CreateIntField (font, itemName, value);
					break;
				case "id":
					CreateIntField (ids, itemName, value);
					break;
				case "integer":
					CreateIntField (ints, itemName, value);
					break;
				case "interpolator":
					CreateIntField (interpolators, itemName, value);
					break;
				case "layout":
					CreateIntField (layout, itemName, value);
					break;
				case "menu":
					CreateIntField (menu, itemName, value);
					break;
				case "mipmap":
					CreateIntField (mipmaps, itemName, value);
					break;
				case "plurals":
					CreateIntField (plurals, itemName, value);
					break;
				case "raw":
					CreateIntField (raw, itemName, value);
					break;
				case "string":
					CreateIntField (strings, itemName, value);
					break;
				case "style":
					CreateIntField (style, itemName, value);
					break;
				case "styleable":
					switch (items [0]) {
					case "int":
						CreateIntField (styleable, itemName, Convert.ToInt32 (items [3], 10));
						break;
					case "int[]":
						var arrayValues = items [3].Trim (new char [] { '{', '}' })
							.Replace (" ", "")
							.Split (new char [] { ',' });
						CreateIntArrayField (styleable, itemName, arrayValues.Length,
							arrayValues.Select (x => string.IsNullOrEmpty (x) ? -1 : Convert.ToInt32 (x, 16)).ToArray ());
						break;
					}
					break;
				case "transition":
					CreateIntField (transition, itemName, value);
					break;
				case "xml":
					CreateIntField (xml, itemName, value);
					break;
				// for custom views
				default:
					CodeTypeDeclaration customClass;
					if (!custom_types.TryGetValue (items [1], out customClass)) {
							customClass = CreateClass (items [1]);
							custom_types.Add (items [1], customClass);
					}
					CreateIntField (customClass, itemName, value);
					break;
				}
			}
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
				try {
					ProcessXmlFile (file);
				} catch (XmlException ex) {
					Log.LogCodedWarning ("XA1000", $"There was an problem parsing {file}. This is likely due to incomplete or invalid xml. Exception: {ex}");
				}
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
			var t = new CodeTypeDeclaration (ResourceParser.GetNestedTypeName (type)) {
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
				Attributes = app ? MemberAttributes.Const | MemberAttributes.Public : MemberAttributes.Static | MemberAttributes.Public,
			};
			parentType.Members.Add (f);
		}

		CodeMemberField CreateIntField (CodeTypeDeclaration parentType, string name, int value = -1)
		{
			string mappedName = GetResourceName (parentType.Name, name, map);
			CodeMemberField f = (CodeMemberField)parentType.Members.OfType<CodeTypeMember> ().FirstOrDefault (x => string.Compare (x.Name, mappedName, StringComparison.Ordinal) == 0);
			if (f != null)
				return f;
			f = new CodeMemberField (typeof (int), mappedName) {
				// pity I can't make the member readonly...
				Attributes = app ? MemberAttributes.Const | MemberAttributes.Public : MemberAttributes.Static | MemberAttributes.Public,
				InitExpression = null,
			};
			if (value != -1) {
				f.InitExpression = new CodePrimitiveExpression (value);
				string valueName = parentType.Name == "Styleable" ? value.ToString () : $"0x{value.ToString ("X")}";
				f.Comments.Add (new CodeCommentStatement ($"aapt resource value: {valueName}"));
			}
			parentType.Members.Add (f);
			return f;
		}

		CodeMemberField CreateIntArrayField (CodeTypeDeclaration parentType, string name, int count, params int[] values)
		{
			string mappedName = GetResourceName (parentType.Name, name, map);
			CodeMemberField f = (CodeMemberField)parentType.Members.OfType<CodeTypeMember> ().FirstOrDefault (x => string.Compare (x.Name, mappedName, StringComparison.Ordinal) == 0);
			if (f != null)
				return f;
			f = new CodeMemberField (typeof (int []), name) {
				// pity I can't make the member readonly...
				Attributes = MemberAttributes.Static | MemberAttributes.Public,
			};
			CodeArrayCreateExpression c = (CodeArrayCreateExpression)f.InitExpression;
			if (c == null) {
				f.InitExpression = c = new CodeArrayCreateExpression (typeof (int []));
			}
			var sb = new StringBuilder ();
			for (int i = 0; i < count; i++) {
				int value = -1;
				if (i < values.Length)
					value = values[i];
				c.Initializers.Add (new CodePrimitiveExpression (value));
				sb.Append ($"0x{value.ToString ("X")}");
				if (i < count -1)
					sb.Append (",");
			}
			if (values.Length > 0)
				f.Comments.Add (new CodeCommentStatement ($"aapt resource value: {{ {sb} }}"));
			parentType.Members.Add (f);
			return f;
		}

		HashSet<string> resourceNamesToUseDirectly = new HashSet<string> () {
				"integer-array",
				"string-array",
				"declare-styleable",
				"add-resource",
			};

		void CreateResourceField (string root, string fieldName, XmlReader element = null)
		{
			var i = root.IndexOf ('-');
			var item = i < 0 ? root : root.Substring (0, i);
			item = resourceNamesToUseDirectly.Contains (root) ? root : item;
			switch (item.ToLower ()) {
			case "animator":
				CreateIntField (animator, fieldName);
				break;
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
				CreateIntField (dimension, fieldName);
				break;
			case "font":
				CreateIntField (font, fieldName);
				break;
			case "fraction":
				CreateIntField (dimension, fieldName);
				break;
			case "integer":
				CreateIntField (ints, fieldName);
				break;
			case "interpolator":
				CreateIntField (interpolators, fieldName);
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
			case "menu":
				CreateIntField (menu, fieldName);
				break;
			case "mipmap":
				CreateIntField (mipmaps, fieldName);
				break;
			case "raw":
				CreateIntField (raw, fieldName);
				break;
			case "plurals":
				CreateIntField (plurals, fieldName);
				break;
			case "string":
				CreateIntField (strings, fieldName);
				break;
			case "enum":
			case "flag":
			case "id":
				CreateIntField (ids, fieldName);
				break;
			case "array":
			case "integer-array":
			case "string-array":
				CreateIntField (arrays, fieldName);
				break;
			case "configVarying":
			case "add-resource":
			case "declare-styleable":
				ProcessStyleable (element);
				break;
			case "style":
				CreateIntField (style, fieldName);
				break;
			case "transition":
				CreateIntField (transition, fieldName);
				break;
			case "xml":
				CreateIntField (xml, fieldName);
				break;
			default:
				break;
			}
		}

		void ProcessStyleable (XmlReader reader)
		{
			string topName = null;
			int fieldCount = 0;
			List<CodeMemberField> fields = new List<CodeMemberField> ();
			List<string> attribs = new List<string> ();
			while (reader.Read ()) {
				if (reader.NodeType == XmlNodeType.Whitespace || reader.NodeType == XmlNodeType.Comment)
					continue;
				string name = null;
				if (string.IsNullOrEmpty (topName)) {
					if (reader.HasAttributes) {
						while (reader.MoveToNextAttribute ()) {
							if (reader.Name.Replace ("android:", "") == "name")
								topName = reader.Value;
						}
					}
				}
				if (!reader.IsStartElement () || reader.LocalName == "declare-styleable")
					continue;
				if (reader.HasAttributes) {
					while (reader.MoveToNextAttribute ()) {
						if (reader.Name.Replace ("android:", "") == "name")
							name = reader.Value;
					}
				}
				reader.MoveToElement ();
				if (reader.LocalName == "attr") {
					attribs.Add (name);
				} else {
					if (name != null)
						CreateIntField (ids, $"{name}");
				}
			}
			CodeMemberField field = CreateIntArrayField (styleable, topName, attribs.Count);
			if (!arrayMapping.ContainsKey (field)) {
				attribs.Sort (StringComparer.OrdinalIgnoreCase);
				for (int i = 0; i < attribs.Count; i++) {
					string name = attribs [i];
					if (!name.StartsWith ("android:", StringComparison.OrdinalIgnoreCase))
						fields.Add (CreateIntField (attrib, name));
					else {
						// this is an android:xxx resource, we should not calcuate the id
						// we should get it from "somewhere" maybe the pubic.xml
						var f = new CodeMemberField (typeof (int), name);
						f.InitExpression = new CodePrimitiveExpression (0);
						fields.Add (f);
					}
				}
				CodeArrayCreateExpression c = field.InitExpression as CodeArrayCreateExpression;
				if (c == null)
					return;
				
				arrayMapping.Add (field, fields.ToArray ());
			}
		}

		void ProcessXmlFile (string file)
		{
			using (var reader = XmlReader.Create (file)) {
				while (reader.Read ()) {
					if (reader.NodeType == XmlNodeType.Whitespace || reader.NodeType == XmlNodeType.Comment)
						continue;
					if (reader.IsStartElement ()) {
						var elementName = reader.Name;
						if (reader.HasAttributes) {
							CodeTypeDeclaration customClass = null;
							string name = null;
							string type = null;
							string id = null;
							string custom_id = null;
							while (reader.MoveToNextAttribute ()) {
								if (reader.LocalName == "name")
									name = reader.Value;
								if (reader.LocalName == "type")
									type = reader.Value;
								if (reader.LocalName == "id") {
									string[] values = reader.Value.Split ('/');
									if (values.Length != 2) {
										id = reader.Value.Replace ("@+id/", "").Replace ("@id/", "");
									} else {
										if (values [0] != "@+id" && values [0] != "@id" && !values [0].Contains ("android:")) {
											custom_id = values [0].Replace ("@", "").Replace ("+", "");
										}
										id = values [1];
									}
									
								}
								if (reader.LocalName == "inflatedId") {
									string inflateId = reader.Value.Replace ("@+id/", "").Replace ("@id/", "");
									CreateIntField (ids, inflateId);
								}
							}
							if (name?.Contains ("android:") ?? false)
								continue;
							if (id?.Contains ("android:") ?? false)
								continue;
							// Move the reader back to the element node.
							reader.MoveToElement ();
							if (!string.IsNullOrEmpty (name))
								CreateResourceField (type ?? elementName, name, reader.ReadSubtree ());
							if (!string.IsNullOrEmpty (custom_id) && !custom_types.TryGetValue (custom_id, out customClass)) {
								customClass = CreateClass (custom_id);
								custom_types.Add (custom_id, customClass);
							}
							if (!string.IsNullOrEmpty (id)) {
								CreateIntField (customClass ?? ids, id);
							}
						}
					}
				}
			}
		}
	}
}
