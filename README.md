# Pretune
Syntax-based C# preprocessor generating boilerplate code before compile.

## Pros
 - No assembly dependencies needed. It's development-time tool.
 - Can check the generated code, and also set breakpoints on debug.
 - using C# code as DSL, c# compiler will verify input is well-typed.

## Features 
 - [Generating constructor](#auto_constructor) (for data only classes)
 - [Implementing INotifyPropetyChanged](#implement_inotifypropertychanged)

### Generate Constuctor <a name="auto_constructor"> </a>
Pretune generates constructor by adding 'AutoConstructor' attibute to class.

make this code
```csharp 
// MyClass.cs
[AutoConstructor]
partial class MyClass
{
    int a;
    string b;
}
```

and build then you will get following code

```csharp
// PretuneGenerated/MyClass.g.cs
partial class MyClass
{
    public MyClass(int a, string b)
    {
        this.a = a;
        this.b = b;
    }
}
```
> **NOTICE** Though this feature replaced by `record` of C# 9, it's still useful for the project that needs previous version of C#. And it will be applied to struct (ASAP).

### Implementing `INotifyPropetyChanged` <a name="implement_inotifypropertychanged"> </a> 

make this code
```csharp 
// ViewModel.cs
[ImplementINotifyPropertyChanged]
public partial class ViewModel
{
    string firstName;
    string lastName;
}    
```

and build then you will get following code

```csharp
// PretuneGenerated/ViewModel.g.cs
partial class ViewModel
{
    public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    public string FirstName
    {
        get => firstName;
        set
        {
            if (!firstName.Equals(value))
            {
                firstName = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs("FirstName"));
            }
        }
    }

    public string LastName
    {
        get => lastName;
        set
        {
            if (!lastName.Equals(value))
            {
                lastName = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs("LastName"));
            }
        }
    }
}
```

## Installation

### Visual Studio
Add 'Pretune' package from nuget

### CLI
```
dotnet add package Pretune --version 0.5.0
```

## Requirements
 - .net core runtime 3.1
