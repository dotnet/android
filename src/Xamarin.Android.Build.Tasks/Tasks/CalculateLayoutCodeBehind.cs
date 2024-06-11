using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.XPath;

using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;

using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class CalculateLayoutCodeBehind : AndroidAsyncTask
	{
		public override string TaskPrefix => "CLC";

		sealed class LayoutInclude
		{
			public string Id;
			public string Name;
		}

		sealed class LayoutGroup
		{
			public List<ITaskItem> InputItems;
			public List<ITaskItem> LayoutBindingItems;
			public List<ITaskItem> LayoutPartialClassItems;
		}

		static readonly char[] partialClassNameSplitChars = { ';' };

		public const int ParallelGenerationThreshold = 20; // Minimum number of ResourceFiles to trigger
								   // parallel generation of layouts

		public const string LayoutBindingFileNameMetadata = "LayoutBindingFileName";
		public const string ClassNameMetadata = "ClassName";
		public const string LayoutGroupMetadata = "LayoutGroup";
		public const string WidgetCollectionKeyMetadata = "WidgetCollectionKey";
		public const string LayoutPartialClassFileNameMetadata = "LayoutPartialClassFileName";
		public const string PartialClassNamesMetadata = "PartialClassNames";
		public const string PartialCodeBehindClassNameMetadata = "PartialCodeBehindClassName";
		public const string GlobalIdPrefix = "global@";

		const string DefaultAndroidNamespace = "http://schemas.android.com/apk/res/android";
		const string DefaultXamarinNamespace = "http://schemas.xamarin.com/android/xamarin/tools";
		const string XmlNamespaceUri = "http://www.w3.org/2000/xmlns/";
		const string AndroidNamespace = "android";
		const string XamarinNamespace = "xamarin";
		const string XamarinClassesAttribute = "classes";
		const string XamarinManagedTypeAttribute = "managedType";

		readonly char[] LayoutFilePathSplit = new[] { ';' };
		readonly Dictionary <string, string> knownNamespaceFixups = new Dictionary <string, string> (StringComparer.OrdinalIgnoreCase) {
			{"android.view", "Android.Views"},
			{"android.support.wearable.view", "Android.Support.Wearable.Views"},
			{"android.support.constraint", "Android.Support.Constraints"},
			{"com.actionbarsherlock", "ABSherlock"},
			{"com.actionbarsherlock.widget", "ABSherlock.Widget"},
			{"com.actionbarsherlock.view", "ABSherlock.View"},
			{"com.actionbarsherlock.app", "ABSherlock.App"},

		};
		readonly Dictionary <string, string> knownTypeNameFixups = new Dictionary <string, string> (StringComparer.OrdinalIgnoreCase) {
			{"WebView", "Android.Webkit.WebView"},
		};
		readonly List <string> knownNamespacePrefixes = new List <string> {
			"com.google.",
		};

		XPathExpression widgetWithId;
		string sourceFileExtension;

		public string BindingDependenciesCacheFile { get; set; }

		[Required]
		public string BaseNamespace { get; set; }

		[Required]
		public string OutputFileExtension { get; set; }

		[Required]
		public string OutputLanguage { get; set; }

		[Required]
		public ITaskItem [] BoundLayouts { get; set; }

		[Output]
		public ITaskItem [] LayoutBindingFiles { get; set; }

		[Output]
		public ITaskItem [] LayoutPartialClassFiles { get; set; }

		public async override System.Threading.Tasks.Task RunTaskAsync ()
		{
			widgetWithId = XPathExpression.Compile ("//*[@android:id and string-length(@android:id) != 0] | //include[not(@android:id)]");

			GenerateLayoutBindings.BindingGeneratorLanguage gen;
			if (!GenerateLayoutBindings.KnownBindingGenerators.TryGetValue (OutputLanguage, out gen) || gen == null) {
				LogDebugMessage ($"Language {OutputLanguage} isn't supported, will use {GenerateLayoutBindings.DefaultOutputGenerator.Name} instead");
				sourceFileExtension = GenerateLayoutBindings.DefaultOutputGenerator.Extension;
			} else
				sourceFileExtension = OutputFileExtension;

			var layoutsByName = new Dictionary <string, LayoutGroup> (StringComparer.OrdinalIgnoreCase);

			foreach (ITaskItem item in BoundLayouts) {
				if (item == null)
					continue;

				AddLayoutFile (item, ref layoutsByName);
			}

			var layoutBindingFiles = new List<ITaskItem> ();
			var layoutPartialClassFiles = new List<ITaskItem> ();
			if (layoutsByName.Count >= ParallelGenerationThreshold) {
				// NOTE: Update the tests in $TOP_DIR/tests/CodeBehind/UnitTests/BuildTests.cs if this message
				// is changed!
				LogDebugMessage ($"Parsing layouts in parallel (threshold of {ParallelGenerationThreshold} layouts met)");

				await this.WhenAll (layoutsByName, kvp =>
					ParseAndLoadGroup (layoutsByName, kvp.Key, kvp.Value.InputItems, ref kvp.Value.LayoutBindingItems, ref kvp.Value.LayoutPartialClassItems));

				foreach (var kvp in layoutsByName) {
					LayoutGroup group = kvp.Value;
					if (group == null)
						continue;
					if (group.LayoutBindingItems != null && group.LayoutBindingItems.Count > 0)
						layoutBindingFiles.AddRange (group.LayoutBindingItems);
					if (group.LayoutPartialClassItems != null && group.LayoutPartialClassItems.Count > 0)
						layoutPartialClassFiles.AddRange (group.LayoutPartialClassItems);
				}
			} else {
				foreach (var kvp in layoutsByName) {
					ParseAndLoadGroup (layoutsByName, kvp.Key, kvp.Value.InputItems, ref layoutBindingFiles, ref layoutPartialClassFiles);
				}
			}

			LayoutBindingFiles = layoutBindingFiles.ToArray ();
			if (LayoutBindingFiles.Length == 0)
				LogDebugMessage ("  No layout file qualifies for code-behind generation");
			LayoutPartialClassFiles = layoutPartialClassFiles.ToArray ();

			LogDebugTaskItems ("  LayoutBindingFiles:", LayoutBindingFiles);
			LogDebugTaskItems ("  LayoutPartialClassFiles:", LayoutPartialClassFiles);
		}

		void ParseAndLoadGroup (Dictionary <string, LayoutGroup> groupIndex, string groupName, List<ITaskItem> items, ref List<ITaskItem> layoutBindingFiles, ref List<ITaskItem> layoutPartialClassFiles)
		{
			IDictionary<string, LayoutWidget> widgets = new Dictionary <string, LayoutWidget> (StringComparer.Ordinal);
			if (!LoadLayoutGroup (groupIndex, items, ref widgets))
				return;

			CreateCodeBehindTaskItems (groupName, items, widgets.Values, ref layoutBindingFiles, ref layoutPartialClassFiles);
		}

		bool LoadLayoutGroup (Dictionary <string, LayoutGroup> groupIndex, List<ITaskItem> items, ref IDictionary<string, LayoutWidget> widgets, string rootWidgetIdOverride = null)
		{
			bool ret = true;
			foreach (ITaskItem item in items) {
				if (!LoadLayout (item.ItemSpec, groupIndex, ref widgets, rootWidgetIdOverride))
					ret = false;
			}

			return ret;
		}

		bool LoadLayout (string filePath, Dictionary <string, LayoutGroup> groupIndex, ref IDictionary <string, LayoutWidget> widgets, string rootWidgetIdOverride = null)
		{
			var doc = new XPathDocument (filePath);
			var nav = doc.CreateNavigator ();

			var nsmgr = new XmlNamespaceManager (nav.NameTable);
			string androidNS = SetNamespace (nav, nsmgr, AndroidNamespace, DefaultAndroidNamespace);
			string xamarinNS = SetNamespace (nav, nsmgr, XamarinNamespace, DefaultXamarinNamespace);
			string id;
			string parsedId;
			string name;
			bool skipFirst = false;

			nav.MoveToFirstChild ();

			// This is needed in case the first element after XML declaration is not an actual element but a
			// comment, for instance
			while (nav.NodeType != XPathNodeType.Element) {
				nav.MoveToNext ();
			}

			string xamarinClasses = nav.GetAttribute (XamarinClassesAttribute, xamarinNS)?.Trim ();

			if (!String.IsNullOrWhiteSpace (rootWidgetIdOverride)) {
				if (!ParseIdWithError (nav, filePath, rootWidgetIdOverride, true, out parsedId, out name))
					LogCodedError ("XA1012", Properties.Resources.XA1012, rootWidgetIdOverride);
				else {
					skipFirst = true;
					CreateWidget (nav, filePath, androidNS, xamarinNS, rootWidgetIdOverride, parsedId, name, xamarinClasses, ref widgets);
				}
			}

			widgetWithId.SetContext (nsmgr);
			XPathNodeIterator nodes = nav.Select (widgetWithId);
			List<LayoutInclude> includes = null;
			if (nodes.Count == 0)
				return true;

			bool errors = false;
			while (nodes.MoveNext ()) {
				if (skipFirst)
					continue;

				XPathNavigator current = nodes.Current;

				// <merge> anywhere is ignored - Android always returns 'null' if you try to find such
				// an element. Prevents https://github.com/xamarin/xamarin-android/issues/1929
				if (String.Compare ("merge", current.LocalName, StringComparison.Ordinal) == 0)
					continue;

				bool isInclude = String.Compare ("include", current.LocalName, StringComparison.Ordinal) == 0;

				if (!GetAndParseId (current, filePath, androidNS, isInclude, out id, out parsedId, out name)  && !isInclude) {
					errors = true;
					continue;
				}

				if (isInclude) {
					string layoutName = GetLayoutNameFromReference (current.GetAttribute ("layout", String.Empty))?.Trim ();
					if (!String.IsNullOrEmpty (layoutName))
						AddToList (new LayoutInclude { Id = id, Name = layoutName }, ref includes);
					continue;
				}

				CreateWidget (current, filePath, androidNS, xamarinNS, id, parsedId, name, xamarinClasses, ref widgets);
			}

			if (includes == null || includes.Count == 0)
				return !errors;

			foreach (LayoutInclude include in includes) {
				if (include == null)
					continue;

				LayoutGroup includedGroup;
				if (!groupIndex.TryGetValue (include.Name, out includedGroup) || includedGroup == null || includedGroup.InputItems == null || includedGroup.InputItems.Count == 0)
					continue;

				if (!LoadLayoutGroup (groupIndex, includedGroup.InputItems, ref widgets, include.Id))
					errors = true;
			}

			return !errors;
		}

		void CreateWidget (XPathNavigator current, string filePath, string androidNS, string xamarinNS, string id, string parsedId, string name, string partialClasses, ref IDictionary <string, LayoutWidget> widgets)
		{
			bool isFragment = String.Compare ("fragment", current.LocalName, StringComparison.Ordinal) == 0;
			string managedType = current.GetAttribute (XamarinManagedTypeAttribute, xamarinNS);
			string oldType = null;

			if (String.IsNullOrEmpty (managedType)) {
				bool mayNeedTypeFixup = true;
				if (isFragment) {
					managedType = current.GetAttribute ("name", androidNS)?.Trim ();
					if (String.IsNullOrEmpty (managedType)) {
						mayNeedTypeFixup = false;
						managedType = "global::Android.App.Fragment";
					}
				} else
					managedType = current.LocalName;

				if (mayNeedTypeFixup)
					mayNeedTypeFixup = !FixUpTypeName (ref managedType);

				int idx = managedType.IndexOf (',');
				if (idx >= 0)
					managedType = managedType.Substring (0, idx).Trim ();

				if (mayNeedTypeFixup && (idx = managedType.LastIndexOf ('.')) >= 0) {
					LogCodedWarning ("XA1005", Properties.Resources.XA1005, id, managedType);
					LogCodedWarning ("XA1005", Properties.Resources.XA1005_Instructions);

					oldType = managedType;
					string ns = managedType.Substring (0, idx);
					string klass = managedType.Substring (idx + 1);
					string fixedNS = null;
					if (FixUpNamespace (ns, out fixedNS)) {
						LogMessage ($"Fixed up a known namespace from '{ns}' to '{fixedNS}'");
						managedType = $"{fixedNS}.{klass}";
					} else {
						LogMessage ("Fixed up namespace by naive capitalization of the name");
						managedType = $"{CapitalizeName (ns)}.{klass}";
					}
					LogMessage ($"Element with ID '{id}' managed type fixed up to: '{managedType}'");
				}
			}

			LayoutWidget widget;
			bool fresh = false;
			if (!widgets.TryGetValue (parsedId, out widget) || widget == null) {
				fresh = true;
				widget = new LayoutWidget {
					Id = parsedId,
					Type = managedType,
					Name = name,
					PartialClasses = partialClasses,
					AllTypes = new List<LayoutWidgetType> (),
					Locations = new List<LayoutLocationInfo> (),
					WidgetType = isFragment ? LayoutWidgetType.Fragment : LayoutWidgetType.View,
				};
				widgets [widget.Id] = widget;
			}

			LayoutLocationInfo location = GetLocationInfo (current, filePath);
			widget.AllTypes.Add (widget.WidgetType);
			widget.Locations.Add (location);
			if (oldType != null) {
				if (widget.TypeFixups == null)
					widget.TypeFixups = new List<LayoutTypeFixup> ();
				widget.TypeFixups.Add (new LayoutTypeFixup { OldType = oldType, Location = location });
			}

			if (fresh)
				return;

			if (widget.Type != null && String.Compare (widget.Type, managedType, StringComparison.Ordinal) == 0)
				return;

			widget.Type = null;
			widget.WidgetType = LayoutWidgetType.Unknown;
			widget.AllTypes.Add (isFragment ? LayoutWidgetType.Fragment : LayoutWidgetType.View);
		}

		void AddToList <T> (T item, ref List<T> list)
		{
			if (list == null)
				list = new List<T> ();
			list.Add (item);
		}

		string GetLayoutNameFromReference (string reference)
		{
			string id = reference?.Trim ();
			if (String.IsNullOrEmpty (reference))
				return null;
			if (id.StartsWith ("@layout/", StringComparison.Ordinal))
			    return id.Substring (8);
			return null;
		}

		string GetId (XPathNavigator navigator, string androidNS)
		{
			return navigator.GetAttribute ("id", androidNS);
		}

		bool GetAndParseId (XPathNavigator navigator, string filePath, string androidNS, bool ignoreMissing, out string rawId, out string parsedId, out string name)
		{
			rawId = GetId (navigator, androidNS);
			return ParseIdWithError (navigator, filePath, rawId, ignoreMissing, out parsedId, out name);
		}

		bool ParseIdWithError (XPathNavigator navigator, string filePath, string rawId, bool ignoreMissing, out string parsedId, out string name)
		{
			if (!ParseID (rawId, out parsedId, out name)) {
				if (!ignoreMissing)
					LogCodedError ("XA1013", Properties.Resources.XA1013, navigator.Name, filePath);
				return false;
			}

			return true;
		}

		// This should be done in a different manner. Instead of hardcoding the namespaces here we should have
		// something that would let us pass the mappings to the task.
		bool FixUpNamespace (string ns, out string fixedNS)
		{
			if (knownNamespaceFixups.TryGetValue (ns, out fixedNS))
				return true;

			string newNS = null;
			foreach (string prefix in knownNamespacePrefixes) {
				if (RemoveNSPrefix (prefix, ns, ref newNS)) {
					fixedNS = newNS;
					return true;
				}
			}

			return false;
		}

		bool RemoveNSPrefix (string prefix, string fullNS, ref string fixedNS)
		{
			if (fullNS.StartsWith (prefix, StringComparison.OrdinalIgnoreCase)) {
				fixedNS = CapitalizeName (fullNS.Substring (prefix.Length));
				return true;
			}

			return false;
		}

		bool FixUpTypeName (ref string typeName)
		{
			string newType;
			if (knownTypeNameFixups.TryGetValue (typeName, out newType)) {
				typeName = newType;
				return true;
			}

			return false;
		}

		string CapitalizeName (string name)
		{
			var parts = new List <string> ();
			foreach (string p in name.Split ('.')) {
				// Since it's quite common...
				if (p.Length == 2)
					parts.Add (p.ToUpper ());
				else
					parts.Add ($"{Char.ToUpper (p[0])}{p.Substring (1)}");
			}
			return String.Join (".", parts);
		}

		LayoutLocationInfo GetLocationInfo (XPathNavigator nav, string filePath)
		{
			var lineInfo = nav as IXmlLineInfo;
			var ret = new LayoutLocationInfo {
				FilePath = filePath
			};
			if (lineInfo != null) {
				ret.Line = lineInfo.LineNumber;
				ret.Column = lineInfo.LinePosition;
			} else {
				ret.Line = 0;
				ret.Column = 0;
			}

			return ret;
		}

		string SetNamespace (XPathNavigator nav, XmlNamespaceManager nsmgr, string nsName, string defaultValue)
		{
			string nsValue = nav.GetAttribute (nsName, XmlNamespaceUri);
			if (String.IsNullOrEmpty (nsValue))
				nsValue = defaultValue;

			nsmgr.AddNamespace (nsName, nsValue);
			return nsValue;
		}

		bool ParseID (string id, out string parsedId, out string name)
		{
			parsedId = null;
			name = null;
			id = id?.Trim ();
			if (String.IsNullOrEmpty (id))
				return true;

			string ns;
			bool capitalize = false;
			if (id.StartsWith ("@id/", StringComparison.Ordinal) || id.StartsWith ("@+id/", StringComparison.Ordinal))
				ns = "Resource.Id";
			else if (id.StartsWith ("@android:id/", StringComparison.Ordinal)) {
				ns = $"{GlobalIdPrefix}Android.Resource.Id";
				capitalize = true;
			} else
				return false;

			var sb = new StringBuilder (id.Substring (id.IndexOf ('/') + 1));
			if (capitalize)
				sb [0] = Char.ToUpper (sb [0]);

			name = sb.ToString ();
			parsedId = $"{ns}.{name}";
			return true;
		}

		void CreateCodeBehindTaskItems (string groupName, List <ITaskItem> layoutItems, ICollection<LayoutWidget> widgets, ref List<ITaskItem> layoutBindingFiles, ref List<ITaskItem> layoutPartialClassFiles)
		{
			if (layoutItems == null || layoutItems.Count == 0)
				return;

			if (layoutBindingFiles == null)
				layoutBindingFiles = new List<ITaskItem> ();

			string className = $"{BaseNamespace}.{groupName}";
			string collectionKey = RegisterGroupWidgets (widgets);
			string partialClasses = widgets.FirstOrDefault (w => w != null && !String.IsNullOrEmpty (w.PartialClasses))?.PartialClasses;
			bool havePartialClasses = !String.IsNullOrEmpty (partialClasses);

			string[] partialClassNames = null;
			if (havePartialClasses) {
				if (layoutPartialClassFiles == null)
					layoutPartialClassFiles = new List<ITaskItem> ();
				partialClassNames = partialClasses.Split (partialClassNameSplitChars, StringSplitOptions.RemoveEmptyEntries);
			}

			foreach (ITaskItem item in layoutItems) {
				var layoutItem = new TaskItem (item.ItemSpec);
				layoutItem.SetMetadata (LayoutBindingFileNameMetadata, $"{className}.g{sourceFileExtension}");
				layoutItem.SetMetadata (ClassNameMetadata, className);
				layoutItem.SetMetadata (LayoutGroupMetadata, groupName);
				layoutItem.SetMetadata (WidgetCollectionKeyMetadata, collectionKey);
				if (havePartialClasses) {
					layoutItem.SetMetadata (PartialClassNamesMetadata, partialClasses);

					foreach (string partialClassName in partialClassNames) {
						var partialClassItem = new TaskItem (item.ItemSpec);
						partialClassItem.SetMetadata (LayoutPartialClassFileNameMetadata, $"{partialClassName}.{groupName}.g{sourceFileExtension}");
						partialClassItem.SetMetadata (ClassNameMetadata, className);
						partialClassItem.SetMetadata (LayoutGroupMetadata, groupName);
						partialClassItem.SetMetadata (PartialCodeBehindClassNameMetadata, partialClassName);

						layoutPartialClassFiles.Add (partialClassItem);
					}
				}

				layoutBindingFiles.Add (layoutItem);
			}
		}

		string RegisterGroupWidgets (ICollection<LayoutWidget> widgets)
		{
			string key = Guid.NewGuid ().ToString ();
			LogDebugMessage ($"Registering {widgets?.Count} widgets for key {key}");
			BuildEngine4.RegisterTaskObjectAssemblyLocal (ProjectSpecificTaskObjectKey (key), widgets, RegisteredTaskObjectLifetime.Build, allowEarlyCollection: false);
			return key;
		}

		void CopyMetadataIfFound (string name, ITaskItem fromItem, ITaskItem toItem)
		{
			string meta = fromItem.GetMetadata (name);
			if (String.IsNullOrEmpty (meta))
				return;
			toItem.SetMetadata (name, meta);
		}

		void AddLayoutFile (ITaskItem item, ref Dictionary <string, LayoutGroup> layoutsByName)
		{
			string filePath = item.ItemSpec;
			if (String.IsNullOrEmpty (filePath) || !File.Exists(filePath))
				return;

			string groupName = Path.GetFileNameWithoutExtension (filePath);
			LayoutGroup group;
			if (!layoutsByName.TryGetValue (groupName, out group) || group == null) {
				group = new LayoutGroup {
					InputItems = new List<ITaskItem> ()
				};
				layoutsByName [groupName] = group;
			}

			group.InputItems.Add (item);
		}
	}
}
