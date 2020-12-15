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

    [AutoConstructor, ImplementINotifyPropertyChanged]
    public partial class Property : System.ComponentModel.INotifyPropertyChanged
    {
        string firstName;
        string lastName;

        // 
        [DependsOn(nameof(firstName), nameof(lastName))]
        public string FullName { get => $"{firstName} {lastName}"; }

        // generated
        //public event PropertyChangedEventHandler PropertyChanged;

        //public string FirstName
        //{
        //    get => firstName;
        //    set
        //    {
        //        if (!firstName.Equals(value))
        //        {
        //            firstName = value;
        //            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs("FirstName"));
        //            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs("FullName"));
        //        }
        //    }
        //}
    }
}
