using AssistantAI.AiModule.Utilities.Extension;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using OpenAI.Chat;
using System.ComponentModel;
using System.Reflection;

namespace AssistantAI.AiModule.Utilities;

public class ToolsFunctionsBuilder<T> {
    public readonly Dictionary<Delegate, JSchema> ToolFunctions = new Dictionary<Delegate, JSchema>();
    public ToolsFunctionsBuilder<T> WithToolFunction(Delegate delegateMethod) {
        if(ToolFunctions.ContainsKey(delegateMethod))
            return this;

        SchemaOptions schemaOptions = new SchemaOptions {
            AddDefaultDescription = true,
            IgnoredTypes = new List<Type> { typeof(T) }
        };

        JSchema methodJsonSchema = delegateMethod.GetMethodInfo().GetJsonSchemaFromMethod(schemaOptions);
        ToolFunctions.Add(delegateMethod, methodJsonSchema);

        return this;
    }
}

public class ToolsFunctions<T> {
    private readonly ToolsFunctionsBuilder<T> builder;
    public List<ChatTool> ChatTools { get => builder.ToolFunctions.Keys.Select(GetToolFunctions).ToList(); }

    public ToolsFunctions(ToolsFunctionsBuilder<T> builder) {
        this.builder = builder;
    }

    public ChatTool GetToolFunctions(Delegate delegateMethod) {
        SchemaOptions schemaOptions = new SchemaOptions {
            AddDefaultDescription = true,
            IgnoredTypes = new List<Type> { typeof(T) }
        };

        JSchema methodJsonSchema = delegateMethod.GetMethodInfo().GetJsonSchemaFromMethod(schemaOptions);
        string methodDescription = delegateMethod.GetMethodInfo().GetCustomAttribute<DescriptionAttribute>()?.Description ?? "No description provided.";

        return ChatTool.CreateFunctionTool(
            functionName: delegateMethod.GetMethodInfo().Name,
            functionDescription: methodDescription,
            functionParameters: BinaryData.FromString(methodJsonSchema.ToString())
        );
    }

    public object? CallToolFunction(ChatToolCall toolCall, T option) {
        Delegate? method = builder.ToolFunctions.Keys.FirstOrDefault(m => m.GetMethodInfo().Name == toolCall.FunctionName);

        if(method == null)
            throw new ArgumentException($"No tool function with the name '{toolCall.FunctionName}' was found.");

        JObject arguments = JObject.Parse(toolCall.FunctionArguments.ToString());
        ParameterInfo[] parameters = method.GetMethodInfo().GetParameters();

        object?[] args = new object?[parameters.Length];

        foreach(var param in parameters) {
            JToken? argToken = arguments[param.Name!];

            if(argToken != null) {
                args[Array.IndexOf(parameters, param)] = argToken.ToObject(param.ParameterType);
            }

            if(typeof(T).IsAssignableFrom(param.ParameterType)) {
                args[Array.IndexOf(parameters, param)] = option;
            }
        }

        try {
            return method.DynamicInvoke(args);
        } catch(TargetInvocationException e) {
            return e.InnerException?.Message ?? e.Message;
        }
    }
}