using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MapsterChecker.Analyzer;

/// <summary>
/// Registry that stores discovered custom Mapster mapping configurations.
/// Indexed by source/destination type pairs for efficient lookup during analysis.
/// </summary>
public class MappingConfigurationRegistry
{
    private readonly Dictionary<string, HashSet<CustomMappingInfo>> _typeMappings = new();
    private readonly Dictionary<string, Dictionary<string, CustomMappingInfo>> _propertyMappings = new();

    /// <summary>
    /// Checks if a custom mapping configuration exists for the given source and destination types.
    /// </summary>
    /// <param name="sourceType">The source type</param>
    /// <param name="destinationType">The destination type</param>
    /// <returns>True if a custom mapping exists</returns>
    public bool HasCustomMapping(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        var key = GetTypeKey(sourceType, destinationType);
        return _typeMappings.ContainsKey(key);
    }

    /// <summary>
    /// Checks if a custom property mapping exists for a specific property.
    /// </summary>
    /// <param name="sourceType">The source type</param>
    /// <param name="destinationType">The destination type</param>
    /// <param name="propertyName">The property name</param>
    /// <returns>True if a custom property mapping exists</returns>
    public bool HasPropertyMapping(ITypeSymbol sourceType, ITypeSymbol destinationType, string propertyName)
    {
        var typeKey = GetTypeKey(sourceType, destinationType);
        return _propertyMappings.TryGetValue(typeKey, out var properties) &&
               properties.ContainsKey(propertyName);
    }

    /// <summary>
    /// Gets the custom mapping information for a specific property.
    /// </summary>
    /// <param name="sourceType">The source type</param>
    /// <param name="destinationType">The destination type</param>
    /// <param name="propertyName">The property name</param>
    /// <returns>Custom mapping information if found, null otherwise</returns>
    public CustomMappingInfo? GetPropertyMapping(ITypeSymbol sourceType, ITypeSymbol destinationType, string propertyName)
    {
        var typeKey = GetTypeKey(sourceType, destinationType);
        if (_propertyMappings.TryGetValue(typeKey, out var properties) &&
            properties.TryGetValue(propertyName, out var mapping))
        {
            return mapping;
        }
        return null;
    }

    /// <summary>
    /// Registers a new custom mapping configuration.
    /// </summary>
    /// <param name="sourceType">The source type</param>
    /// <param name="destinationType">The destination type</param>
    /// <param name="mappingInfo">The custom mapping information</param>
    public void RegisterMapping(ITypeSymbol sourceType, ITypeSymbol destinationType, CustomMappingInfo mappingInfo)
    {
        var typeKey = GetTypeKey(sourceType, destinationType);
        
        // Register at type level
        if (!_typeMappings.ContainsKey(typeKey))
        {
            _typeMappings[typeKey] = new HashSet<CustomMappingInfo>();
        }
        _typeMappings[typeKey].Add(mappingInfo);

        // Register property-level mappings
        if (mappingInfo.MappingType == CustomMappingType.PropertyMapping && !string.IsNullOrEmpty(mappingInfo.PropertyName))
        {
            if (!_propertyMappings.ContainsKey(typeKey))
            {
                _propertyMappings[typeKey] = new Dictionary<string, CustomMappingInfo>();
            }
            _propertyMappings[typeKey][mappingInfo.PropertyName!] = mappingInfo;
        }
    }

    /// <summary>
    /// Gets all custom mappings for debugging and testing purposes.
    /// </summary>
    /// <returns>All registered custom mappings</returns>
    public IEnumerable<(string TypeKey, IEnumerable<CustomMappingInfo> Mappings)> GetAllMappings()
    {
        return _typeMappings.Select(kvp => (kvp.Key, kvp.Value.AsEnumerable()));
    }

    /// <summary>
    /// Clears all registered mappings (useful for testing).
    /// </summary>
    public void Clear()
    {
        _typeMappings.Clear();
        _propertyMappings.Clear();
    }

    private string GetTypeKey(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        return $"{sourceType.ToDisplayString()}→{destinationType.ToDisplayString()}";
    }
}

/// <summary>
/// Contains information about a custom Mapster mapping configuration.
/// </summary>
public class CustomMappingInfo
{
    /// <summary>
    /// Gets or sets the type of custom mapping.
    /// </summary>
    public CustomMappingType MappingType { get; set; }

    /// <summary>
    /// Gets or sets the property name for property-level mappings.
    /// </summary>
    public string? PropertyName { get; set; }

    /// <summary>
    /// Gets or sets the custom mapping expression syntax.
    /// </summary>
    public ExpressionSyntax? MappingExpression { get; set; }

    /// <summary>
    /// Gets or sets the destination property expression syntax.
    /// </summary>
    public ExpressionSyntax? DestinationExpression { get; set; }

    /// <summary>
    /// Gets or sets the source mapping expression syntax.
    /// </summary>
    public ExpressionSyntax? SourceExpression { get; set; }

    /// <summary>
    /// Gets or sets the semantic model for expression analysis.
    /// </summary>
    public SemanticModel? SemanticModel { get; set; }

    /// <summary>
    /// Gets or sets the location of the mapping configuration for diagnostics.
    /// </summary>
    public Location? Location { get; set; }

    /// <summary>
    /// Gets or sets additional configuration details.
    /// </summary>
    public string? AdditionalInfo { get; set; }

    public override bool Equals(object? obj)
    {
        return obj is CustomMappingInfo other &&
               MappingType == other.MappingType &&
               PropertyName == other.PropertyName;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 23 + MappingType.GetHashCode();
            hash = hash * 23 + (PropertyName?.GetHashCode() ?? 0);
            return hash;
        }
    }
}

/// <summary>
/// Defines the types of custom Mapster mappings that can be configured.
/// </summary>
public enum CustomMappingType
{
    /// <summary>
    /// Property-level mapping using .Map(dest => dest.Property, src => expression).
    /// </summary>
    PropertyMapping,

    /// <summary>
    /// Property ignore using .Ignore(dest => dest.Property).
    /// </summary>
    PropertyIgnore,

    /// <summary>
    /// Constructor-based mapping using .MapWith(src => new Destination(...)).
    /// </summary>
    ConstructorMapping,

    /// <summary>
    /// Conditional mapping using .Map(dest => dest.Property, src => expression, condition).
    /// </summary>
    ConditionalMapping,

    /// <summary>
    /// Global settings configuration.
    /// </summary>
    GlobalSettings
}