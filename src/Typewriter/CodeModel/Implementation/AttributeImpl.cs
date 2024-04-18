using System;
using System.Collections.Generic;
using System.Linq;
using Typewriter.CodeModel.Collections;
using Typewriter.Configuration;
using Typewriter.Metadata.Interfaces;
using static Typewriter.CodeModel.Helpers;

namespace Typewriter.CodeModel.Implementation
{
    public sealed class AttributeImpl : Attribute
    {
        private readonly IAttributeMetadata _metadata;

        private AttributeImpl(IAttributeMetadata metadata, Item parent, Settings settings)
        {
            _metadata = metadata;
            Parent = parent;
            Settings = settings;
        }

        public Settings Settings { get; }

        public override Item Parent { get; }

        public override string name => CamelCase(_metadata.Name.TrimStart('@'));

        public override string Name => _metadata.Name.TrimStart('@');

        public override string FullName => _metadata.FullName;

        public override string AssemblyName => _metadata.AssemblyName;

        public override string Value => GetValue(_metadata.Value);

        public override Type Type => TypeImpl.FromMetadata(_metadata.Type, Parent, Settings);

        private IAttributeArgumentCollection _arguments;

        public override IAttributeArgumentCollection Arguments => _arguments ?? (_arguments = AttributeArgumentImpl.FromMetadata(_metadata.Arguments, this, Settings));

        private static string GetValue(string value)
        {
            if (value == null)
            {
                return null;
            }

            if (value.StartsWith("\"", StringComparison.OrdinalIgnoreCase) && value.EndsWith("\"", StringComparison.OrdinalIgnoreCase))
            {
                var trimmed = value.Substring(1, value.Length - 2);

                if (!trimmed.Replace("\\\"", string.Empty).Contains("\""))
                {
                    return trimmed;
                }
            }

            return value;
        }

        public override string ToString()
        {
            return Name;
        }

        public static IAttributeCollection FromMetadata(IEnumerable<IAttributeMetadata> metadata, Item parent, Settings settings)
        {
            return new AttributeCollectionImpl(metadata.Select(a => new AttributeImpl(a, parent, settings)));
        }
    }
}