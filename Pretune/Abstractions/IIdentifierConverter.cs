namespace Pretune.Abstractions
{
    internal interface IIdentifierConverter
    {
        string ConvertMemberToParam(string memberIdentifier);
        string ConvertMemberToProperty(string memberName);
    }
}