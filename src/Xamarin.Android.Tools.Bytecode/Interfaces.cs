using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace Xamarin.Android.Tools.Bytecode {

	// http://docs.oracle.com/javase/specs/jvms/se7/html/jvms-4.html#jvms-4.1
	public sealed class Interfaces : Collection<ConstantPoolClassItem> {

		public  ConstantPool    ConstantPool        {get; private set;}

		public Interfaces (ConstantPool constantPool, Stream stream)
		{
			if (constantPool == null)
				throw new ArgumentNullException ("constantPool");
			if (stream == null)
				throw new ArgumentNullException ("stream");

			ConstantPool    = constantPool;
			var count   = stream.ReadNetworkUInt16 ();
			for (int i = 0; i < count; ++i) {
				var iface = stream.ReadNetworkUInt16 ();
				Add ((ConstantPoolClassItem) constantPool [iface]);
			}
		}
	}
}
