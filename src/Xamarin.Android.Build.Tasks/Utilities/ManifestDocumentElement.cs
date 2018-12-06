using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using Mono.Cecil;

using Monodroid;
using MonoDroid.Tuner;
using MonoDroid.Utils;

using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.TypeNameMappings;

using Android.App;
using Android.Content.PM;
using Android.Views;

using Xamarin.Android.Tasks;

namespace Xamarin.Android.Manifest {

	class ManifestDocumentElement {

		public static string ToString (TypeDefinition typeDef)
		{
			return JavaNativeTypeManager.ToJniName (typeDef).Replace ('/', '.');
		}

		public static TypeDefinition ResolveType (string type, ICustomAttributeProvider provider, IAssemblyResolver resolver)
		{
			if (provider == null)
				throw new ArgumentException ("Type resolution support requires an AssemblyDefinition or TypeDefinition.", "provider");
			if (resolver == null)
				throw new ArgumentException ("Type resolution support requires a IAssemblyResolver.", "resolver");

			// `type` is either a "bare" type "Foo.Bar", or an
			// assembly-qualified type "Foo.Bar, AssemblyName [Version=...]?".
			//
			// Bare types are looked up via `provider`; assembly-qualified types are
			// looked up via `resolver`

			int c = type.IndexOf (',');
			string typeName = c < 0 ? type  : type.Substring (0, c);

			string assmName = c < 0 ? null  : type.Substring (c+1);

			var assmNameRef = AssemblyNameReference.Parse (assmName);
			var assembly    = assmName == null ? null : resolver.Resolve (assmNameRef);
			if (assembly == null) {
				assembly = provider as AssemblyDefinition;
				if (assembly == null) {
					TypeDefinition decl = (TypeDefinition) provider;
					assembly = decl.Module.Assembly;
				}
			}
			var ret = assembly.Modules.Cast<ModuleDefinition> ()
				.Select (md => md.Types.FirstOrDefault (t => t.FullName == typeName))
				.FirstOrDefault (td => td != null);
			if (ret == null)
				throw new ArgumentException ("Type not found: " + type, "type");

			return ret;
		}
	}

	class ManifestDocumentElement<T> : ManifestDocumentElement, IEnumerable<string> {

		public ManifestDocumentElement (string element)
		{
			Element = element;
		}

		public readonly string Element;

		class MappingInfo {
			public string             AttributeName;
			public Func<T, object>    Getter;
			public Action<T, object>  Setter;
			public Type               MemberType;
			public Func<T, string>    AttributeValue;
			public Func<T, ICustomAttributeProvider, IAssemblyResolver, string>    AttributeValue2;
		}

		readonly IDictionary<string, MappingInfo>   Mappings = new Dictionary<string, MappingInfo> ();

		public void Add (string member, string attributeName, Func<T, object> getter, Action<T, object> setter, Type memberType = null)
		{
			Mappings.Add (member, new MappingInfo {
					AttributeName   = attributeName,
					Getter          = getter,
					Setter          = setter,
					MemberType      = memberType,
			});
		}

		public void Add (string member, string attributeName, Action<T, object> setter, Func<T, string> attributeValue)
		{
			Mappings.Add (member, new MappingInfo {
					AttributeName   = attributeName,
					Setter          = setter,
					AttributeValue  = attributeValue,
			});
		}

		public void Add (string member, string attributeName, Action<T, object> setter, Func<T, ICustomAttributeProvider, IAssemblyResolver, string> attributeValue)
		{
			Mappings.Add (member, new MappingInfo {
					AttributeName   = attributeName,
					Setter          = setter,
					AttributeValue2 = attributeValue,
			});
		}

		public ICollection<string> Load (T value, CustomAttribute attribute)
		{
			if (attribute == null)
				return null;

			var specified = new HashSet<string> ();

			foreach (var e in attribute.Properties) {
				specified.Add (e.Name);
				var s = Mappings [e.Name].Setter;
				if (s != null)
					s (value, e.Argument.GetSettableValue ());
			}

			return specified;
		}

