using Pretune;
using System;
using System.Collections.Generic;
using System.Text;

namespace CodeGenerator
{
    public partial class Sample<T>
    {
        public Sample(int @x, T @param)
        {
            this.X = @x;
            this.Param = @param;
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