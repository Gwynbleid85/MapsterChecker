using Microsoft.CodeAnalysis;

namespace MapsterChecker.Analyzer;

public static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor NullableToNonNullableMapping = new(
        id: "MAPSTER001",
        title: "Nullable to non-nullable mapping",
        messageFormat: "Mapping from nullable type '{0}' to non-nullable type '{1}' may result in null reference exceptions",
        category: "MapsterChecker.Nullability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Mapster.Adapt call maps from a nullable type to a non-nullable type, which may cause runtime null reference exceptions if the source value is null.");

    public static readonly DiagnosticDescriptor IncompatibleTypeMapping = new(
        id: "MAPSTER002",
        title: "Incompatible type mapping",
        messageFormat: "Cannot map from type '{0}' to incompatible type '{1}'",
        category: "MapsterChecker.Compatibility",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Mapster.Adapt call attempts to map between fundamentally incompatible types that cannot be converted.");

    public static readonly DiagnosticDescriptor MissingPropertyMapping = new(
        id: "MAPSTER003",
        title: "Missing property mapping",
        messageFormat: "Property '{0}' in destination type '{1}' has no corresponding property in source type '{2}'",
        category: "MapsterChecker.Mapping",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Destination type contains properties that are not present in the source type and will not be mapped by Mapster.Adapt.");

    public static readonly DiagnosticDescriptor PropertyNullableToNonNullableMapping = new(
        id: "MAPSTER001P",
        title: "Property nullable to non-nullable mapping",
        messageFormat: "Property '{0}' maps from nullable type '{1}' to non-nullable type '{2}' which may result in null reference exceptions",
        category: "MapsterChecker.PropertyNullability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Property in Mapster.Adapt call maps from a nullable type to a non-nullable type, which may cause runtime null reference exceptions if the source property value is null.");

    public static readonly DiagnosticDescriptor PropertyIncompatibleTypeMapping = new(
        id: "MAPSTER002P",
        title: "Property incompatible type mapping",
        messageFormat: "Property '{0}' cannot map from type '{1}' to incompatible type '{2}'",
        category: "MapsterChecker.PropertyCompatibility",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Property in Mapster.Adapt call attempts to map between fundamentally incompatible types that cannot be converted.");

    public static readonly DiagnosticDescriptor PropertyMissingMapping = new(
        id: "MAPSTER003P",
        title: "Missing source property for mapping",
        messageFormat: "Property '{0}' in destination type has no corresponding property in source type",
        category: "MapsterChecker.PropertyMapping",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Destination property has no corresponding source property and will not be mapped by Mapster.Adapt, potentially leaving it with default values.");
}