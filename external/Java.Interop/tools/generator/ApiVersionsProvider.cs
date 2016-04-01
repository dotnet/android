using System;
using System.Collections.Generic;
using System.Xml;
using System.Linq;

namespace Xamarin.AndroidTools.AnnotationSupport
{
	public class ApiVersionsProvider
	{
		public void Parse (string apiVersionsFilePath)
		{
			using (var reader = XmlReader.Create (apiVersionsFilePath))
				Parse (reader);
		}

		void Parse (XmlReader reader)
		{
			// read api
			reader.MoveToContent (); // -> api
			reader.Read (); // -> (next)
			reader.MoveToContent (); // -> class
			for (; reader.LocalName == "class"; reader.ReadToNextSibling ("class")) {

				int tmpi;
				var name = reader.GetAttribute ("name").Replace ('/', '.').Replace ('$', '.');
				var since = int.Parse (reader.GetAttribute ("since"));
				var deprecated = int.TryParse (reader.GetAttribute ("deprecated"), out tmpi) ? tmpi : 0;

				ClassDefinition klass;
				if (!Versions.TryGetValue (name, out klass)) {
					klass = new ClassDefinition { Name = name, Since = since };
					Versions.Add (name, klass);
				}

				reader.Read ();
				reader.MoveToContent ();
				for (; reader.NodeType != XmlNodeType.EndElement; reader.Skip (), reader.MoveToContent ()) {
					if (reader.NodeType != XmlNodeType.Element)
						continue;

					var csince = reader.GetAttribute ("since");
					if (csince == null)
						continue;
					var cdeprecated = int.TryParse (reader.GetAttribute ("deprecated"), out tmpi) ? tmpi : 0;

					var cname = reader.GetAttribute ("name");
					switch (reader.LocalName) {
					case "field":
						klass.Fields.Add (new Definition { Name = cname, Since = int.Parse (csince), Deprecated = cdeprecated });
						break;
					case "method":
						klass.Methods.Add (new Definition { Name = cname, Since = int.Parse (csince), Deprecated = cdeprecated });
						break;
					}
				}
			}
		}

		public class Definition
		{
			public string Name; // it is name + JNI signature for methods.
			public int Since;
			public int Deprecated;

			string method;
			string [] args;

			public string MethodName {
				get {
					EnsureParsed ();
					return method;
				}
			}
			public string [] Args {
				get {
					EnsureParsed ();
#if GENERATOR
						throw new NotSupportedException ("Not supported as embedded in generator.");
#endif
					return args;
				}
			}

			void EnsureParsed ()
			{
				bool isCtor = Name == "<init>";
				int braceAt = Name.IndexOf ('(');
				method = Name.Substring (0, braceAt);
				string jni = Name.Substring (braceAt);
#if !GENERATOR
				args = ManagedTypeFinder.ParseJniMethodArgumentsSignature (jni);
#endif
			}
		}

		public class ClassDefinition : Definition
		{
			public IList<Definition> Fields { get; private set; } = new List<Definition> ();
			public IList<Definition> Methods { get; private set; } = new List<Definition> ();
		}

		public IDictionary<string,ClassDefinition> Versions { get; private set; } = new Dictionary<string,ClassDefinition> ();
	}
}

