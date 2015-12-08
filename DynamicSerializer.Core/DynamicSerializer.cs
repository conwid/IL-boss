using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace DynamicSerializer.Core
{
    public class DynamicSerializer : XmlObjectSerializer
    {
        private string op;
        public DynamicSerializer(string opname)
            : base()
        {
            this.op = opname; //Ez mit fog tudni?
        }

        public override bool IsStartObject(System.Xml.XmlDictionaryReader reader)
        {
            return true;
        }

        public override object ReadObject(System.Xml.XmlDictionaryReader reader, bool verifyObjectName)
        {
            byte[] b = reader.ReadElementContentAsBase64();
            MemoryStream ms = new MemoryStream(b);
            object res = DynamicSerializerEngine.Deserialize(ms);
            ms.Close();
            return res;

        }

        public override void WriteEndObject(System.Xml.XmlDictionaryWriter writer)
        {
        }

        public override void WriteObjectContent(System.Xml.XmlDictionaryWriter writer, object graph)
        {
            MemoryStream mem = new MemoryStream();
            DynamicSerializerEngine.Serialize(graph, mem);
            writer.WriteStartElement(this.op);
            byte[] res = mem.ToArray();
            writer.WriteBase64(res, 0, res.Length);
            //Console.WriteLine("Length: {0}", res.Length);
            writer.WriteEndElement();
        }

        public override void WriteStartObject(System.Xml.XmlDictionaryWriter writer, object graph)
        {
        }
    }

}
