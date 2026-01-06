# Mapster Checker

I need to make a tool that will check the Mapster method invocation for type conflicts.

## Background
In my project im using Mapster's (https://github.com/MapsterMapper/Mapster) `src.Adapt(dest)` and `src.Adapt<dest>()` methods to map between different objects. However, since there is no compile time check fot mappings, I need to somehow ensure that the types are compatible. One of the main problems is mapping nullable types to non-nullable types, wich will result in nasty things. And I need to chatch these probems at build time.

## Requirements
- The tool should be able to analyze the code and find all `Adapt` method invocations
- It should check if the source and destination types are compatible
- If there are any conflicts, it should report them with a clear message
- The tool should be able to run as part of the build process
- It should be easy to integrate into existing projects
- The tool should be able to handle both `src.Adapt(dest)` and `src.Adapt<dest>()` method invocations
- It should be able to handle complex types and collections
- The tool should run on .NET 9, .NET 10, and later versions