		public XElement ToElement (T value, ICollection<string> specified, string packageName,
			ICustomAttributeProvider provider = null, IAssemblyResolver resolver = null, int targetSdkVersion = 0)
		{
			var r = new XElement (Element,
					specified.OrderBy (e => e)
					.Select (e => ToAttribute (e, value, packageName, provider, resolver, targetSdkVersion))
					.Where (a => a != null));
			AndroidResource.UpdateXmlResource (r);
			return r;
		}

		XAttribute ToAttribute (string name, T value, string packageName,
			ICustomAttributeProvider provider, IAssemblyResolver resolver, int targetSdkVersion = 0)
		{
			if (!Mappings.ContainsKey (name))
				throw new ArgumentException ("Invalid attribute name: " + name);
			var m = Mappings [name];
			if (m.AttributeName == null)
				return null;

			string v = ToAttributeValue (name, value, provider, resolver,targetSdkVersion);
			if (v == null)
				return null;
			v = v.Replace ("@PACKAGE_NAME@", packageName);
			return new XAttribute (ManifestDocument.AndroidXmlNamespace + m.AttributeName, v);
		}

		string ToAttributeValue (string name, T value, ICustomAttributeProvider provider, IAssemblyResolver resolver, int targetSdkVersion = 0)
		{
			var m = Mappings [name];
			if (m.AttributeValue != null)
				return m.AttributeValue (value);
			if (m.AttributeValue2 != null)
				return m.AttributeValue2 (value, provider, resolver);

			if (m.Getter == null)
				return null;

			var v = m.Getter (value);
			if (v == null)
				return null;

			var t = m.MemberType ?? v.GetType ();
			var c = ValueConverters [t];
			return c (v, provider, resolver, targetSdkVersion);
		}

		static readonly Dictionary<Type, Func<object, ICustomAttributeProvider, IAssemblyResolver, int, string>> ValueConverters = new Dictionary<Type, Func<object, ICustomAttributeProvider, IAssemblyResolver, int, string>> () {
			{ typeof (bool),                (value, p, r, v) => ToString ((bool) value) },
			{ typeof (int),                 (value, p, r, v) => value.ToString () },
			{ typeof (float),               (value, p, r, v) => value.ToString () },
			{ typeof (string),              (value, p, r, v) => value.ToString () },
			{ typeof (ActivityPersistableMode),     (value, p, r, v) => ToString ((ActivityPersistableMode) value) },
			{ typeof (ConfigChanges),       (value, p, r, v) => ToString ((ConfigChanges) value) },
			{ typeof (DocumentLaunchMode),  (value, p, r, v) => ToString ((DocumentLaunchMode) value) },
			{ typeof (LaunchMode),          (value, p, r, v) => ToString ((LaunchMode) value) },
			{ typeof (Protection),          (value, p, r, v) => ToString ((Protection) value) },
			{ typeof (ScreenOrientation),   (value, p, r, v) => ToString ((ScreenOrientation) value, v) },
			{ typeof (SoftInput),           (value, p, r, v) => ToString ((SoftInput) value) },
			{ typeof (UiOptions),           (value, p, r, v) => ToString ((UiOptions) value) },
			{ typeof (Type),                (value, p, r, v) => ToString (value.ToString (), p, r) },
			{ typeof (WindowRotationAnimation),     (value, p, r, v) => ToString ((WindowRotationAnimation) value) },
		};

		static string ToString (bool value)
		{
			return value ? "true" : "false";
		}

		static string ToString (ActivityPersistableMode value)
		{
			switch (value) {
				case ActivityPersistableMode.AcrossReboots:   return "persistAcrossReboots";
				case ActivityPersistableMode.Never:           return "persistNever";
				case ActivityPersistableMode.RootOnly:        return "persistRootOnly";
				default:
					throw new ArgumentException ($"Unsupported ActivityPersistableMode value '{value}'.", "ActivityPersistableMode");
			}
		}

