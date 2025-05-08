using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

class MarshalMethodsXmlFile
{
	static readonly XmlWriterSettings settings = new XmlWriterSettings {
		Indent = true,
		NewLineOnAttributes = false,
		OmitXmlDeclaration = true,
	};

	public static void Export (string filename, AndroidTargetArch arch, NativeCodeGenStateObject state, TaskLoggingHelper log)
	{
		if (state.MarshalMethods.Count == 0) {
			WriteEmptyFile (filename, log);
			return;
		}

		using var sw = MemoryStreamPool.Shared.CreateStreamWriter ();
		using (var xml = XmlWriter.Create (sw, settings))
			Export (xml, arch, state);

		sw.Flush ();

		Files.CopyIfStreamChanged (sw.BaseStream, filename);

		log.LogDebugMessage ($"Wrote '{filename}'");
	}

	public static KeyValuePair<AndroidTargetArch, NativeCodeGenStateObject>? Import (string filename)
	{
		// If the file has zero length, then the assembly doesn't have marshal methods.
		// This check is much faster than loading and parsing an empty XML file.
		var fi = new FileInfo (filename);

		if (fi.Length == 0)
			return null;

		var xml = XDocument.Load (filename);
		var root = xml.Root ?? throw new InvalidOperationException ($"Invalid XML file '{filename}'");

		var arch = (AndroidTargetArch) Enum.Parse (typeof (AndroidTargetArch), root.GetRequiredAttribute ("arch"));
		var state = new KeyValuePair<AndroidTargetArch, NativeCodeGenStateObject> (arch, new NativeCodeGenStateObject ());

		var marshalMethods = root.Element ("marshal-methods");

		if (marshalMethods is not null)
			ImportMethods (marshalMethods, state);

		return state;
	}

	static void ImportMethods (XElement element, KeyValuePair<AndroidTargetArch, NativeCodeGenStateObject> state)
	{
		foreach (var mm in element.Elements ("key")) {
			var name = mm.GetRequiredAttribute ("name");
			var methods = new List<MarshalMethodEntryObject> ();

			foreach (var method in mm.Elements ("method")) {

				var isSpecial = method.GetAttributeOrDefault ("is-special", false);
				var jniTypeName = method.GetRequiredAttribute ("jni-type-name");
				var jniMethodName = method.GetRequiredAttribute ("jni-method-name");
				var jniMethodSignature = method.GetRequiredAttribute ("jni-method-signature");
				var declaringType = ImportDeclaringType (method.Element ("declaring-type"));
				var implementedMethod = ImportMethod (method.Element ("implemented-method"));
				var nativeCallback = ImportMethod (method.Element ("native-callback"));
				var registeredMethod = ImportMethodBase (method.Element ("registered-method"));

				var obj = new MarshalMethodEntryObject (
					declaringType: declaringType,
					implementedMethod: implementedMethod,
					isSpecial: isSpecial,
					jniTypeName: jniTypeName,
					jniMethodName: jniMethodName,
					jniMethodSignature: jniMethodSignature,
					nativeCallback: nativeCallback!,
					registeredMethod: registeredMethod
				);

				methods.Add (obj);
			}
			state.Value.MarshalMethods.Add (name, methods);
		}
	}

	static MarshalMethodEntryMethodObject? ImportMethod (XElement element)
	{
		if (element is null)
			return null;

		var name = element.GetRequiredAttribute ("name");
		var fullName = element.GetRequiredAttribute ("full-name");
		var metadataToken = uint.Parse (element.GetRequiredAttribute ("metadata-token"));
		var assemblyIndex = element.GetUIntAttributeOrDefault ("assembly-index", null);
		var classIndex = element.GetUIntAttributeOrDefault ("class-index", null);
		var methodIndex = element.GetUIntAttributeOrDefault ("method-index", null);
		var declaringType = ImportDeclaringType (element.Element ("declaring-type"));
		var parameters = ImportParameters (element.Element ("parameters"));

		var method = new MarshalMethodEntryMethodObject (
			name: name,
			fullName: fullName,
			declaringType: declaringType,
			metadataToken: metadataToken,
			parameters: parameters
		);

		method.AssemblyIndex = assemblyIndex;
		method.ClassIndex = classIndex;
		method.MethodIndex = methodIndex;

		return method;
	}

	static List<MarshalMethodEntryMethodParameterObject> ImportParameters (XElement element)
	{
		var parameters = new List<MarshalMethodEntryMethodParameterObject> ();

		if (element is null)
			return parameters;

		foreach (var parameter in element.Elements ("parameter")) {
			var name = parameter.GetAttributeOrDefault ("name", "");
			var parameterTypeName = parameter.GetRequiredAttribute ("parameter-type-name");
			var parameterObject = new MarshalMethodEntryMethodParameterObject (name, parameterTypeName);
			parameters.Add (parameterObject);
		}

		return parameters;
	}

	static MarshalMethodEntryMethodBaseObject? ImportMethodBase (XElement element)
	{
		if (element is null)
			return null;

		var fullName = element.GetRequiredAttribute ("full-name");

		return new MarshalMethodEntryMethodBaseObject (
			fullName: fullName
		);
	}

	static MarshalMethodEntryTypeObject ImportDeclaringType (XElement element)
	{
		var fullName = element.GetRequiredAttribute ("full-name");
		var metadataToken = uint.Parse (element.GetRequiredAttribute ("metadata-token"));
		var module = element.Element ("module");

		return new MarshalMethodEntryTypeObject (
			fullName: fullName,
			metadataToken: metadataToken,
			module: ImportModule (module)
		);
	}

