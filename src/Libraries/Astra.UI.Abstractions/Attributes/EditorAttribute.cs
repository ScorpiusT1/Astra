namespace Astra.UI.Abstractions.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class EditorAttribute : Attribute
    {
        public Type EditorType { get; }
        public EditorAttribute(Type editorType)
        {
            EditorType = editorType;
        }
    }


}
