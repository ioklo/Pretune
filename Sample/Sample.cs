using Pretune;
using System;
using System.Collections.Generic;
using System.Text;

namespace CodeGenerator
{
    [AutoConstructor]
    public partial class Sample<T>
    {
        public int X { get; set; }
        public int Y { get => 1; } // no generation
        T Param;
    }

    [AutoConstructor]
    public partial class PolarCoord
    {
        int x;
        int y;

    }


    [AutoConstructor, ImplementINotifyPropertyChanged]
    public partial class Property
    {
        string firstName;
        string lastName;

        //[DependsOn(nameof(firstName), nameof(lastName))]
        //public string FullName { get => $"{firstName} {lastName}"; }
    }
}
