using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Reflection.Emit;

namespace DynamicSerializer.Core
{
	public static class Helpers
	{
		private static Type stringtype = typeof( string );
		private static Type objecttype = typeof( object );
		private static Type datetimetype = typeof( DateTime );
		private static Type collectiontype = typeof( ICollection<> );
		private static Type dicttype = typeof( Dictionary<,> );

		private static FieldInfo nextserializerfield = typeof( DynamicSerializerEngine ).GetField( "nextserializer", BindingFlags.Static | BindingFlags.Public );
		private static MethodInfo invokemethod = typeof( Action<object, BinaryWriter> ).GetMethod( "Invoke", new[] { objecttype, typeof( BinaryWriter ) } );

		

		#region TypeHelpers

		public static bool IsAtomic( this Type type )
		{
			return ((type != objecttype) && ((type.IsPrimitive) || (type.IsEnum) || (type == stringtype) || (type == datetimetype)));
		}

		public static bool IsGenericCollection( this Type type )
		{
			return type.GetInterfaces().Any( i => i.IsGenericType && i.GetGenericTypeDefinition() == collectiontype );
		}

		public static bool IsGenericDictionary( this Type type )
		{
			return ((type.IsGenericType) && (type.GetGenericTypeDefinition() == dicttype));
		}

		public static bool IsNonGenericCollection( this Type type )
		{
			Type t = type.GetInterface( "ICollection" );
			return (t != null && !t.IsGenericType);
		}
		#endregion

		#region PropertyHelpers        
		public static bool IsPropertyWritable( this PropertyInfo p )
		{
			return ((p.CanWrite) && (p.GetSetMethod( true ) != null));
		}

		public static bool IsFieldWritable( this FieldInfo f )
		{
			return (!f.IsLiteral) && (!f.IsInitOnly);
		}
		#endregion

		#region ILGeneratorHelpers

		public static void EmitInvokeNextSerializer( this ILGenerator gen )
		{
			gen.Emit( OpCodes.Ldsfld, nextserializerfield );
			gen.Emit( OpCodes.Ldarg_0 );
			gen.Emit( OpCodes.Ldarg_1 );
			gen.Emit( OpCodes.Callvirt, invokemethod );
		}
		#endregion

		public static int TypeIndex( this List<string> typelist, string typename )
		{
			for( int i = 0; i < typelist.Count; i++ )
			{
				if( typelist[i].Equals( typename ) )
				{
					return i;
				}
			}
			return -1;
		}

		

	}

}
