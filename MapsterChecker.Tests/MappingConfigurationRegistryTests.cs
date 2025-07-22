using MapsterChecker.Analyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using Xunit;

namespace MapsterChecker.Tests;

/// <summary>
/// Unit tests for the MappingConfigurationRegistry class.
/// Tests the core functionality of storing and retrieving custom mapping configurations.
/// </summary>
public class MappingConfigurationRegistryTests
{
    [Fact]
    public void RegisterMapping_ShouldStoreMapping()
    {
        // Arrange
        var registry = new MappingConfigurationRegistry();
        var sourceType = CreateTestType("Person");
        var destType = CreateTestType("PersonDto");
        var mappingInfo = new CustomMappingInfo
        {
            MappingType = CustomMappingType.PropertyMapping,
            PropertyName = "Id"
        };

        // Act
        registry.RegisterMapping(sourceType, destType, mappingInfo);

        // Assert
        Assert.True(registry.HasCustomMapping(sourceType, destType));
        Assert.True(registry.HasPropertyMapping(sourceType, destType, "Id"));
    }

    [Fact]
    public void HasCustomMapping_WithNoMapping_ShouldReturnFalse()
    {
        // Arrange
        var registry = new MappingConfigurationRegistry();
        var sourceType = CreateTestType("Person");
        var destType = CreateTestType("PersonDto");

        // Act & Assert
        Assert.False(registry.HasCustomMapping(sourceType, destType));
    }

    [Fact]
    public void HasPropertyMapping_WithNoMapping_ShouldReturnFalse()
    {
        // Arrange
        var registry = new MappingConfigurationRegistry();
        var sourceType = CreateTestType("Person");
        var destType = CreateTestType("PersonDto");

        // Act & Assert
        Assert.False(registry.HasPropertyMapping(sourceType, destType, "Id"));
    }

    [Fact]
    public void GetPropertyMapping_WithExistingMapping_ShouldReturnMapping()
    {
        // Arrange
        var registry = new MappingConfigurationRegistry();
        var sourceType = CreateTestType("Person");
        var destType = CreateTestType("PersonDto");
        var mappingInfo = new CustomMappingInfo
        {
            MappingType = CustomMappingType.PropertyMapping,
            PropertyName = "Id",
            AdditionalInfo = "test mapping"
        };

        registry.RegisterMapping(sourceType, destType, mappingInfo);

        // Act
        var retrievedMapping = registry.GetPropertyMapping(sourceType, destType, "Id");

        // Assert
        Assert.NotNull(retrievedMapping);
        Assert.Equal(CustomMappingType.PropertyMapping, retrievedMapping.MappingType);
        Assert.Equal("Id", retrievedMapping.PropertyName);
        Assert.Equal("test mapping", retrievedMapping.AdditionalInfo);
    }

    [Fact]
    public void GetPropertyMapping_WithNonExistentMapping_ShouldReturnNull()
    {
        // Arrange
        var registry = new MappingConfigurationRegistry();
        var sourceType = CreateTestType("Person");
        var destType = CreateTestType("PersonDto");

        // Act
        var retrievedMapping = registry.GetPropertyMapping(sourceType, destType, "Id");

        // Assert
        Assert.Null(retrievedMapping);
    }

    [Fact]
    public void RegisterMapping_MultipleProperties_ShouldStoreAll()
    {
        // Arrange
        var registry = new MappingConfigurationRegistry();
        var sourceType = CreateTestType("Person");
        var destType = CreateTestType("PersonDto");
        
        var idMapping = new CustomMappingInfo
        {
            MappingType = CustomMappingType.PropertyMapping,
            PropertyName = "Id"
        };
        
        var nameMapping = new CustomMappingInfo
        {
            MappingType = CustomMappingType.PropertyMapping,
            PropertyName = "Name"
        };

        var ignoreMapping = new CustomMappingInfo
        {
            MappingType = CustomMappingType.PropertyIgnore,
            PropertyName = "IgnoredField"
        };

        // Act
        registry.RegisterMapping(sourceType, destType, idMapping);
        registry.RegisterMapping(sourceType, destType, nameMapping);
        registry.RegisterMapping(sourceType, destType, ignoreMapping);

        // Assert
        Assert.True(registry.HasCustomMapping(sourceType, destType));
        Assert.True(registry.HasPropertyMapping(sourceType, destType, "Id"));
        Assert.True(registry.HasPropertyMapping(sourceType, destType, "Name"));
        Assert.True(registry.HasPropertyMapping(sourceType, destType, "IgnoredField"));
    }

