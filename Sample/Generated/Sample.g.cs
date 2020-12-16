using Pretune;
using System;
using System.Collections.Generic;
using System.Text;

namespace CodeGenerator
{
    public partial class Sample<T>
    {
        public Sample(int @x, T @param2)
        {
            this.X = @x;
            this.Param2 = @param2;
        }
    }

    public partial class Property
    {
        public Property(string @firstName, string @lastName)
        {
            this.firstName = @firstName;
            this.lastName = @lastName;
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    }
}