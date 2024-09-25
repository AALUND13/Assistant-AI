using Newtonsoft.Json.Schema;
using OpenAI.Chat;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;

namespace AssistantAI.Services
{
    public class ChatToolService
    {
        public readonly Dictionary<Delegate, JSchema> ToolFunctions = new Dictionary<Delegate, JSchema>();
        public List<ChatTool> ChatTools { get => ToolFunctions.Keys.Select(GetToolFunctions).ToList(); }

        public void AddToolFunction(Delegate delegateMethod)
        {
            if(ToolFunctions.ContainsKey(delegateMethod))
                return;

            JSchema methodJsonSchema = delegateMethod.GetMethodInfo().GetJsonSchemaFromMethod();
            ToolFunctions.Add(delegateMethod, methodJsonSchema);
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



        public string? CallToolFunction(ChatToolCall toolCall)
        {
            // Find the method in the ToolFunctions dictionary based on the function name
            Delegate? method = ToolFunctions.Keys.FirstOrDefault(m => m.GetMethodInfo().Name == toolCall.FunctionName);

            if (method == null)
                throw new ArgumentException($"No tool function with the name '{toolCall.FunctionName}' was found.");

            // Parse the JSON containing function arguments
            using JsonDocument jsonDocument = JsonDocument.Parse(toolCall.FunctionArguments);

            ParameterInfo[] parameters = method.GetMethodInfo().GetParameters();
            object?[] arguments = new object?[parameters.Length];

            // Iterate over the method parameters and JSON properties to match and deserialize them
            foreach (JsonProperty property in jsonDocument.RootElement.EnumerateObject())
            {
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (parameters[i].Name == property.Name)
                    {
                        // Deserialize JSON value to the correct type of the method parameter
                        arguments[i] = JsonSerializer.Deserialize(property.Value.GetRawText(), parameters[i].ParameterType);
                        break;
                    }
                }
            }

            string? result;
            try {
                // Invoke the method with the deserialized arguments
                result = method.DynamicInvoke(arguments)?.ToString();
            } catch(Exception e) {
                result = e.Message;
            }

            return result;
        }
    }
}
