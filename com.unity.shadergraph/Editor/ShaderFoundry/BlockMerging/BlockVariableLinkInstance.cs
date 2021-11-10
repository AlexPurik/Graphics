using System.Collections.Generic;
using System.Diagnostics;

namespace UnityEditor.ShaderFoundry
{
    internal class FieldOverride
    {
        internal string Name;
        internal string Alias;
    }

    /// Represents a block variable instance. A variable might have an owner (a variable in a sub-class instance).
    /// The resolved fields are also tracked so each instance knows what other variables it's connected to.
    [DebuggerDisplay("{Type.Name} {ReferenceName}")]
    internal class BlockVariableLinkInstance
    {
        // If this is a field in another type, then this is the owning instance.
        internal BlockVariableLinkInstance Owner;
        internal ShaderType Type;
        internal string ReferenceName;
        internal string DisplayName;
        internal string DefaultExpression;
        internal List<ShaderAttribute> Attributes = new List<ShaderAttribute>();
        internal List<FieldOverride> FieldOverrides = new List<FieldOverride>();

        // If this is a non primitive type, this is the field instances for this variable instance.
        // Note: These are not currently populated for all variables, currently only for the block's input/output types.
        List<BlockVariableLinkInstance> fields = new List<BlockVariableLinkInstance>();
        // Reference name of available field to a match.
        Dictionary<string, ResolvedFieldMatch> resolvedFieldMatches = new Dictionary<string, ResolvedFieldMatch>();

        internal static BlockVariableLinkInstance Construct(BlockVariable variable, BlockVariableLinkInstance owner, IEnumerable<ShaderAttribute> attributes = null)
        {
            return Construct(variable.Type, variable.ReferenceName, variable.DisplayName, variable.DefaultExpression, owner, attributes);
        }

        internal static BlockVariableLinkInstance Construct(BlockVariableLinkInstance variable, string newName, BlockVariableLinkInstance owner)
        {
            return Construct(variable.Type, newName, newName, variable.DefaultExpression, owner, variable.Attributes);
        }

        internal static BlockVariableLinkInstance Construct(BlockVariableLinkInstance variable, string referenceName, string displayName, BlockVariableLinkInstance owner)
        {
            return Construct(variable.Type, referenceName, displayName, variable.DefaultExpression, owner, variable.Attributes);
        }

        internal static BlockVariableLinkInstance Construct(ShaderType type, string referenceName, string displayName, string defaultExpression, BlockVariableLinkInstance owner, IEnumerable<ShaderAttribute> attributes = null)
        {
            var result = new BlockVariableLinkInstance
            {
                Type = type,
                ReferenceName = referenceName,
                DisplayName = displayName,
                DefaultExpression = defaultExpression,
                Owner = owner,
            };
            if (attributes != null)
            {
                foreach (var attribute in attributes)
                    result.Attributes.Add(attribute);
            }
            return result;
        }

        internal IEnumerable<BlockVariableLinkInstance> Fields => fields;
        internal IEnumerable<ResolvedFieldMatch> ResolvedFieldMatches => resolvedFieldMatches.Values;
        internal IEnumerable<BlockVariableLinkInstance> ResolvedFields
        {
            get
            {
                // This is in field order so as to avoid being non-deterministic in ordering
                foreach (var field in Fields)
                {
                    if (FindResolvedField(field.ReferenceName) != null)
                        yield return field;
                }
            }
        }

        internal void AddField(BlockVariableLinkInstance field)
        {
            fields.Add(field);
        }

        internal BlockVariableLinkInstance FindField(string name)
        {
            return fields.Find((f) => (f.ReferenceName == name));
        }

        internal void AddResolvedField(string name, ResolvedFieldMatch resolvedMatch)
        {
            resolvedFieldMatches[name] = resolvedMatch;
        }

        internal ResolvedFieldMatch FindResolvedField(string name)
        {
            resolvedFieldMatches.TryGetValue(name, out var result);
            return result;
        }

        internal BlockVariable Build(ShaderContainer container)
        {
            var blockVariableBuilder = new BlockVariable.Builder(container);
            blockVariableBuilder.Type = Type;
            blockVariableBuilder.ReferenceName = ReferenceName;
            blockVariableBuilder.DisplayName = DisplayName;
            blockVariableBuilder.DefaultExpression = DefaultExpression;
            foreach (var attribute in Attributes)
                blockVariableBuilder.AddAttribute(attribute);
            return blockVariableBuilder.Build();
        }

        internal void AddAlias(string name, string alias)
        {
            FieldOverrides.Add(new FieldOverride{ Name = name, Alias = alias });
        }
    }
}
