using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;

namespace Xamarin.Android.Tools.Bytecode {

	public enum ModuleFlags {
		Open        = 0x0020,
		Synthetic   = 0x1000,
		Mandated    = 0x8000,
	}

	public enum ModuleRequiresInfoFlags {
		Transitive      = 0x0020,
		StaticPhase     = 0x0040,
		Synthetic       = 0x1000,
		Mandated        = 0x8000,
	}

	// https://docs.oracle.com/javase/specs/jvms/se11/html/jvms-4.html#jvms-4.7.25
	public sealed class ModuleRequiresInfo {
		ushort  requiresIndex;
		ushort  requiresVersionIndex;


		public  ConstantPool                ConstantPool    {get; private set;}
		public  ModuleRequiresInfoFlags     RequiresFlags   {get; private set;}

		public  string Requires =>
			((ConstantPoolModuleItem) ConstantPool [requiresIndex]).Name.Value;
		public  string RequiresVersion =>
			((ConstantPoolUtf8Item) ConstantPool [requiresVersionIndex]).Value;

		public ModuleRequiresInfo (ConstantPool constantPool, Stream stream)
		{
			ConstantPool            = constantPool;

			requiresIndex           = stream.ReadNetworkUInt16 ();
			RequiresFlags           = (ModuleRequiresInfoFlags) stream.ReadNetworkUInt16 ();
			requiresVersionIndex    = stream.ReadNetworkUInt16 ();
		}

		public override string ToString ()
		{
			return $"Requires({Requires}, Version={RequiresVersion}, Flags={RequiresFlags})";
		}
	}

	public enum ModuleExportsPackageInfoFlags {
		Synthetic   = 0x1000,
		Mandated    = 0x8000,
	} 

	// https://docs.oracle.com/javase/specs/jvms/se11/html/jvms-4.html#jvms-4.7.25
	public sealed class ModuleExportsPackageInfo {
		ushort  exportsIndex;
		public  string  Exports =>
			((ConstantPoolPackageItem) ConstantPool [exportsIndex]).Name.Value;

		public  ConstantPool                            ConstantPool    {get; private set;}
		public  ModuleExportsPackageInfoFlags	        Flags           {get; private set;}
		public  Collection<ConstantPoolModuleItem>	ExportsTo       {get;} = new ();


		public ModuleExportsPackageInfo (ConstantPool constantPool, Stream stream)
		{
			ConstantPool            = constantPool;

			exportsIndex            = stream.ReadNetworkUInt16 ();
			Flags                   = (ModuleExportsPackageInfoFlags) stream.ReadNetworkUInt16 ();

			var count               = stream.ReadNetworkUInt16 ();
			for (int i = 0; i < count; ++i) {
				var exports_to_index = stream.ReadNetworkUInt16 ();
				ExportsTo.Add ((ConstantPoolModuleItem) constantPool [exports_to_index]);
			}
		}

		public override string ToString ()
		{
			var s = new StringBuilder ()
				.Append ("ExportsPackage(Name=\"").Append (Exports).Append ("\"");
			if (Flags != 0) {
				s.Append (", Flags=").Append (Flags);
			}
			if (ExportsTo.Count > 0) {
				s.Append (", To={");
				s.Append (ExportsTo [0].Name.Value);
				for (int i = 1; i < ExportsTo.Count; ++i) {
					s.Append (", ");
					s.Append (ExportsTo [i].Name.Value);
				}
				s.Append ("}");
			}
			s.Append (")");
			return s.ToString ();
		}
	}


	public enum ModuleOpensPackageInfoFlags {
		Synthetic   = 0x1000,
		Mandated    = 0x8000,
	}

	// https://docs.oracle.com/javase/specs/jvms/se11/html/jvms-4.html#jvms-4.7.25
	public sealed class ModuleOpensPackageInfo {
		ushort  opensIndex;

		public  ConstantPool                            ConstantPool    {get; private set;}

		public  ModuleOpensPackageInfoFlags	        Flags 	    {get; private set;}

		public  Collection<ConstantPoolModuleItem>	OpensTo     {get;} = new ();
		public  string  Opens =>
			((ConstantPoolPackageItem) ConstantPool [opensIndex]).Name.Value;

		public ModuleOpensPackageInfo (ConstantPool constantPool, Stream stream)
		{
			ConstantPool            = constantPool;

			opensIndex              = stream.ReadNetworkUInt16 ();
			Flags                   = (ModuleOpensPackageInfoFlags) stream.ReadNetworkUInt16 ();

			var count               = stream.ReadNetworkUInt16 ();
			for (int i = 0; i < count; ++i) {
				var opens_to_index      = stream.ReadNetworkUInt16 ();
				OpensTo.Add ((ConstantPoolModuleItem) constantPool [opens_to_index]);
			}
		}

		public override string ToString ()
		{
			var to = string.Join (", ", OpensTo.Select (e => e.Name.Value));
			return $"Opens({Opens}, Flags={Flags}, To={to})";
		}
	}

	// https://docs.oracle.com/javase/specs/jvms/se11/html/jvms-4.html#jvms-4.7.25
	public sealed class ModuleProvidesInfo {
		ushort  providesIndex;

		public  string Provides =>
			((ConstantPoolClassItem) ConstantPool [providesIndex]).Name.Value;

		public  ConstantPool        ConstantPool    {get; private set;}

		public  Collection<ConstantPoolClassItem>       ProvidesWith    {get;} = new ();

		public ModuleProvidesInfo (ConstantPool constantPool, Stream stream)
		{
			ConstantPool            = constantPool;

			providesIndex           = stream.ReadNetworkUInt16 ();

			var count               = stream.ReadNetworkUInt16 ();
			for (int i = 0; i < count; ++i) {
				var provides_with_index = stream.ReadNetworkUInt16 ();
				ProvidesWith.Add ((ConstantPoolClassItem) constantPool [provides_with_index]);
			}
		}

		public override string ToString ()
		{
			var with = string.Join (", ", ProvidesWith.Select (e => e.Name.Value));
			return $"Service(ServiceInterface={Provides}, ServiceImplementations={{{with}}})";
		}
	}
}