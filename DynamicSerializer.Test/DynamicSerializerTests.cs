using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using DynamicSerializer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DynamicSerializer.Test
{
    /// <summary>
    /// Czeglédi Viktor Test osztály, hogy külön meglegyenek a tesztek
    /// </summary>
    [TestClass]
    public class DynamicSerializerTests
    {
        [TestMethod]
        public void CircularReferenceTest()
        {
            var A = new CircularA();
            var B = new CircularB(1);


            B.A = A;
            A.BField = new CircularB(3);
            A.BArray = new List<CircularB> { B, A.BField, A.BField };
            //var list = new List<CircularA>();
            //for (int i = 0; i < 20; i++)
            //{
            //    list.Add(new CircularA());
            //}
            //A.CArray = A.BArray;
            B.A = A;
            var ms = new MemoryStream();
            DynamicSerializerEngine.Serialize(A, ms);
            Utility.ConsoleWriter(ms);
            ms.Position = 0;
            var res = DynamicSerializerEngine.Deserialize(ms);
            //Assert.AreSame(((CircularA)res).BArray[1], ((CircularA)res).BField);
            //Assert.AreNotSame(((CircularA)res).BArray[0], ((CircularA)res).BField);
        }

        [TestMethod]
        public void Test()
        {

            var oid = new ObjectIDGenerator();
            var a = new A {name = "A"};
            var b = new A {name = "A"};
            var res = a.Equals(b);
            bool b1;
            bool b2;
            bool b3;
            bool b4;
            var r1 = oid.HasId(a, out b1);
            var r3 = oid.HasId(a, out b3);
            var r4 = oid.GetId(a, out b4);
            a = new A();
            r1 = oid.HasId(a, out b1);
            r3 = oid.HasId(a, out b3);
            r4 = oid.GetId(a, out b4);
            //var ms = new MemoryStream();

            //DynamicSerializerEngine.Serialize(, ms);
            //ms.Position = 0;
            //DynamicSerializerEngine.Deserialize(ms);
        }

        [TestMethod]
        public void NoDefaultCtor()
        {
            NoCtor c;
            var ms = new MemoryStream();
            var noctor = new NoCtor(3,"s");
            DynamicSerializerEngine.Serialize(noctor, ms);
            Utility.ConsoleWriter(ms);
            //NoCtor o = (NoCtor)FormatterServices.GetUninitializedObject(typeof (NoCtor));

            var r = DynamicSerializerEngine.Deserialize(ms);
        }

		[TestMethod]
		public void TestInt()
		{
			IX test = new IX() { IP = 99 };
			var ms = new MemoryStream();
			DynamicSerializerEngine.Serialize( test, ms );
			Utility.ConsoleWriter( ms );

			ms.Seek( 0, SeekOrigin.Begin );
			var r = DynamicSerializerEngine.Deserialize( ms );
		}
    }
}
