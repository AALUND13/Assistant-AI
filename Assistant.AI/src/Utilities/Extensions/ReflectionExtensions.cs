using AssistantAI.Utilities.Attributes;
using Newtonsoft.Json.Schema;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace AssistantAI.Utilities.Extension;

public class SchemaOptions {
    public bool AddDefaultDescription { get; set; } = true;
    public List<Type> IgnoredTypes { get; set; } = new List<Type>();
}

public static class ReflectionExtensions {
    /// <summary>
    /// Generates a JSON schema from the properties of the specified .NET type.
    /// </summary>
    /// <param name="type">The .NET type to generate the schema from.</param>
    /// <returns>A <see cref="JSchema"/> object representing the JSON schema of the type.</returns>
    public static JSchema GetJsonSchemaFromType(this Type type, SchemaOptions? schemaOptions = null) {
        schemaOptions ??= new SchemaOptions();

        var schema = new JSchema {
            Type = JSchemaType.Object,
            Description = GetDescription(type, schemaOptions),
            AllowAdditionalProperties = false
        };

        foreach(PropertyInfo property in type.GetProperties()) {
            if(property.GetCustomAttribute<IgnoreAttribute>() != null)
                continue;
            else if(schemaOptions.IgnoredTypes.Contains(property.PropertyType))
                continue;

            var propertySchema = new JSchema {
                Description = GetDescription(type, schemaOptions),
                Type = GetSchemaTypeFromType(property.PropertyType)
            };

            // Check if the property is an object and generate a schema for it
            if(propertySchema.Type == JSchemaType.Object) {
                propertySchema = GetJsonSchemaFromType(property.PropertyType);
                propertySchema.AllowAdditionalProperties = false;
            } else if(propertySchema.Type == JSchemaType.Array) {
                propertySchema.Items.Add(GetJsonSchemaFromType(property.PropertyType.GetElementType()!, schemaOptions));
            }
            schema.Properties.Add(property.Name, propertySchema);
            if(property.GetCustomAttribute<RequiredAttribute>() != null) {

                schema.Required.Add(property.Name);
            }
        }

        return schema;
    }

    /// <summary>
    /// Generates a JSON schema from the given method parameter.
    /// </summary>
    /// <param name="parameter">The parameter to generate the schema from.</param>
    /// <returns>A <see cref="JSchema"/> object representing the JSON schema of the parameter.</returns>
    public static JSchema GetJsonSchemaFromParameter(this ParameterInfo parameter, SchemaOptions schemaOptions) {
        var jSchemaType = GetSchemaTypeFromType(parameter.ParameterType);

        var schema = new JSchema {
            Description = GetDescription(parameter, schemaOptions),
            Type = jSchemaType
        };

        if(jSchemaType == JSchemaType.Object) {
            return GetJsonSchemaFromType(parameter.ParameterType);
        }

        return schema;
    }

    /// <summary>
    /// Generates a JSON schema from the parameters of the specified method.
    /// </summary>
    /// <param name="method">The method to generate the schema from.</param>
    /// <returns>A <see cref="JSchema"/> object representing the JSON schema of the method.</returns>
    public static JSchema GetJsonSchemaFromMethod(this MethodInfo method, SchemaOptions schemaOptions) {
        var schema = new JSchema {
            Type = JSchemaType.Object,
            Description = GetDescription(method, schemaOptions),
        };

        foreach(ParameterInfo parameter in method.GetParameters()) {
            if(parameter.GetCustomAttribute<IgnoreAttribute>() != null)
                continue;
            else if(schemaOptions.IgnoredTypes.Contains(parameter.ParameterType))
                continue;

            var parameterSchema = GetJsonSchemaFromParameter(parameter, schemaOptions);

            schema.Properties.Add(parameter.Name!, parameterSchema);
            if(!parameter.IsOptional) {
                schema.Required.Add(parameter.Name!);
            }
        }

        return schema;
    }

    /// <summary>
    /// Determines the JSON schema type corresponding to a .NET type.
    /// </summary>
    /// <param name="type">The .NET type to determine the JSON schema type for.</param>
    /// <returns>The <see cref="JSchemaType"/> that corresponds to the .NET type.</returns>
    /// <exception cref="NotSupportedException">Thrown if the type is not supported for conversion to a JSON schema type.</exception>
    public static JSchemaType GetSchemaTypeFromType(Type type) {
        var typeMappings = new Dictionary<Type, JSchemaType>
        {
            { typeof(string), JSchemaType.String },
            { typeof(int), JSchemaType.Integer },
            { typeof(bool), JSchemaType.Boolean },
            { typeof(double), JSchemaType.Number },
            { typeof(float), JSchemaType.Number },
            { typeof(decimal), JSchemaType.Number },
            { typeof(ulong), JSchemaType.Number }
        };

        if(type.IsArray || typeof(System.Collections.IEnumerable).IsAssignableFrom(type) && type != typeof(string)) {
            return JSchemaType.Array;
        }

        if(typeMappings.TryGetValue(type, out JSchemaType schemaType)) {
            return schemaType;
        }

        if(type.IsClass && type != typeof(string) && type != typeof(Newtonsoft.Json.Linq.JObject) && type != typeof(Newtonsoft.Json.Linq.JArray)) {
            return JSchemaType.Object;
        }

        throw new NotSupportedException($"Type {type.Name} is not supported.");
    }



    private static string? GetDescription(MemberInfo member, SchemaOptions schemaOptions) {
        return member.GetCustomAttribute<DescriptionAttribute>()?.Description ?? (schemaOptions.AddDefaultDescription ? "No description provided." : null);
    }

    private static string? GetDescription(ParameterInfo member, SchemaOptions schemaOptions) {
        return member.GetCustomAttribute<DescriptionAttribute>()?.Description ?? (schemaOptions.AddDefaultDescription ? "No description provided." : null);
    }

}