		static string ToString (ConfigChanges value)
		{
			var values = new List<string> ();
			if ((value & ConfigChanges.Density) == ConfigChanges.Density)
				values.Add ("density");
			if ((value & ConfigChanges.FontScale) == ConfigChanges.FontScale)
				values.Add ("fontScale");
			if ((value & ConfigChanges.Keyboard) == ConfigChanges.Keyboard)
				values.Add ("keyboard");
			if ((value & ConfigChanges.KeyboardHidden) == ConfigChanges.KeyboardHidden)
				values.Add ("keyboardHidden");
			if ((value & ConfigChanges.LayoutDirection) == ConfigChanges.LayoutDirection)
				values.Add ("layoutDirection");
			if ((value & ConfigChanges.Locale) == ConfigChanges.Locale)
				values.Add ("locale");
			if ((value & ConfigChanges.Mcc) == ConfigChanges.Mcc)
				values.Add ("mcc");
			if ((value & ConfigChanges.Mnc) == ConfigChanges.Mnc)
				values.Add ("mnc");
			if ((value & ConfigChanges.Navigation) == ConfigChanges.Navigation)
				values.Add ("navigation");
			if ((value & ConfigChanges.Orientation) == ConfigChanges.Orientation)
				values.Add ("orientation");
			if ((value & ConfigChanges.SmallestScreenSize) == ConfigChanges.SmallestScreenSize)
				values.Add ("smallestScreenSize");
			if ((value & ConfigChanges.ScreenLayout) == ConfigChanges.ScreenLayout)
				values.Add ("screenLayout");
			if ((value & ConfigChanges.ScreenSize) == ConfigChanges.ScreenSize)
				values.Add ("screenSize");
			if ((value & ConfigChanges.Touchscreen) == ConfigChanges.Touchscreen)
				values.Add ("touchscreen");
			if ((value & ConfigChanges.UiMode) == ConfigChanges.UiMode)
				values.Add ("uiMode");

			return string.Join ("|", values.ToArray ());
		}

		static string ToString (DocumentLaunchMode value)
		{
			switch (value) {
				case DocumentLaunchMode.Always:          return "always";
				case DocumentLaunchMode.IntoExisting:    return "intoExisting";
				case DocumentLaunchMode.Never:           return "never";
				case DocumentLaunchMode.None:            return "none";
				default:
					throw new ArgumentException ($"Unsupported DocumentLaunchMode value '{value}'.", "DocumentLaunchMode");
			}
		}

		static string ToString (LaunchMode value)
		{
			switch (value) {
				case LaunchMode.Multiple:         return "standard";
				case LaunchMode.SingleInstance:   return "singleInstance";
				case LaunchMode.SingleTask:       return "singleTask";
				case LaunchMode.SingleTop:        return "singleTop";
				default:
					throw new ArgumentException ("Unsupported LaunchMode value '" + value + "'.", "LaunchMode");
			}
		}

		static string ToString (Protection value)
		{
			value = value & Protection.MaskBase;
			switch (value) {
				case Protection.Dangerous:          return "dangerous";
				case Protection.Normal:             return "normal";
				case Protection.Signature:          return "signature";
				case Protection.SignatureOrSystem:  return "signatureOrSystem";
				default:
					throw new ArgumentException ("Unsupported Protection value '" + value + "'.", "LaunchMode");
			}
		}