	static MarshalMethodEntryModuleObject ImportModule (XElement element)
	{
		var assembly = element.Element ("assembly");

		return new MarshalMethodEntryModuleObject (
			assembly: ImportAssembly (assembly)
		);
	}

	static MarshalMethodEntryAssemblyObject ImportAssembly (XElement element)
	{
		var fullName = element.GetRequiredAttribute ("full-name");
		var nameFullName = element.GetRequiredAttribute ("name-full-name");
		var mainModuleFileName = element.GetAttributeOrDefault ("main-module-file-name", "");
		var nameName = element.GetRequiredAttribute ("name-name");

		return new MarshalMethodEntryAssemblyObject (
			fullName: fullName,
			nameFullName: nameFullName,
			mainModuleFileName: mainModuleFileName,
			nameName: nameName
		);
	}

	static void Export (XmlWriter xml, AndroidTargetArch arch, NativeCodeGenStateObject state)
	{
		xml.WriteStartElement ("api");
		xml.WriteAttributeString ("arch", arch.ToString ());

		xml.WriteStartElement ("marshal-methods");

		foreach (var kvp in state.MarshalMethods)
			ExportMethods (xml, kvp);

		xml.WriteEndElement ();
		xml.WriteEndElement ();
	}

	static void ExportMethods (XmlWriter xml, KeyValuePair<string, IList<MarshalMethodEntryObject>> kvp)
	{
		xml.WriteStartElement ("key");
		xml.WriteAttributeString ("name", kvp.Key);

		foreach (var method in kvp.Value) {
			xml.WriteStartElement ("method");
			xml.WriteAttributeString ("is-special", method.IsSpecial.ToString ());
			xml.WriteAttributeString ("jni-type-name", method.JniTypeName);
			xml.WriteAttributeString ("jni-method-name", method.JniMethodName);
			xml.WriteAttributeString ("jni-method-signature", method.JniMethodSignature);
			ExportDeclaringType (xml, method.DeclaringType);
			ExportMethod (xml, "implemented-method", method.ImplementedMethod);
			ExportMethod (xml, "native-callback", method.NativeCallback);
			ExportRegisteredMethod (xml, method.RegisteredMethod);
			xml.WriteEndElement ();
		}

		xml.WriteEndElement ();
	}

	static void ExportDeclaringType (XmlWriter xml, MarshalMethodEntryTypeObject type)
	{
		xml.WriteStartElement ("declaring-type");
		xml.WriteAttributeString ("full-name", type.FullName);
		xml.WriteAttributeString ("metadata-token", type.MetadataToken.ToString ());
		ExportModule (xml, type.Module);
		xml.WriteEndElement ();
	}

	static void ExportModule (XmlWriter xml, MarshalMethodEntryModuleObject module)
	{
		xml.WriteStartElement ("module");
		ExportAssembly (xml, module.Assembly);
		xml.WriteEndElement ();
	}

	static void ExportAssembly (XmlWriter xml, MarshalMethodEntryAssemblyObject assembly)
	{
		xml.WriteStartElement ("assembly");
		xml.WriteAttributeString ("full-name", assembly.FullName);
		xml.WriteAttributeString ("name-full-name", assembly.NameFullName);
		xml.WriteAttributeString ("main-module-file-name", assembly.MainModuleFileName);
		xml.WriteAttributeString ("name-name", assembly.NameName);
		xml.WriteEndElement ();
	}

	static void ExportRegisteredMethod (XmlWriter xml, MarshalMethodEntryMethodBaseObject? method)
	{
		if (method is null)
			return;

		xml.WriteStartElement ("registered-method");
		xml.WriteAttributeString ("full-name", method.FullName);
		xml.WriteEndElement ();
	}

	static void ExportMethod (XmlWriter xml, string elementName, MarshalMethodEntryMethodObject? method)
	{
		if (method is null)
			return;

		xml.WriteStartElement (elementName);
		xml.WriteAttributeString ("name", method.Name);
		xml.WriteAttributeString ("full-name", method.FullName);
		xml.WriteAttributeString ("metadata-token", method.MetadataToken.ToString ());
		xml.WriteAttributeStringIfNotDefault ("assembly-index", method.AssemblyIndex.ToString ());
		xml.WriteAttributeStringIfNotDefault ("class-index", method.ClassIndex.ToString ());
		xml.WriteAttributeStringIfNotDefault ("method-index", method.MethodIndex.ToString ());

		ExportDeclaringType (xml, method.DeclaringType);

		if (method.HasParameters) {
			xml.WriteStartElement ("parameters");
			foreach (var parameter in method.Parameters)
				ExportParameter (xml, parameter);
			xml.WriteEndElement ();
		}

		xml.WriteEndElement ();
	}

	static void ExportParameter (XmlWriter xml, MarshalMethodEntryMethodParameterObject parameter)
	{
		xml.WriteStartElement ("parameter");
		xml.WriteAttributeString ("name", parameter.Name);
		xml.WriteAttributeString ("parameter-type-name", parameter.ParameterTypeName);
		xml.WriteEndElement ();
	}

	/// <summary>
	/// Given an assembly path, return the path to the ".mm.xml" file that should be next to it.
	/// </summary>
	public static string GetMarshalMethodsXmlFilePath (string assemblyPath)
		=> Path.ChangeExtension (assemblyPath, ".mm.xml");

	public static void WriteEmptyFile (string destination, TaskLoggingHelper log)
	{
		log.LogDebugMessage ($"Writing empty file '{destination}'");

		// We write a zero byte file to indicate the file couldn't have JLO types and wasn't scanned
		File.Create (destination).Dispose ();
	}
}
