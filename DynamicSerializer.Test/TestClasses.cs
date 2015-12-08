using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;

namespace DynamicSerializer.Test
{

	public class IX
	{
		public int IP { get; set; }
	}


	[Serializable]
    public class A
    {

        public A()
        {


        }

        public A(string _name, C _c)
        {

            name = _name;
            cref = _c;
        }

        public string name { get; set; }

        public C cref { get; set; }

        public override bool Equals(object obj)
        {
            return ((obj is A) && ((A) obj).name == this.name);
        }

        public override int GetHashCode()
        {
            return 1;
        }

        public static bool operator ==(A a, A b)
        {
            return a.Equals(b);
        }
        public static bool operator !=(A a, A b)
        {
            return !(a == b);
        }

    }


    public class B : A
    {

        public B()
        {


        }

        public int number { get { return Number; } set { Number = value; } }

        [NonSerialized]
        private int Number;

        public B(string name, int num, C c)
            : base(name, c)
        {

            number = num;
        }


    }

    [Serializable]
    public class C
    {

        public C()
        {

        }

        public int adat { get; set; }

        public C(int _b)
        {
            adat = _b;
        }

    }

    public class CircularA
    {
        public List<CircularB> BArray { get; set; }
        public List<CircularB> CArray { get; set; }
        public CircularB BField { get; set; }

    }

    public class CircularB
    {
        public CircularA A { get; set; }
        public int Id { get; set; }

        public CircularB(int id)
        {
            Id = id;
        }
    }

    public class NoCtor
    {
        public int i;
        //public string s { get; set; }
        public string s;
        public NoCtor(int i, string s)
        {
            this.i = i;
            this.s = s;
        }
    }

    public class Basic
    {
        public int num;
    }
}