    [Fact]
    public void RegisterMapping_DifferentTypePairs_ShouldStoreIndependently()
    {
        // Arrange
        var registry = new MappingConfigurationRegistry();
        var personType = CreateTestType("Person");
        var personDtoType = CreateTestType("PersonDto");
        var orderType = CreateTestType("Order");
        var orderDtoType = CreateTestType("OrderDto");
        
        var personMapping = new CustomMappingInfo
        {
            MappingType = CustomMappingType.PropertyMapping,
            PropertyName = "Id"
        };
        
        var orderMapping = new CustomMappingInfo
        {
            MappingType = CustomMappingType.PropertyMapping,
            PropertyName = "OrderId"
        };

        // Act
        registry.RegisterMapping(personType, personDtoType, personMapping);
        registry.RegisterMapping(orderType, orderDtoType, orderMapping);

        // Assert
        Assert.True(registry.HasCustomMapping(personType, personDtoType));
        Assert.True(registry.HasCustomMapping(orderType, orderDtoType));
        Assert.True(registry.HasPropertyMapping(personType, personDtoType, "Id"));
        Assert.True(registry.HasPropertyMapping(orderType, orderDtoType, "OrderId"));
        
        // Cross-checks should return false
        Assert.False(registry.HasPropertyMapping(personType, personDtoType, "OrderId"));
        Assert.False(registry.HasPropertyMapping(orderType, orderDtoType, "Id"));
    }

    [Fact]
    public void Clear_ShouldRemoveAllMappings()
    {
        // Arrange
        var registry = new MappingConfigurationRegistry();
        var sourceType = CreateTestType("Person");
        var destType = CreateTestType("PersonDto");
        var mappingInfo = new CustomMappingInfo
        {
            MappingType = CustomMappingType.PropertyMapping,
            PropertyName = "Id"
        };

        registry.RegisterMapping(sourceType, destType, mappingInfo);
        Assert.True(registry.HasCustomMapping(sourceType, destType));

        // Act
        registry.Clear();

        // Assert
        Assert.False(registry.HasCustomMapping(sourceType, destType));
        Assert.False(registry.HasPropertyMapping(sourceType, destType, "Id"));
    }

    [Fact]
    public void GetAllMappings_ShouldReturnAllRegisteredMappings()
    {
        // Arrange
        var registry = new MappingConfigurationRegistry();
        var sourceType = CreateTestType("Person");
        var destType = CreateTestType("PersonDto");
        
        var mapping1 = new CustomMappingInfo
        {
            MappingType = CustomMappingType.PropertyMapping,
            PropertyName = "Id"
        };
        
        var mapping2 = new CustomMappingInfo
        {
            MappingType = CustomMappingType.PropertyIgnore,
            PropertyName = "IgnoredField"
        };

        registry.RegisterMapping(sourceType, destType, mapping1);
        registry.RegisterMapping(sourceType, destType, mapping2);

        // Act
        var allMappings = registry.GetAllMappings().ToList();

        // Assert
        Assert.Single(allMappings); // One type pair
        Assert.Equal(2, allMappings[0].Mappings.Count()); // Two mappings for that pair
    }

    [Fact]
    public void CustomMappingInfo_Equality_ShouldWorkCorrectly()
    {
        // Arrange
        var mapping1 = new CustomMappingInfo
        {
            MappingType = CustomMappingType.PropertyMapping,
            PropertyName = "Id"
        };
        
        var mapping2 = new CustomMappingInfo
        {
            MappingType = CustomMappingType.PropertyMapping,
            PropertyName = "Id"
        };
        
        var mapping3 = new CustomMappingInfo
        {
            MappingType = CustomMappingType.PropertyMapping,
            PropertyName = "Name"
        };

        // Act & Assert
        Assert.Equal(mapping1, mapping2);
        Assert.NotEqual(mapping1, mapping3);
        Assert.Equal(mapping1.GetHashCode(), mapping2.GetHashCode());
    }

    /// <summary>
    /// Helper method to create a mock ITypeSymbol for testing purposes.
    /// In real scenarios, this would come from Roslyn's semantic model.
    /// </summary>
    private static ITypeSymbol CreateTestType(string typeName)
    {
        // Create a minimal compilation to get type symbols
        var code = $"public class {typeName} {{ }}";
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("TestAssembly")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddSyntaxTrees(syntaxTree);

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var classDeclaration = syntaxTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
        var typeSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);

        return typeSymbol!;
    }
}