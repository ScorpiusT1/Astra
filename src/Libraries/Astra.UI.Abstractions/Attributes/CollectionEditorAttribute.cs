namespace Astra.UI.Abstractions.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class CollectionEditorAttribute : Attribute
    {
        public Type ItemType { get; set; }
    }


}
