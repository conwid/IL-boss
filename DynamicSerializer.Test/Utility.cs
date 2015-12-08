using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;
using DynamicSerializer.Core;

namespace DynamicSerializer.Test
{
	public sealed class Utility
	{
		/// <summary>
		/// Olvasható kimenet az eredmény tesztelésére
		/// </summary>
		/// <param name="ms"></param>
		public static void ConsoleWriter( MemoryStream ms )
		{
			ms.Position = 0;

			var br = new SmartBinaryReader( ms );
			var br2 = new BinaryReader( ms );

			StringBuilder sbBinary = new StringBuilder();
			StringBuilder sbHex = new StringBuilder();

			Debug.WriteLine( "Ref lista mérete: " + br.ReadInt32() );

			var numoftypes = br.ReadInt32();
			Debug.WriteLine( "Típusok száma: " + numoftypes );

			for( int i = 0; i < numoftypes; i++ )
			{
				Debug.WriteLine( "Tipus[" + (i + 15) + "]: " + br.ReadString() );
			}
			while( ms.Position != ms.Length )
			{
				var myByte = br2.ReadByte();
				sbBinary.Append( Convert.ToString( myByte, 2 ).PadLeft( 8, '0' ) + " " );
				sbHex.Append( myByte.ToString( "X" ).PadLeft( 2, '0' ) + " " );
			}
			Debug.WriteLine( sbBinary.ToString() );
			Debug.WriteLine( sbHex.ToString() );
			Debug.WriteLine( "" );
			ms.Position = 0;
		}


		#region Stream


		public static Stream SerializeToMemStream( object graph )
		{

			MemoryStream mem = new MemoryStream();
			DynamicSerializerEngine.Serialize( graph, mem );


			return mem;
		}

		public static byte[] SerializeToByteArray( object graph )
		{

			MemoryStream mem = new MemoryStream();
			DynamicSerializerEngine.Serialize( graph, mem );
			byte[] res = mem.ToArray();

			return res;
		}

		public static object DeserializeFromByteArray( Byte[] stream )
		{

			MemoryStream ms = new MemoryStream( stream );

			return DynamicSerializerEngine.Deserialize( ms );



		}

		public static object DeserializeFromMemStream( MemoryStream ms )
		{

			return DynamicSerializerEngine.Deserialize( ms );

		}


		/*
        public static T DeSerializeFromMem<T>(Stream stream)
        {
            DynamicSerializer serializer = new DynamicSerializer("");
            stream.Position = 0;
            return (T)serializer.ReadObject(stream);



        }
        */
		#endregion


		#region File
		/// <summary>
		/// Serialize the given oject graph to XML using the DynamicSerializer engine
		/// </summary>
		/// <param name="graph">An element of the graph to be serialized</param>
		/// <param name="filename"></param>
		public static void SerializeToXml( object graph, string filename )
		{


			//TODO: If the file exists open it and set stream to end
			FileStream fs = new FileStream( filename, FileMode.Create );

			XmlDictionaryWriter writer = XmlDictionaryWriter.CreateTextWriter( fs );

			MemoryStream mem = new MemoryStream();
			DynamicSerializerEngine.Serialize( graph, mem );
			writer.WriteStartElement( graph.ToString() );
			byte[] res = mem.ToArray();
			writer.WriteBase64( res, 0, res.Length );
			//Console.WriteLine("Length: {0}", res.Length);
			writer.WriteEndElement();


			writer.Close();
			fs.Close();

		}


		/// <summary>
		/// Deserilaize objects from the given XML file using the DynamicSerializer engine
		/// </summary>
		/// <param name="filename"></param>
		/// <returns>List<object> containing the objects</returns>
		public static List<object> DeSerializeFromXml( string filename )
		{


			if( !File.Exists( filename ) )
			{
				throw new FileNotFoundException();
			}

			FileStream fs = new FileStream( filename, FileMode.Open );
			XmlDictionaryReader reader = XmlDictionaryReader.CreateTextReader( fs, new XmlDictionaryReaderQuotas() );
			List<object> result = new List<object>();


			while( !reader.EOF )
			{

				byte[] b = reader.ReadElementContentAsBase64();
				MemoryStream ms = new MemoryStream( b );
				object res = DynamicSerializerEngine.Deserialize( ms );
				ms.Close();

				result.Add( res );
			}

			reader.Close();
			fs.Close();
			return result;


		}

		#endregion

	}
}
