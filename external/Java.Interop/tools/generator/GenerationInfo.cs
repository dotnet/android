using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MonoDroid.Generation {

	public class GenerationInfo {

		public GenerationInfo (string csdir, string javadir, string assembly)
		{
			this.csdir = csdir;
			this.javadir = javadir;
			this.assembly = assembly;
		}

		string assembly;
		public string Assembly {
			get { return assembly; }
		}

		string csdir;
		public string CSharpDir {
			get { return csdir; }
		}

		string javadir;
		public string JavaDir {
			get { return javadir; }
		}

		string member;
		public string CurrentMember {
			get { return typename + "." + member; }
			set { member = value; }
		}

		string typename;
		public string CurrentType {
			get { return typename; }
			set { typename = value; }
		}

		StreamWriter sw;
		public StreamWriter Writer {
			get { return sw; }
			set { sw = value; }
		}
		
		List<string> generated_files = new List<string> ();
		public IEnumerable<string> GeneratedFiles {
			get { return generated_files; }
		}

		public StreamWriter OpenStream (string name) 
		{
			if (!Directory.Exists(csdir))
				Directory.CreateDirectory (csdir);
			string filename = Path.Combine (csdir, name + ".cs");
			
			sw = new StreamWriter (File.Create (filename));
			generated_files.Add (filename);
			return sw;
		}

		List<string> enums = new List<string> ();
		public ICollection<string> Enums {
			get { return enums; }
		}

		List<KeyValuePair<string, string>> type_registrations = new List<KeyValuePair<string, string>> ();
		public ICollection<KeyValuePair<string, string>> TypeRegistrations {
			get { return type_registrations; }
		}

		public void GenerateLibraryProjectFile ()
		{
			int idx = Assembly.IndexOf (',');
			string name = idx < 0 ? Assembly : Assembly.Substring (0, idx);
			string filename = Path.Combine (csdir, name + ".csproj");
			string template;
			using (var res = typeof (GenerationInfo).Assembly.GetManifestResourceStream ("library-project-template.txt"))
				template = new StreamReader (res).ReadToEnd ();
			string [] files = (from s in GeneratedFiles 
				select "    <Compile Include=\"" + s.Substring (csdir.Length + 1) + "\" />").ToArray ();
			string proj = template.Replace ("$$$$$$$$ ASSEMBLY NAME HERE $$$$$$$$", name)
		 		.Replace ("$$$$$$$$ ROOT NAMESPACE HERE $$$$$$$$", name)
				.Replace ("$$$$$$$$ FILENAMES HERE $$$$$$$$", String.Join ("\n", files));
			using (var output = File.CreateText (filename))
				output.WriteLine (proj);
		}
	}
}

