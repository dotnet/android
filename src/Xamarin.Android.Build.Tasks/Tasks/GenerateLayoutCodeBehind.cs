using System;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Collections;
using System.Collections.Generic;
using System.CodeDom.Compiler;
using System.Xml.Linq;
using System.CodeDom;
using System.Xml;
using System.Linq;
using System.Text;
using System.IO;
using Microsoft.CSharp;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Xamarin.Android.Tasks {

	public class GenerateCodeBehindForLayout : Task {

		const string UserDataIsMainKey = "IsMain";
		const string ChildClassParentFieldName = "__parent";

		sealed class Widget {
			Lazy<List<Widget>> children = new Lazy<List<Widget>> (false);
			Lazy<HashSet<string>> idCache = new Lazy<HashSet<string>> (false);

			public List<Widget> Children {
				get { return children.Value; }
			}

			public HashSet<string> IDCache {
				get { return idCache.Value; }
			}

			public bool IsLeaf {
				get { return !children.IsValueCreated || children.Value.Count == 0; }
			}

			public string Name { get; set; }
			public string Type { get; set; }
			public string ID { get; set; }
			public string FileName { get; set; }
			public int Line { get; set; }
			public int Column { get; set; }
			public Widget Parent { get; set; }
			public bool IsInaccessible { get; set; }
			public bool IsRoot { get; set; }
			public bool IsFragment { get; set; }

			public void AddChild (Widget child)
			{
				if (child == null)
					return;
				if (String.IsNullOrEmpty (child.ID))
					throw new InvalidOperationException ("Attempt to add a child without ID");

				Children.Add (child);
				if (IDCache.Contains (child.ID))
					child.IsInaccessible = true;
				else
					IDCache.Add (child.ID);
			}
		}

		enum MethodAccessibility {
			Internal,
			Private,
			Protected,
			Public,
		};

		[Flags]
		enum MethodScope {
			Abstract = 0x01,
			Override = 0x02,
			Static = 0x04,
			Virtual = 0x08,
			Final = 0x10,
		}

		static readonly List<string> StandardImports = new List<string> {
			"System",
			"Android.App",
			"Android.Widget",
			"Android.Views",
			"Android.OS"
		};

		[Required]
		public ITaskItem [] ResourceFiles { get; set; }

		[Required]
		public string AndroidFragmentType { get; set; }

		public string MonoAndroidCodeBehindDir { get; set; }

		[Output]
		public ITaskItem [] GeneratedFiles { get; set; }

		public override bool Execute ()
		{
			Log.LogDebugMessage ("GenerateCodeBehindForLayout Task");
			Log.LogDebugMessage ("  MonoAndroidCodeBehindDir: {0}", MonoAndroidCodeBehindDir);
			Log.LogDebugTaskItems ("  ResourceFiles:", ResourceFiles);

			var generatedFiles = new List<ITaskItem> ();

			var generatorOptions = new CodeGeneratorOptions {
				BlankLinesBetweenMembers = true,
				VerbatimOrder = false,
			};
			foreach (var resourceFile in ResourceFiles) {
				ITaskItem item = GenerateCodeBehind (resourceFile, generatorOptions);
				if (item != null)
					generatedFiles.Add (item);

			}

			GeneratedFiles = generatedFiles.ToArray ();

			Log.LogDebugTaskItems ("  GeneratedFiles:", GeneratedFiles);
			return !Log.HasLoggedErrors;
		}

		void GetLineInfo (IXmlLineInfo linfo, out int line, out int column)
		{
			if (linfo == null || !linfo.HasLineInfo ()) {
				line = column = 1;
				return;
			}
			line = linfo.LineNumber;
			column = linfo.LinePosition;
		}

		readonly string classSuffix = "class";
		readonly string managedTypeSuffix = "managedType";
		readonly string androidSuffix = "android";
		readonly string toolsNamespace = "http://schemas.xamarin.com/android/tools";
		readonly string namespaceUri = "http://www.w3.org/2000/xmlns/";

		bool GenerateLayoutMembers (CodeTypeDeclaration mainClass, string fileName)
		{
			string klass = null;
			string androidNS = null;
			var globalIdCache = new Dictionary<string, int> (StringComparer.Ordinal);
			var root = new Widget {
				IsRoot = true
			};
			var options = new XmlReaderSettings () {
				IgnoreComments = true,
				IgnoreWhitespace = true,
			};
			using (var fs = File.OpenRead (fileName)) {
				using (var reader = XmlReader.Create (fs, options)) {
					var lineInfo = reader as IXmlLineInfo;
					while (reader.Read ()) {
						if (reader.IsStartElement ()) {
							if (string.IsNullOrEmpty (klass)) {
								klass = reader.GetAttribute (classSuffix, toolsNamespace);
								if (string.IsNullOrEmpty (klass)) {
									Log.LogError ($"Layout file '{fileName}' doesn't have root element with the 'tools:class' attribute or the attribute has invalid value");
									return false;
								}
							}
							if (string.IsNullOrEmpty (androidNS)) {
								androidNS = reader.GetAttribute (androidSuffix, namespaceUri);
							}
							if (!string.IsNullOrEmpty (androidNS) && !string.IsNullOrEmpty (klass)) {
								using (var subtree = reader.ReadSubtree ()) {
									while (subtree.Read ())
										LoadWidgets (fileName, subtree, lineInfo, androidNS, root, globalIdCache);
								}
							}

						}
					}
				}
			}
			return GenerateWidgetMembers (mainClass, mainClass.Name, root, globalIdCache, String.Empty);
		}

		void LoadWidgets (string fileName, XmlReader reader, IXmlLineInfo lineinfo, string androidNS, Widget widgetRoot, Dictionary<string, int> globalIdCache)
		{
			if (reader == null)
				return;
			string id =  reader.GetAttribute ("id", androidNS);
			Widget root = null;
			if (id != null) {
				root = CreateWidget (fileName, reader, lineinfo, id, androidNS, widgetRoot);
				if (root != null)
					Log.LogDebugMessage ($"Adding Widget '{root.Name}' with ID '{root.ID}' to parent widget '{widgetRoot.ID}' ({widgetRoot.Name})");
				widgetRoot.AddChild (root);
				if (!String.IsNullOrEmpty (root?.ID)) {
					if (globalIdCache.ContainsKey (root.ID))
						globalIdCache [root.ID]++;
					else
						globalIdCache.Add (root.ID, 1);
				}
			}

			if (reader.NodeType != XmlNodeType.Element)
				return;

			using (var subtree = reader.ReadSubtree ()) {
				bool first = true;
				while (subtree.Read ()) {
					// This check is here because `ReadSubtree` above returns the parent of the tree
					// along with all of its children and thus passing the first element (the
					// parent) to the recursive call would cause endless recursion and, eventually,
					// stack overflow
					if (first) {
						first = false;
						continue;
					}

					LoadWidgets (fileName, subtree, lineinfo, androidNS, root ?? widgetRoot, globalIdCache);
				}
			}

			if (root == null || root.IsLeaf)
				return;

			// This is a shortcut to generate a viewclass.Widget member which is the *actual* Android view
			root.AddChild (new Widget {
				Name = "Widget",
				Type = root.Type,
				ID = root.ID,
				Parent = root.Parent,
				FileName = root.FileName,
				Line = root.Line,
				Column = root.Column,
				IsFragment = root.IsFragment,
			});
		}

		Widget CreateWidget (string fileName, XmlReader e, IXmlLineInfo lineInfo, string id, string androidNS, Widget parent)
		{
			int line, column;
			GetLineInfo (lineInfo, out line, out column);

			if (String.IsNullOrEmpty (id)) {
				Log.LogWarning ($"Element {e.Name} defined at '{fileName}:({line},{column})' has an empty ID");
				return null;
			}

			string parsedId, name;
			bool isFragment = String.Compare ("fragment", e.LocalName, StringComparison.Ordinal) == 0;
			string managedType = e.GetAttribute (managedTypeSuffix, toolsNamespace)?.Trim ();
			if (String.IsNullOrEmpty (managedType)) {
				if (isFragment) {
					managedType = e.GetAttribute ("name", androidNS)?.Trim ();
					if (String.IsNullOrEmpty (managedType))
						managedType = "global::Android.App.Fragment";
				} else
					managedType = e.LocalName;
			}

			int comma = managedType.IndexOf (',');
			if (comma >= 0)
				managedType = managedType.Substring (0, comma).Trim ();

			if (String.IsNullOrEmpty (managedType)) {
				Log.LogError ($"Unable to determine managed type for element '{e.Name}' defined at '{fileName}:({line},{column})'");
				return null;
			}

			ParseID (id, out parsedId, out name);
			var ret = new Widget {
				Name = name,
				Type = managedType,
				ID = parsedId,
				Parent = parent,
				FileName = fileName,
				Line = line,
				Column = column,
				IsFragment = isFragment,
			};

			return ret;
		}

		bool GenerateWidgetMembers (CodeTypeDeclaration klass, string parentType, Widget widget, Dictionary<string, int> globalIdCache, string indent)
		{
			if (!widget.IsRoot) {
				Log.LogDebugMessage ($"Widget members for class {klass.Name}");
				Log.LogDebugMessage ($"{indent}Widget {widget.Name} with ID '{widget.ID}' and type '{widget.Type}'; parent type '{parentType}'");
			}

			if (widget.IsInaccessible)
				return true;

			if (widget.IsLeaf) {
				Log.LogDebugMessage ($"Generating leaf member for widget {widget.Name} with ID '{widget.ID}' and type '{widget.Type}'");
				return GenerateLeafWidgetMember (klass, widget, globalIdCache);
			}

			CodeTypeDeclaration widgetClass;
			if (widget.IsRoot)
				widgetClass = klass;
			else {
				if (!GenerateGroupWidgetMember (klass, parentType, widget, globalIdCache, out widgetClass))
					return false;
			}

			bool ret = true;
			foreach (Widget child in widget.Children) {
				// Don't fail when one widget fails, try to generate as many members as possible thus
				// making it possible for the developer to know what all is broken at a glance instead
				// of re-running the build
				if (!GenerateWidgetMembers (widgetClass, parentType, child, globalIdCache, indent + "   "))
					ret = false;
			}

			return ret;
		}

		bool CreateWidgetMembers (CodeTypeDeclaration klass, Widget widget, string memberType, Dictionary<string, int> globalIdCache, CodeMemberMethod creator)
		{
			bool ret = true;
			CodeMemberField backingField = CreateBackingField (widget, memberType);
			CodeMemberField backingFuncField = CreateBackingFuncField (widget, memberType);

			klass.Members.Add (backingFuncField);
			klass.Members.Add (backingField);
			klass.Members.Add (creator);
			klass.Members.Add (CreateProperty (widget, memberType, backingField, backingFuncField, creator, GetParent (klass)));

			return ret;
		}

		CodeExpression GetParent (CodeTypeDeclaration klass)
		{
			if (klass.UserData.Contains (UserDataIsMainKey) && (bool)klass.UserData [UserDataIsMainKey])
				return new CodeThisReferenceExpression ();
			return new CodeFieldReferenceExpression (new CodeThisReferenceExpression (), ChildClassParentFieldName);
		}

		bool GenerateLeafWidgetMember (CodeTypeDeclaration klass, Widget widget, Dictionary<string, int> globalIdCache)
		{
			List<Widget> findPath = GetShortestFindPath (widget, globalIdCache);
			return CreateWidgetMembers (klass, widget, widget.Type, globalIdCache, ImplementWidgetCreator (widget, GetParent (klass), findPath));
		}

		bool GenerateGroupWidgetMember (CodeTypeDeclaration klass, string parentType, Widget widget, Dictionary<string, int> globalIdCache, out CodeTypeDeclaration widgetClass)
		{
			string className = GetClassName (widget);
			widgetClass = AddLayoutClass (klass, GetClassName (widget), parentType, widget);
			return CreateWidgetMembers (klass, widget, className, globalIdCache, ImplementLayoutClassCreator (widget));
		}

		List<Widget> GetShortestFindPath (Widget widget, Dictionary<string, int> globalIdCache)
		{
			if (HasUniqueId (widget, globalIdCache)) {
				return new List<Widget> {
					widget
				};
			}

			var ret = new List<Widget> ();
			var w = widget.Parent;
			while (w != null && !w.IsRoot) {
				ret.Add (w);
				if (HasUniqueId (w, globalIdCache))
					break;
				w = w.Parent;
			}
			ret.Add (widget);

			return ret;
		}

		CodeMemberProperty CreateProperty (Widget widget, string returnType, CodeMemberField backingField, CodeMemberField backingFuncField, CodeMemberMethod creator, CodeExpression parent)
		{
			var ensureViewRef = new CodeMethodReferenceExpression (parent, "__EnsureView");
			ensureViewRef.TypeArguments.Add (new CodeTypeReference (returnType));

			var backingFuncFieldReference = new CodeFieldReferenceExpression (new CodeThisReferenceExpression (), backingFuncField.Name);

			var ensureViewInvoke = new CodeMethodInvokeExpression (
				ensureViewRef,
				new CodeExpression [] {
					backingFuncFieldReference,
					new CodeDirectionExpression (FieldDirection.Ref, new CodeFieldReferenceExpression (new CodeThisReferenceExpression (), backingField.Name)),
				}
			);

			var ret = new CodeMemberProperty {
				Name = widget.Name,
				HasGet = true,
				HasSet = false,
				Type = new CodeTypeReference (returnType),
				LinePragma = new CodeLinePragma (widget.FileName, widget.Line),
				Attributes = MemberAttributes.Public | MemberAttributes.Final
			};

			var assignFunc = new CodeAssignStatement (
				backingFuncFieldReference,
				new CodeMethodReferenceExpression (new CodeThisReferenceExpression (), creator.Name)
			);

			ret.GetStatements.Add (new CodeConditionStatement (
				new CodeBinaryOperatorExpression (backingFuncFieldReference, CodeBinaryOperatorType.IdentityEquality, new CodePrimitiveExpression (null)),
				new CodeStatement [] { assignFunc }
			));
			ret.GetStatements.Add (new CodeMethodReturnStatement (ensureViewInvoke));
			return ret;
		}

		string GetClassName (Widget widget)
		{
			return $"__{widget.Name}_Views";
		}

		CodeMemberMethod ImplementLayoutClassCreator (Widget widget)
		{
			string className = GetClassName (widget);
			CodeMemberMethod method = CreateMethod ($"__CreateClass_{className}", MethodAccessibility.Private, MethodScope.Final, className);

			CodeExpression parent;

			if (widget.Parent == null || widget.Parent.IsRoot)
				parent = new CodeThisReferenceExpression ();
			else
				parent = new CodeFieldReferenceExpression (new CodeThisReferenceExpression (), "__parent");
			var instantiate = new CodeObjectCreateExpression (
				className,
				new [] { parent }
			);
			method.Statements.Add (new CodeMethodReturnStatement (instantiate));

			return method;
		}

		CodeMemberMethod ImplementWidgetCreator (Widget widget, CodeExpression parent, List<Widget> findPath)
		{
			CodeMemberMethod method = CreateMethod ($"__Create_{widget.Name}", MethodAccessibility.Private, MethodScope.Final, widget.Type);

			if (findPath.Count == 1) {
				method.Statements.Add (new CodeMethodReturnStatement (CreateFindViewInvoke (findPath [0], parent, parent)));
				return method;
			}
			var viewVar = new CodeVariableDeclarationStatement ("View", "view");
			var viewVarRef = new CodeVariableReferenceExpression ("view");
			method.Statements.Add (viewVar);

			CodeExpression parentView = parent;
			foreach (Widget w in findPath) {
				CodeMethodInvokeExpression findViewCall = CreateFindViewInvoke (w, parent, parentView);
				var assignView = new CodeAssignStatement (viewVarRef, findViewCall);
				var ifViewNull = new CodeConditionStatement (
					new CodeBinaryOperatorExpression (viewVarRef, CodeBinaryOperatorType.IdentityInequality, new CodePrimitiveExpression (null)),
					new [] { new CodeMethodReturnStatement (new CodePrimitiveExpression (null)) }
				);

				method.Statements.Add (assignView);
				method.Statements.Add (ifViewNull);

				if (parentView == parent)
					parentView = viewVarRef;
			}

			method.Statements.Add (new CodeMethodReturnStatement (new CodeCastExpression (widget.Type, viewVarRef)));

			return method;
		}

		CodeMethodInvokeExpression CreateFindViewInvoke (Widget widget, CodeExpression parent, CodeExpression parentView)
		{
			string methodName = widget.IsFragment ? "__FindFragment" : "__FindView";
			var findViewRef = new CodeMethodReferenceExpression (parent, methodName);
			findViewRef.TypeArguments.Add (new CodeTypeReference (widget.Type));

			return new CodeMethodInvokeExpression (findViewRef, new CodeExpression [] { parentView, new CodeSnippetExpression (widget.ID) });
		}

		CodeMemberField CreateBackingField (Widget widget, string memberType)
		{
			return new CodeMemberField (memberType, $"__{widget.Name}") {
				LinePragma = new CodeLinePragma (widget.FileName, widget.Line)
			};
		}

		CodeMemberField CreateBackingFuncField (Widget widget, string memberType)
		{
			return new CodeMemberField ($"global::System.Func<{memberType}>", $"__{widget.Name}Func");
		}

		bool HasUniqueId (Widget widget, Dictionary<string, int> globalIdCache)
		{
			int count;
			return (!globalIdCache.TryGetValue (widget.ID, out count) || count == 1);
		}

		void ParseID (string id, out string parsedId, out string name)
		{
			parsedId = null;
			name = null;
			id = id?.Trim ();
			if (String.IsNullOrEmpty (id))
				return;

			string ns;
			bool capitalize = false;
			if (id.StartsWith ("@id/", StringComparison.Ordinal) || id.StartsWith ("@+id/", StringComparison.Ordinal))
				ns = "Resource.Id";
			else if (id.StartsWith ("@android:id/")) {
				ns = "Android.Resource.Id";
				capitalize = true;
			} else
				throw new InvalidOperationException ($"Unknown Android ID format '{id}'");

			var sb = new StringBuilder (id.Substring (id.IndexOf ('/') + 1));
			if (capitalize)
				sb [0] = Char.ToUpper (sb [0]);

			name = sb.ToString ();
			parsedId = $"{ns}.{name}";
		}

		ITaskItem GenerateCodeBehind (ITaskItem layoutFile, CodeGeneratorOptions generatorOptions)
		{
			string codeBehindFile = layoutFile.GetMetadata ("CodeBehindFileName");
			if (string.IsNullOrEmpty (codeBehindFile)) {
				Log.LogError ($"Required MetaData 'CodeBehindFileName' for {layoutFile} was not found.");
				return null;
			}
			string partialClassName = layoutFile.GetMetadata ("ClassName");
			if (String.IsNullOrEmpty (partialClassName)) {
				Log.LogError ($"Required MetaData 'ClassName' for {layoutFile} was not found.");
				return null;
			}
			int idx = partialClassName.LastIndexOf ('.');
			string className;
			string namespaceName;

			if (idx >= 0) {
				className = partialClassName.Substring (idx + 1);
				namespaceName = partialClassName.Substring (0, idx);
			} else {
				className = partialClassName;
				namespaceName = null;
			}

			if (String.IsNullOrEmpty (className)) {
				Log.LogError ($"Layout file {layoutFile.ItemSpec} doesn't specify a valid code-behind class name. It cannot be empty.");
				return null;
			}

			var compileUnit = new CodeCompileUnit ();
			var ns = new CodeNamespace (namespaceName);
			compileUnit.Namespaces.Add (ns);
			foreach (string import in StandardImports)
				ns.Imports.Add (new CodeNamespaceImport ($"global::{import}"));

			CodeTypeDeclaration mainClass = AddMainClass (layoutFile, ns, className);
			if (!GenerateLayoutMembers (mainClass, Path.GetFullPath (layoutFile.ItemSpec)))
				Log.LogError ($"Layout code-behind failed for '{layoutFile.ItemSpec}'");
			else {
				var provider = new CSharpCodeProvider ();
				using (var sw = new StreamWriter (Path.Combine (MonoAndroidCodeBehindDir, codeBehindFile), false, Encoding.UTF8)) {
					using (var tw = new IndentedTextWriter (sw, "\t")) {
						provider.GenerateCodeFromCompileUnit (compileUnit, tw, generatorOptions);
					}
				}
			}

			return new TaskItem (codeBehindFile); ;
		}

		CodeTypeDeclaration CreateClass (string className, bool isPartial = false, bool isPublic = true, bool isNested = false, bool isSealed = false)
		{
			var ret = new CodeTypeDeclaration (className) {
				IsClass = true,
				IsPartial = isPartial,
			};

			TypeAttributes typeAttributes;
			if (isPublic)
				typeAttributes = isNested ? TypeAttributes.NestedPublic : TypeAttributes.Public;
			else
				typeAttributes = isNested ? TypeAttributes.NestedPrivate : TypeAttributes.NotPublic;

			ret.TypeAttributes = (ret.TypeAttributes & ~TypeAttributes.VisibilityMask) | typeAttributes;
			if (isSealed)
				ret.TypeAttributes |= TypeAttributes.Sealed;

			return ret;
		}

		CodeTypeDeclaration AddLayoutClass (CodeTypeDeclaration outerClass, string className, string parentType, Widget widget)
		{
			CodeTypeDeclaration klass = CreateLayoutClass (outerClass, className, parentType, widget);
			outerClass.Members.Add (klass);
			return klass;
		}

		CodeTypeDeclaration CreateLayoutClass (CodeTypeDeclaration mainClass, string className, string parentType, Widget widget)
		{
			CodeTypeDeclaration ret = CreateClass (className, isPartial: false, isPublic: true, isNested: true, isSealed: true);

			var mainClassTypeRef = new CodeTypeReference (parentType);
			ret.Members.Add (new CodeMemberField (mainClassTypeRef, ChildClassParentFieldName));

			var constructor = new CodeConstructor {
				Attributes = MemberAttributes.Public,
			};
			constructor.Parameters.Add (new CodeParameterDeclarationExpression (mainClassTypeRef, ChildClassParentFieldName));

			var assignParent = new CodeAssignStatement (
				new CodeFieldReferenceExpression (new CodeThisReferenceExpression (), ChildClassParentFieldName),
				new CodeVariableReferenceExpression (ChildClassParentFieldName)
			);
			constructor.Statements.Add (assignParent);

			ret.Members.Add (constructor);

			return ret;
		}

		CodeTypeDeclaration AddMainClass (ITaskItem layoutFile, CodeNamespace ns, string className)
		{
			CodeTypeDeclaration klass = CreateMainClass (layoutFile, className);
			klass.UserData [UserDataIsMainKey] = true;
			ns.Types.Add (klass);
			return klass;
		}

		CodeTypeDeclaration CreateMainClass (ITaskItem layoutFile, string className)
		{
			var ret = CreateClass (className, isPartial: true, isPublic: true);

			AddComment (ret.Comments, $"Generated from layout file '{layoutFile.ItemSpec}'");
			AddCommonMembers (ret, layoutFile);

			return ret;
		}

		void AddCommonMembers (CodeTypeDeclaration klass, ITaskItem layoutFile)
		{
			var activityTypeRef = new CodeTypeReference ("Android.App.Activity", CodeTypeReferenceOptions.GlobalReference);

			klass.Members.Add (ImplementInitializeContentView (layoutFile));
			klass.Members.Add (ImplementFindView (new CodeTypeReference ("Android.Views.View", CodeTypeReferenceOptions.GlobalReference)));
			klass.Members.Add (ImplementFindView (activityTypeRef));
			klass.Members.Add (ImplementFindView (new CodeTypeReference ("Android.App.Fragment", CodeTypeReferenceOptions.GlobalReference), activityTypeRef, (CodeVariableReferenceExpression parentView) => new CodePropertyReferenceExpression (parentView, "Activity")));
			klass.Members.Add (ImplementFindFragment (activityTypeRef));
			klass.Members.Add (ImplementEnsureView ());
			klass.Members.Add (new CodeSnippetTypeMember ("\tpartial void OnLayoutViewNotFound<T> (int resourceId, ref T type) where T : global::Android.Views.View;"));
			klass.Members.Add (new CodeSnippetTypeMember ("\tpartial void OnLayoutFragmentNotFound<T> (int resourceId, ref T type) where T : global::Android.App.Fragment;"));
		}

		CodeMemberMethod ImplementInitializeContentView (ITaskItem layoutFile)
		{
			CodeMemberMethod method = CreateMethod ("InitializeContentView", MethodAccessibility.Private, MethodScope.Final);

			// SetContentView (Resource.Layout.Main);
			string layoutResourceName = $"Resource.Layout.{Path.GetFileNameWithoutExtension (layoutFile.ItemSpec)}";
			var methodInvoke = new CodeMethodInvokeExpression (
				new CodeThisReferenceExpression (),
				"SetContentView",
				new [] { new CodeSnippetExpression (layoutResourceName) }
			);

			method.Statements.Add (new CodeExpressionStatement (methodInvoke));
			return method;
		}

		CodeMemberMethod ImplementEnsureView ()
		{
			CodeMemberMethod method = CreateMethod ("__EnsureView", MethodAccessibility.Private, MethodScope.Final);

			// T __EnsureView <T> (Func<T> creator, ref T field) where T: class
			var typeParam = new CodeTypeParameter ("T");
			typeParam.Constraints.Add (" class"); // Hack: CodeDOM doesn't support the "class" constraint
							      // and not passing the leading whitespace would result
							      // in @class being output in generated code
			method.TypeParameters.Add (typeParam);

			var tRef = new CodeTypeReference (typeParam);
			var funcRef = new CodeTypeReference (typeof (Func<>), CodeTypeReferenceOptions.GlobalReference);
			funcRef.TypeArguments.Add (tRef);
			method.Parameters.Add (new CodeParameterDeclarationExpression (funcRef, "creator"));

			method.Parameters.Add (
				new CodeParameterDeclarationExpression (tRef, "field") {

					Direction = FieldDirection.Ref
				}
			);
			method.ReturnType = tRef;

			// if (field != null)
			//    return field;
			var fieldVarRef = new CodeVariableReferenceExpression ("field");
			var ifFieldNotNull = new CodeConditionStatement (
				new CodeBinaryOperatorExpression (fieldVarRef, CodeBinaryOperatorType.IdentityInequality, new CodePrimitiveExpression (null)),
				new [] { new CodeMethodReturnStatement (fieldVarRef) }
			);
			method.Statements.Add (ifFieldNotNull);

			// if (creator == null)
			//    throw new ArgumentNullException (nameof (creator));
			var creatorVarRef = new CodeVariableReferenceExpression ("creator");
			var argNullEx = new CodeThrowExceptionStatement (
				new CodeObjectCreateExpression (
					new CodeTypeReference (typeof (ArgumentNullException), CodeTypeReferenceOptions.GlobalReference),
					new [] { new CodeSnippetExpression ("nameof (creator)") }
				)
			);
			var ifCreatorNull = new CodeConditionStatement (
				new CodeBinaryOperatorExpression (creatorVarRef, CodeBinaryOperatorType.IdentityEquality, new CodePrimitiveExpression (null)),
				new [] { argNullEx }
			);
			method.Statements.Add (ifCreatorNull);

			// field = creator ();
			method.Statements.Add (new CodeAssignStatement (fieldVarRef, new CodeDelegateInvokeExpression (creatorVarRef)));

			// return field;
			method.Statements.Add (new CodeMethodReturnStatement (fieldVarRef));
			return method;
		}

		CodeMemberMethod ImplementFindView (CodeTypeReference typeForParent, CodeTypeReference typeForOverloadCall = null, Func<CodeVariableReferenceExpression, CodeExpression> constructParentViewCall = null)
		{
			CodeMemberMethod method = CreateMethod ("__FindView", MethodAccessibility.Private, MethodScope.Final);

			// T __FindView<T> (int resourceId) where T: Android.Views.View
			var typeParam = new CodeTypeParameter ("T");
			typeParam.Constraints.Add (new CodeTypeReference ("Android.Views.View", CodeTypeReferenceOptions.GlobalReference));
			method.TypeParameters.Add (typeParam);
			method.Parameters.Add (new CodeParameterDeclarationExpression (typeForParent, "parentView"));
			method.Parameters.Add (new CodeParameterDeclarationExpression (typeof (int), "resourceId"));

			var tReference = new CodeTypeReference (typeParam);
			method.ReturnType = tReference;

			// T view = parentView.FindViewById<T> (resourceId);
			var parentViewRef = new CodeVariableReferenceExpression ("parentView");
			var resourceIdVarRef = new CodeVariableReferenceExpression ("resourceId");

			if (typeForOverloadCall != null) {
				var findViewRef = new CodeMethodReferenceExpression (
					new CodeThisReferenceExpression (),
					"__FindView",
					new [] { tReference }
				);

				CodeExpression parentViewParam;
				if (constructParentViewCall != null)
					parentViewParam = constructParentViewCall (parentViewRef);
				else
					parentViewParam = parentViewRef;
				var findViewCall = new CodeMethodInvokeExpression (findViewRef, new CodeExpression [] { parentViewParam, resourceIdVarRef });
				method.Statements.Add (new CodeMethodReturnStatement (findViewCall));

				return method;
			}

			var findByIdRef = new CodeMethodReferenceExpression (
				parentViewRef,
				"FindViewById",
				new [] { tReference }
			);

			var findByIdInvoke = new CodeMethodInvokeExpression (findByIdRef, new [] { resourceIdVarRef });
			var viewVar = new CodeVariableDeclarationStatement (tReference, "view", findByIdInvoke);
			method.Statements.Add (viewVar);

			// if (view == null) {
			//     OnLayoutViewNotFound (resourceId, ref view);
			// }
			// if (view != null)
			//     return view;
			// throw new System.InvalidOperationException($"View not found (ID: {resourceId})");

			var viewVarRef = new CodeVariableReferenceExpression ("view");
			var ifViewNotNull = new CodeConditionStatement (
				new CodeBinaryOperatorExpression (viewVarRef, CodeBinaryOperatorType.IdentityInequality, new CodePrimitiveExpression (null)),
				new CodeStatement [] { new CodeMethodReturnStatement (viewVarRef) }
			);

			var viewRefParam = new CodeDirectionExpression (FieldDirection.Ref, viewVarRef);
			var viewNotFoundInvoke = new CodeMethodInvokeExpression (
				new CodeThisReferenceExpression (),
				"OnLayoutViewNotFound",
				new CodeExpression [] { resourceIdVarRef, viewRefParam }
			);

			var ifViewNull = new CodeConditionStatement (
				new CodeBinaryOperatorExpression (viewVarRef, CodeBinaryOperatorType.IdentityEquality, new CodePrimitiveExpression (null)),
				new CodeStatement [] { new CodeExpressionStatement (viewNotFoundInvoke) }
			);

			method.Statements.Add (ifViewNull);
			method.Statements.Add (ifViewNotNull);

			var throwInvOp = new CodeThrowExceptionStatement (
				new CodeObjectCreateExpression (
					new CodeTypeReference (typeof (InvalidOperationException), CodeTypeReferenceOptions.GlobalReference),
					new [] { new CodeSnippetExpression ("$\"View not found (ID: {resourceId})\"") }
				)
			);

			method.Statements.Add (throwInvOp);

			return method;
		}

		CodeMemberMethod ImplementFindFragment (CodeTypeReference typeForParent, CodeTypeReference typeForOverloadCall = null, Func<CodeVariableReferenceExpression, CodeExpression> constructParentViewCall = null)
		{
			CodeMemberMethod method = CreateMethod ("__FindFragment", MethodAccessibility.Private, MethodScope.Final);

			var typeParam = new CodeTypeParameter ("T") {
				Constraints = {
					new CodeTypeReference ("Android.App.Fragment", CodeTypeReferenceOptions.GlobalReference),
				},
			};

			method.TypeParameters.Add (typeParam);
			method.Parameters.Add (new CodeParameterDeclarationExpression (typeForParent, "activity"));
			method.Parameters.Add (new CodeParameterDeclarationExpression (typeof (int), "id"));

			var tReference = new CodeTypeReference (typeParam);
			method.ReturnType = tReference;

			// T fragment = FragmentManager.FindFragmentById<T> (id);
			var id = new CodeVariableReferenceExpression ("id");

			var findByIdRef = new CodeMethodReferenceExpression (
					new CodePropertyReferenceExpression (new CodeVariableReferenceExpression ("activity"), "FragmentManager"),
					"FindFragmentById",
					new[] { tReference }
				);

			var findByIdInvoke = new CodeMethodInvokeExpression (findByIdRef, new[] { id });
			var viewVar = new CodeVariableDeclarationStatement (tReference, "fragment", findByIdInvoke);
			method.Statements.Add (viewVar);

			// if (view == null) {
			//     OnLayoutFragmentNotFound (resourceId, ref view);
			// }
			// if (view != null)
			//     return view;
			// throw new System.InvalidOperationException($"Fragment not found (ID: {id})");

			var viewVarRef = new CodeVariableReferenceExpression ("fragment");
			var ifViewNotNull = new CodeConditionStatement (
					new CodeBinaryOperatorExpression (viewVarRef, CodeBinaryOperatorType.IdentityInequality, new CodePrimitiveExpression (null)),
					new CodeStatement[] { new CodeMethodReturnStatement (viewVarRef) }
				);

			var viewRefParam = new CodeDirectionExpression (FieldDirection.Ref, viewVarRef);
			var viewNotFoundInvoke = new CodeMethodInvokeExpression (
					new CodeThisReferenceExpression (),
					"OnLayoutFragmentNotFound",
					new CodeExpression[] { id, viewRefParam }
				);

			var ifViewNull = new CodeConditionStatement (
					new CodeBinaryOperatorExpression (viewVarRef, CodeBinaryOperatorType.IdentityEquality, new CodePrimitiveExpression (null)),
					new CodeStatement[] { new CodeExpressionStatement (viewNotFoundInvoke) }
				);

			method.Statements.Add (ifViewNull);
			method.Statements.Add (ifViewNotNull);

			var throwInvOp = new CodeThrowExceptionStatement (
					new CodeObjectCreateExpression (
							new CodeTypeReference (typeof (InvalidOperationException), CodeTypeReferenceOptions.GlobalReference),
							new[] { new CodeSnippetExpression ("$\"Fragment not found (ID: {id})\"") }
						)
				);

			method.Statements.Add (throwInvOp);

			return method;
		}

		CodeMemberMethod CreateMethod (string methodName, MethodAccessibility access, MethodScope scope)
		{
			return CreateMethod (methodName, access, scope, (CodeTypeReference)null);
		}

		CodeMemberMethod CreateMethod (string methodName, MethodAccessibility access, MethodScope scope, Type returnType)
		{
			return CreateMethod (methodName, access, scope, new CodeTypeReference (returnType));
		}

		CodeMemberMethod CreateMethod (string methodName, MethodAccessibility access, MethodScope scope, string returnType)
		{
			return CreateMethod (methodName, access, scope, new CodeTypeReference (returnType));
		}

		CodeMemberMethod CreateMethod (string methodName, MethodAccessibility access, MethodScope scope, CodeTypeReference returnType)
		{
			var ret = new CodeMemberMethod {
				Name = methodName,
			};
			if (returnType != null)
				ret.ReturnType = returnType;

			MemberAttributes attrs;
			switch (access) {
			case MethodAccessibility.Internal:
				attrs = MemberAttributes.FamilyAndAssembly;
				break;

			case MethodAccessibility.Private:
				attrs = MemberAttributes.Private;
				break;

			case MethodAccessibility.Protected:
				attrs = MemberAttributes.Family;
				break;

			case MethodAccessibility.Public:
				attrs = MemberAttributes.Public;
				break;

			default:
				throw new NotSupportedException ($"Method accessibility {access} is not supported");
			}

			if ((scope & MethodScope.Static) == MethodScope.Static) {
				attrs |= MemberAttributes.Static | MemberAttributes.Final;
			} else if ((scope & MethodScope.Abstract) == MethodScope.Abstract) {
				attrs |= MemberAttributes.Abstract;
			} else {
				if ((scope & MethodScope.Override) == MethodScope.Override) {
					attrs |= MemberAttributes.Override;
				} else if ((scope & MethodScope.Virtual) == MethodScope.Virtual) {
				} else {
					attrs |= MemberAttributes.Final;
				}
			}

			ret.Attributes = attrs;
			return ret;
		}

		void AddComment (CodeCommentStatementCollection comments, string comment)
		{
			comments.Add (new CodeCommentStatement (comment));
		}

		void MarkAsCompilerGenerated (CodeTypeMember member)
		{
			AddCustomAttribute (member.CustomAttributes, typeof (CompilerGeneratedAttribute));
		}

		void AddCustomAttribute (CodeAttributeDeclarationCollection attributes, Type type)
		{
			attributes.Add (new CodeAttributeDeclaration (new CodeTypeReference (type, CodeTypeReferenceOptions.GlobalReference)));
		}
	}
}
