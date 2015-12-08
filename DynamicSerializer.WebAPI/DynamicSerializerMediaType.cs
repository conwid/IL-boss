using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using DynamicSerializer.Core;

namespace DynamicSerializer.WebAPI
{
    public class DynamicSerializerMediaType : BufferedMediaTypeFormatter
    {

        public DynamicSerializerMediaType()
        {
            SupportedMediaTypes.Add(new MediaTypeHeaderValue("application/octet-stream"));
            
        }
        public override bool CanReadType(Type type)
        {
            return true;
        }

        public override bool CanWriteType(Type type)
        {
            return false;
        }

        public override object ReadFromStream(Type type, Stream readStream, HttpContent content, IFormatterLogger formatterLogger)
        {
            using (var ms = new MemoryStream())
            {
                readStream.Position = 0;
                readStream.CopyTo(ms);
                ms.Position = 0;
                var r = DynamicSerializerEngine.Deserialize(ms);
                return r;
            }
        }
    }
}
