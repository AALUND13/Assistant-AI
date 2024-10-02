namespace AssistantAI.Utilities.Attributes {
    /// <summary>
    /// You can use this attribute to ignore a parameter or property when generating a JSON schema.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
    public class IgnoreAttribute : Attribute;
}
