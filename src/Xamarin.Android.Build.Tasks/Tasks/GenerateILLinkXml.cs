using System.Xml.Linq;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks;

public class GenerateILLinkXml : AndroidTask
{
	public override string TaskPrefix => "GILX";

	[Required]
	public string AndroidHttpClientHandlerType { get; set; }

	[Required]
	public string CustomViewMapFile { get; set; }

	[Required]
	public string OutputFile { get; set; }

	public override bool RunTask ()
	{
		string assemblyName, typeName;

		var index = AndroidHttpClientHandlerType.IndexOf (',');
		if (index != -1) {
			typeName = AndroidHttpClientHandlerType.Substring (0, index).Trim ();
			assemblyName = AndroidHttpClientHandlerType.Substring (index + 1).Trim ();
		} else {
			typeName = AndroidHttpClientHandlerType;
			assemblyName = "Mono.Android";
		}

		// public parameterless constructors
		// example: https://github.com/dotnet/runtime/blob/039d2ecb46687e89337d6d629c295687cfe226be/src/mono/System.Private.CoreLib/src/ILLink/ILLink.Descriptors.xml
		var ctor = new XElement ("method", new XAttribute("signature", "System.Void .ctor()"));

		XElement linker;
		var doc = new XDocument (
			linker = new XElement ("linker",
				new XElement ("assembly",
					new XAttribute ("fullname", assemblyName),
					new XElement ("type", new XAttribute ("fullname", typeName), ctor)
				)
			)
		);

		var customViewMap = MonoAndroidHelper.LoadCustomViewMapFile (BuildEngine4, CustomViewMapFile);
		foreach (var pair in customViewMap) {
			index = pair.Key.IndexOf (',');
			if (index == -1)
				continue;

			typeName = pair.Key.Substring (0, index).Trim ();
			assemblyName = pair.Key.Substring (index + 1).Trim ();

			linker.Add (new XElement ("assembly",
				new XAttribute ("fullname", assemblyName),
				new XElement ("type", new XAttribute ("fullname", typeName), ctor)
			));
		}

		if (doc.SaveIfChanged (OutputFile)) {
			Log.LogDebugMessage ($"Saving {OutputFile}");
		} else {
			Log.LogDebugMessage ($"{OutputFile} is unchanged. Skipping.");
		}

		return !Log.HasLoggedErrors;
	}
}
