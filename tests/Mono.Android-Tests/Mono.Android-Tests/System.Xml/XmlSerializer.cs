#nullable enable

using System.Xml.Serialization;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

using NUnit.Framework;

namespace System.XmlTests {
    public class C {
        [DefaultValue(typeof(C), "c")]
        public Type? T { get; }
    }
    
    [TestFixture]
    public class XmlSerializerTest {

        [Test]
        [RequiresUnreferencedCode ("Tests XML serialization metadata after trimming.")]
        public void TrimmingDefaultValueAttribute ()
        {
            // Context: https://github.com/dotnet/runtime/issues/109724
            var s = new XmlSerializer(typeof(C));
            _ = new C().T; // Prevent C.T from being removed by trimming
        }
    }
}
