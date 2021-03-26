using CodeGenerator;
using System;
using System.Collections.Immutable;

namespace Sample
{
    class Program
    {
        static void Main(string[] args)
        {
            NameList nl1 = new NameList(ImmutableArray.Create("hi", "hello"));
            NameList nl2 = new NameList(ImmutableArray.Create("hr".Replace('r', 'i'), "hells".Replace('s', 'o')));

            Console.WriteLine(nl1.Equals(nl2));
        }
    }
}
