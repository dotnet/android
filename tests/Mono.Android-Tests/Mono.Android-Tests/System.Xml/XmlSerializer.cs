#nullable enable

using System.Xml.Serialization;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace System.XmlTests {
    public class C {
        [DefaultValue(typeof(C), "c")]
        public Type? T { get; }
    }
    
    [TestClass]
    public class XmlSerializerTest {

        [TestMethod]
        [RequiresUnreferencedCode ("Tests XML serialization metadata after trimming.")]
        [RequiresDynamicCode ("Tests XML serialization that may require runtime code generation.")]
        public void TrimmingDefaultValueAttribute ()
        {
            // Context: https://github.com/dotnet/runtime/issues/109724
            var s = new XmlSerializer(typeof(C));
            _ = new C().T; // Prevent C.T from being removed by trimming
        }
    }
}
