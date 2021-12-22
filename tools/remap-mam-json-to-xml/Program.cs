using System.Diagnostics;
using Android.Runtime;
using Xamarin.Android.Tasks;

var mam = new MamJsonParser (Log);
foreach (var path in args) {
	mam.Load (path);
}
var xmlTree = mam.ToXml ();
var xml     = xmlTree.ToString ();

Console.WriteLine (xml);

var x = MamXmlParser.Parse (xml);

if (x.ReplacementTypes.Count != mam.ReplacementTypes.Count) {
	Console.WriteLine ("missing types!");
}
if (x.ReplacementMethods.Count != mam.ReplacementMethods.Count) {
	Console.WriteLine ("missing methods!");
}
foreach (var k in mam.ReplacementTypes.Keys) {
	var ev = mam.ReplacementTypes [k];
	var av = x.ReplacementTypes [k];
	if (ev != av) {
		Console.Error.WriteLine ($"bad replacement type for `{k}`: expected `{ev}` got `{av}");
	}
}
foreach (var k in mam.ReplacementMethods.Keys) {
	var ev = mam.ReplacementMethods [k];
	var av = x.ReplacementMethods [k];
	if (ev != av) {
		Console.Error.WriteLine ($"bad replacement type for `{k}`: expected `{ev}` got `{av}");
	}
}

void Log (TraceLevel level, string message)
{
	switch (level) {
	case TraceLevel.Error:
		Console.Error.WriteLine ($"remap-mam-json-to-xml: {message}");
		break;
	default:
		Console.WriteLine (message);
		break;
	}
}
