namespace AssistantAI.Utilities;

public class TemplateBuilder {
    public Dictionary<string, string> TemplateValues = [];

    public TemplateBuilder AddValue(string key, string value) {
        if(TemplateValues.ContainsKey(key))
            return this;

        TemplateValues.Add(key, value);
        return this;
    }

    public string BuildTemplate(string template) {
        foreach(var (key, value) in TemplateValues) {
            template = template.Replace($"${{{key}}}", value);
        }

        return template;
    }
}
