using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Vogen;

internal class WriteSwashbuckleSchemaRelatedCode
{
    public static void WriteIfNeeded(VogenConfiguration? globalConfig,
        SourceProductionContext context,
        Compilation compilation,
        List<VoWorkItem> workItems)
    {
        var c = globalConfig?.SwashbuckleSchemaGeneration ?? VogenConfiguration.DefaultInstance.SwashbuckleSchemaGeneration;

        if (c == SwashbuckleSchemaGeneration.GenerateSchemaFilter)
        {
            WriteSchemaFilter(context, compilation);
            return;
        }

        if (c == SwashbuckleSchemaGeneration.GenerateExtensionMethodToMapTypesOnSwaggerGenOptions)
        {
            WriteExtensionMethodMapping(context, compilation, workItems);
            return;
        }

        return;
    }

    private static void WriteSchemaFilter(SourceProductionContext context, Compilation compilation)
    {
        if (compilation.GetTypeByMetadataName("Swashbuckle.AspNetCore.SwaggerGen.ISchemaFilter") is null)
        {
            return;
        }
        
        string s2 =
            $$"""

              {{GeneratedCodeSegments.Preamble}}

              using System.Reflection;
              
              public class VogenSchemaFilter : global::Swashbuckle.AspNetCore.SwaggerGen.ISchemaFilter
              {                                
                  private const BindingFlags _flags = BindingFlags.Public | BindingFlags.Instance;

                  public void Apply(global::Microsoft.OpenApi.Models.OpenApiSchema schema, global::Swashbuckle.AspNetCore.SwaggerGen.SchemaFilterContext context)
                  {
                      if (context.Type.GetCustomAttribute<Vogen.ValueObjectAttribute>() is not { } attribute)
                          return;
              
                      var type = attribute.GetType();
                      if (!type.IsGenericType || type.GenericTypeArguments.Length != 1)
                      {
                          return;
                      }
              
                      var schemaValueObject = context.SchemaGenerator.GenerateSchema(
                          type.GenericTypeArguments[0], 
                          context.SchemaRepository, 
                          context.MemberInfo, context.ParameterInfo);
                      
                      TryCopyPublicProperties(schemaValueObject, schema);
                  }
              
                  private static void TryCopyPublicProperties<T>(T oldObject, T newObject) where T : class
                  {
                      if (ReferenceEquals(oldObject, newObject))
                      {
                          return;
                      }
              
                      var type = typeof(T);
                      
                      var propertyList = type.GetProperties(_flags);
                      
                      if (propertyList.Length <= 0)
                      {
                          return;
                      }
              
                      foreach (var newObjProp in propertyList)
                      {
                          var oldProp = type.GetProperty(newObjProp.Name, _flags)!;
                          
                          if (!oldProp.CanRead || !newObjProp.CanWrite)
                          {
                              continue;
                          }
              
                          var value = oldProp.GetValue(oldObject);
                          newObjProp.SetValue(newObject, value);
                      }
                  }
              }
              """;

        context.AddSource("SwashbuckleSchemaFilter_g.cs", s2);
    }

    private static void WriteExtensionMethodMapping(
        SourceProductionContext context,
        Compilation compilation,
        List<VoWorkItem> workItems)
    {
        if (compilation.GetTypeByMetadataName("Swashbuckle.AspNetCore.SwaggerGen.ISchemaFilter") is null)
        {
            return;
        }
        
        string s2 =
            $$"""

              {{GeneratedCodeSegments.Preamble}}

              public static class VogenSwashbuckleExtensions
              {
                  public static global::Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions MapVogenTypes(this global::Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions o)
                  {
                      {{MapWorkItems(workItems)}}
                      //                      o.MapType<CustomerName>(() => new OpenApiSchema { Type = "string" });
                      //                      o.MapType<OrderId>(() => new OpenApiSchema { Type = "integer" });
                      //                      o.MapType<Centigrade>(() => new OpenApiSchema { Type = "integer" });
                      //                      o.MapType<Farenheit>(() => new OpenApiSchema { Type = "number" });
                      //                      o.MapType<City>(() => new OpenApiSchema { Description = "The description of a City", Type = "string" });
              
                      return o;
                  }
              }
              """;

        context.AddSource("SwashbuckleSchemaExtensions_g.cs", s2);
    }

    private static string MapWorkItems(List<VoWorkItem> workItems)
    {
        var workItemCode = new StringBuilder();
        foreach (var workItem in workItems)
        {
            workItemCode.AppendLine(
                $$"""global::Microsoft.Extensions.DependencyInjection.SwaggerGenOptionsExtensions.MapType<{{workItem.VoTypeName}}>(o, () => new global::Microsoft.OpenApi.Models.OpenApiSchema { Type = "{{MapUnderlyingTypeToJsonSchema(workItem.UnderlyingTypeFullName)}}" });""");
        }

        return workItemCode.ToString();
        
    }

    private static string MapUnderlyingTypeToJsonSchema(string primitiveType)
    {
        string jsonType = primitiveType switch
        {
            "System.Int32" => "integer",
            "System.Single" => "number",
            "System.Decimal" => "number",
            "System.Double" => "number",
            "System.String" => "string",
            "System.Boolean" => "boolean",
            _ => "object"
        };

        return jsonType;
    }
}