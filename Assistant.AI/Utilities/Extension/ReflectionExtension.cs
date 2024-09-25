using Newtonsoft.Json.Schema;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

public static class ReflectionExtension {
    /// <summary>
    /// Generates a JSON schema from the properties of the specified .NET type.
    /// </summary>
    /// <param name="type">The .NET type to generate the schema from.</param>
    /// <returns>A <see cref="JSchema"/> object representing the JSON schema of the type.</returns>
    public static JSchema GetJsonSchemaFromType(this Type type) {
        var schema = new JSchema {
            Type = JSchemaType.Object,
            Description = type.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "No description provided."
        };

        foreach(PropertyInfo property in type.GetProperties()) {
            var propertySchema = new JSchema {
                Title = property.Name,
                Description = property.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "No description provided.",
                Type = GetSchemaTypeFromType(property.PropertyType)
            };

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
    public static JSchema GetJsonSchemaFromParameter(this ParameterInfo parameter) {
        var jSchemaType = GetSchemaTypeFromType(parameter.ParameterType);

        var schema = new JSchema {
            Description = parameter.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "No description provided.",
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
    public static JSchema GetJsonSchemaFromMethod(this MethodInfo method) {
        var schema = new JSchema {
            Type = JSchemaType.Object,
            Description = method.GetCustomAttribute<DescriptionAttribute>()?.Description
        };

        foreach(ParameterInfo parameter in method.GetParameters()) {
            var parameterSchema = GetJsonSchemaFromParameter(parameter);

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
}
