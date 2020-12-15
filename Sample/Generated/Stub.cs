using System;

namespace Pretune
{
    class AutoConstructorAttribute : Attribute { }
    class ImplementINotifyPropertyChangedAttribute : Attribute { }
    class DependsOnAttribute : Attribute 
    {
        public DependsOnAttribute(params string[] names) { }
    }
}