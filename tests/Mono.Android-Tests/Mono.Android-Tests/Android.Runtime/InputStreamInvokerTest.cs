using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Android.Runtime;

using NUnit.Framework;

namespace Android.RuntimeTests
{
        [TestFixture]
        public class InputStreamInvokerTest
        {
		[Test]
		public void Disposing_Shared_Data_Does_Not_Throw_IOE ()
		{
			var javaInputStream = new Java.IO.ByteArrayInputStream (new byte[]{0x1, 0x2, 0x3, 0x4});
			var invoker = new InputStreamInvoker (javaInputStream);
			javaInputStream.Dispose ();
			invoker.Dispose ();
		}

                [Test]
                public void InputStreamTest ()
                {
                        string path = Path.GetTempFileName ();
                        string text = "Your're so good looking!";
                        File.WriteAllText (path, text);
                        Java.IO.FileInputStream fileStream = new Java.IO.FileInputStream (path);
                        Java.IO.ByteArrayInputStream byteStream = new Java.IO.ByteArrayInputStream (Encoding.ASCII.GetBytes (text));

                        using (var stream = new InputStreamInvoker (fileStream)) {
                                byte[] bytes = new byte[text.Length];
                                Assert.IsTrue (stream.CanRead);
                                Assert.IsTrue (stream.CanSeek);
                                Assert.IsFalse (stream.CanTimeout);
                                Assert.IsFalse (stream.CanWrite);
                                Assert.AreEqual (stream.Length, 24);
                                Assert.AreEqual (stream.Seek (8, SeekOrigin.Begin), 8);
                                Assert.AreEqual (stream.Position, 8);
                                Assert.AreEqual (stream.Read (bytes, 0, 7), 7);
                                Assert.AreEqual ("so good", Encoding.ASCII.GetString (bytes, 0, 7));
                                Assert.AreEqual (stream.Seek (1, SeekOrigin.Current), 16);
                                Assert.AreEqual (stream.Read (bytes, 0, 7), 7);
                                Assert.AreEqual ("looking", Encoding.ASCII.GetString (bytes, 0, 7));
                                Assert.AreEqual (stream.Seek (-text.Length, SeekOrigin.End), 0);
                                Assert.AreEqual (stream.Read (bytes, 0, 7), 7);
                                Assert.AreEqual ("Your're", Encoding.ASCII.GetString (bytes, 0, 7));
                                stream.Position = text.Length - 1;
                                Assert.AreEqual (stream.Read (bytes, 0, 1), 1);
                                Assert.AreEqual ("!", Encoding.ASCII.GetString (bytes, 0, 1));
                        }

                        using (var stream = new InputStreamInvoker (byteStream)) {
                                byte[] bytes = new byte[text.Length];
                                Assert.IsTrue (stream.CanRead);
                                Assert.IsFalse (stream.CanSeek);
                                Assert.IsFalse (stream.CanTimeout);
                                Assert.IsFalse (stream.CanWrite);
                                Assert.Throws<NotSupportedException> (() => { var _ = stream.Length; });
                                Assert.Throws<NotSupportedException> (() => { var _ = stream.Position; });
                                Assert.Throws<NotSupportedException> (() => { stream.Position = 1; });
                                Assert.Throws<NotSupportedException> (() => { stream.Seek (1, SeekOrigin.Begin); });
                                Assert.AreEqual (stream.Read (bytes, 0, text.Length), text.Length);
                                Assert.AreEqual (text, Encoding.ASCII.GetString (bytes, 0, text.Length));
                        }

                        File.Delete (path);
                }
        }
}