		static string ToString (ScreenOrientation value, int targetSdkVersion = 0)
		{
			switch (value) {
				case ScreenOrientation.Behind:            return "behind";
				case ScreenOrientation.FullSensor:        return "fullSensor";
				case ScreenOrientation.FullUser:          return "fullUser";
				case ScreenOrientation.Landscape:         return "landscape";
				case ScreenOrientation.Locked:            return "locked";
				case ScreenOrientation.Nosensor:          return "nosensor";
				case ScreenOrientation.Portrait:          return "portrait";
				case ScreenOrientation.ReverseLandscape:  return "reverseLandscape";
				case ScreenOrientation.ReversePortrait:   return "reversePortrait";
				case ScreenOrientation.Sensor:            return "sensor";
				case ScreenOrientation.SensorLandscape:   return "sensorLandscape";
				case ScreenOrientation.SensorPortrait:    return targetSdkVersion < 16 ? "sensorPortait" : "sensorPortrait"; // https://bugzilla.xamarin.com/show_bug.cgi?id=12935 !!
				case ScreenOrientation.Unspecified:       return "unspecified";
				case ScreenOrientation.User:              return "user";
				case ScreenOrientation.UserLandscape:     return "userLandscape";
				case ScreenOrientation.UserPortrait:      return "userPortrait";
				default:
					throw new ArgumentException ("Unsupported ScreenOrientation value '" + value + "'.", "ScreenOrientation");
			}
		}

		static string ToString (SoftInput value)
		{
			string stateValue;
			switch (value & SoftInput.MaskState) {
				case SoftInput.StateAlwaysHidden:   stateValue = "stateAlwaysHidden";   break;
				case SoftInput.StateAlwaysVisible:  stateValue = "stateAlwaysVisible";  break;
				case SoftInput.StateHidden:         stateValue = "stateHidden";         break;
				case SoftInput.StateUnchanged:      stateValue = "stateUnchanged";      break;
				case SoftInput.StateUnspecified:    stateValue = "stateUnspecified";    break;
				case SoftInput.StateVisible:        stateValue = "stateVisible";        break;
				default:
					throw new ArgumentException ("Unsupported WindowSoftInputMode state value: " +
							(value & SoftInput.MaskAdjust) + ".", "value");
			}

			string adjustValue;
			switch (value & SoftInput.MaskAdjust) {
				case SoftInput.AdjustNothing:       adjustValue = "adjustNothing";      break;
				case SoftInput.AdjustUnspecified:   adjustValue = "adjustUnspecified";  break;
				case SoftInput.AdjustResize:        adjustValue = "adjustResize";       break;
				case SoftInput.AdjustPan:           adjustValue = "adjustPan";          break;
				default:
					throw new ArgumentException ("Unsupported WindowSoftInputMode adjust value: " +
							(value & SoftInput.MaskAdjust) + ".", "value");
			}

			return stateValue + "|" + adjustValue;
		}

		static string ToString (UiOptions value)
		{
			switch (value) {
				case UiOptions.None:                      return "none";
				case UiOptions.SplitActionBarWhenNarrow:  return "splitActionBarWhenNarrow";
				default:
					throw new ArgumentException ("Unsupported UiOptions value '" + value + "'.", "LaunchMode");
			}
		}

		static string ToString (WindowRotationAnimation value)
		{
			switch (value) {
				case WindowRotationAnimation.Crossfade: return "crossfade";
				case WindowRotationAnimation.Jumpcut:   return "jumpcut";
				case WindowRotationAnimation.Rotate:    return "rotate";
				case WindowRotationAnimation.Seamless:  return "seamless";
				default:
					throw new ArgumentException ($"Unsupported WindowRotationAnimation value '{value}'", "WindowRotationAnimation");
			}
		}

		static string ToString (string value, ICustomAttributeProvider provider, IAssemblyResolver resolver)
		{
			var typeDef = ResolveType (value, provider, resolver);
			return ToString (typeDef);
		}

		IEnumerator<string> IEnumerable<string>.GetEnumerator ()
		{
			return Mappings.Keys.GetEnumerator ();
		}

		IEnumerator IEnumerable.GetEnumerator ()
		{
			return Mappings.Keys.GetEnumerator ();
		}
	}
}
