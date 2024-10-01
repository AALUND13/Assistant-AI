using AssistantAI.Utilities.Extension;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using OpenAI.Chat;
using System.ComponentModel;
using System.Reflection;

namespace AssistantAI.Utilities;

public class ToolsFunctionsBuilder {
    public readonly Dictionary<Delegate, JSchema> ToolFunctions = new Dictionary<Delegate, JSchema>();
    public ToolsFunctionsBuilder WithToolFunction(Delegate delegateMethod) {
        if(ToolFunctions.ContainsKey(delegateMethod))
            return this;

        JSchema methodJsonSchema = delegateMethod.GetMethodInfo().GetJsonSchemaFromMethod();
        ToolFunctions.Add(delegateMethod, methodJsonSchema);

        return this;
    }
}

public class ToolsFunctions {
    private readonly ToolsFunctionsBuilder builder;
    public List<ChatTool> ChatTools { get => builder.ToolFunctions.Keys.Select(GetToolFunctions).ToList(); }

    public ToolsFunctions(ToolsFunctionsBuilder builder) {
        this.builder = builder;
    }

    public ChatTool GetToolFunctions(Delegate delegateMethod) {
        JSchema methodJsonSchema = delegateMethod.GetMethodInfo().GetJsonSchemaFromMethod();
        string methodDescription = delegateMethod.GetMethodInfo().GetCustomAttribute<DescriptionAttribute>()?.Description ?? "No description provided.";

        return ChatTool.CreateFunctionTool(
            functionName: delegateMethod.GetMethodInfo().Name,
            functionDescription: methodDescription,
            functionParameters: BinaryData.FromString(methodJsonSchema.ToString())
        );
    }

    public object? CallToolFunction(ChatToolCall toolCall) {
        // Find the method in the ToolFunctions dictionary based on the function name
        Delegate? method = builder.ToolFunctions.Keys.FirstOrDefault(m => m.GetMethodInfo().Name == toolCall.FunctionName);

        if(method == null)
            throw new ArgumentException($"No tool function with the name '{toolCall.FunctionName}' was found.");

        JObject arguments = JObject.Parse(toolCall.FunctionArguments.ToString());
        ParameterInfo[] parameters = method.GetMethodInfo().GetParameters();

        // Convert the json arguments to the corresponding parameter types
        object?[] args = arguments.Children().Select(a => a.First?.ToObject(parameters.First(p => p.Name == a.Path).ParameterType)).ToArray();

        try {
            return method.DynamicInvoke(args);
        } catch(TargetInvocationException e) {
            return e.InnerException?.Message ?? e.Message;
        }
    }
}