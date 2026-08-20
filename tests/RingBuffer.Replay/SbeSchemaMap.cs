using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace RingBuffer.Replay;

/// <summary>A field in a message's fixed block, with its byte offset and width.</summary>
public sealed record SbeField(string Name, string Type, int Offset, int Length);

/// <summary>A message template: its fixed-block layout and total block length.</summary>
public sealed record SbeMessage(int TemplateId, string Name, int BlockLength, IReadOnlyList<SbeField> Fields)
{
    /// <summary>The field covering <paramref name="blockOffset"/>, or null if past the fixed block.</summary>
    public SbeField? FieldAt(int blockOffset) =>
        Fields.FirstOrDefault(f => blockOffset >= f.Offset && blockOffset < f.Offset + f.Length);
}

/// <summary>
/// Reads an SBE XML schema and computes fixed-block field offsets, so the differ can
/// say <em>which field</em> differs rather than only which byte.
///
/// A raw byte offset is useless at 3am. Building this is the difference between a
/// corpus people use and a corpus people mute — which is why task 3.7 exists at all.
///
/// Parsed from the schema rather than from the generated constants, so it cannot
/// drift: change the schema and the differ's names change with it.
/// </summary>
public sealed class SbeSchemaMap
{
    private static readonly Dictionary<string, int> PrimitiveSizes = new(StringComparer.Ordinal)
    {
        ["int8"] = 1, ["uint8"] = 1, ["char"] = 1,
        ["int16"] = 2, ["uint16"] = 2,
        ["int32"] = 4, ["uint32"] = 4, ["float"] = 4,
        ["int64"] = 8, ["uint64"] = 8, ["double"] = 8,
    };

    private readonly Dictionary<int, SbeMessage> _byTemplateId = new();

    public SbeSchemaMap(string schemaPath)
    {
        var doc = XDocument.Load(schemaPath);
        var root = doc.Root ?? throw new InvalidOperationException($"{schemaPath} has no root element");
        var sbe = root.Name.Namespace;

        var typeSizes = BuildTypeSizes(root);

        foreach (var message in root.Elements(sbe + "message"))
        {
            var templateId = int.Parse(Attr(message, "id"), CultureInfo.InvariantCulture);
            var name = Attr(message, "name");

            var fields = new List<SbeField>();
            var offset = 0;
            foreach (var field in message.Elements("field"))
            {
                var typeName = Attr(field, "type");
                if (!typeSizes.TryGetValue(typeName, out var size))
                {
                    // An unknown type means the map is incomplete rather than the
                    // schema wrong. Say so instead of silently mis-numbering every
                    // field after this one.
                    throw new InvalidOperationException(
                        $"cannot size type '{typeName}' used by {name}.{Attr(field, "name")}");
                }

                fields.Add(new SbeField(Attr(field, "name"), typeName, offset, size));
                offset += size;
            }

            _byTemplateId[templateId] = new SbeMessage(templateId, name, offset, fields);
        }
    }

    /// <summary>Header size is fixed by the messageHeader composite: blockLength, templateId, schemaId, version.</summary>
    public int HeaderLength { get; } = 8;

    public SbeMessage? Message(int templateId) =>
        _byTemplateId.TryGetValue(templateId, out var m) ? m : null;

    public IReadOnlyCollection<SbeMessage> Messages => _byTemplateId.Values;

    /// <summary>Names the byte at <paramref name="frameOffset"/> within an SBE frame.</summary>
    public string Describe(int templateId, int frameOffset)
    {
        if (frameOffset < HeaderLength)
        {
            var headerField = frameOffset switch
            {
                < 2 => "messageHeader.blockLength",
                < 4 => "messageHeader.templateId",
                < 6 => "messageHeader.schemaId",
                _ => "messageHeader.version",
            };
            return $"{headerField} (byte {frameOffset} of the header)";
        }

        var message = Message(templateId);
        if (message is null)
        {
            return $"unknown templateId {templateId}, offset {frameOffset}";
        }

        var blockOffset = frameOffset - HeaderLength;
        var field = message.FieldAt(blockOffset);
        if (field is not null)
        {
            return $"{message.Name}.{field.Name} ({field.Type}) at block offset {field.Offset}" +
                   $", byte {blockOffset - field.Offset} of {field.Length}";
        }

        // Past the fixed block: repeating groups and variable-length data, whose
        // offsets depend on the data itself and cannot be resolved statically.
        return $"{message.Name}, {blockOffset - message.BlockLength} byte(s) into the " +
               "groups / var-data region (offsets there are data-dependent)";
    }

    private static Dictionary<string, int> BuildTypeSizes(XElement root)
    {
        var sizes = new Dictionary<string, int>(PrimitiveSizes, StringComparer.Ordinal);
        var types = root.Element("types");
        if (types is null)
        {
            return sizes;
        }

        foreach (var type in types.Elements("type"))
        {
            var primitive = Attr(type, "primitiveType");
            var length = type.Attribute("length") is { } l
                ? int.Parse(l.Value, CultureInfo.InvariantCulture)
                : 1;
            sizes[Attr(type, "name")] = PrimitiveSizes[primitive] * length;
        }

        foreach (var e in types.Elements("enum"))
        {
            sizes[Attr(e, "name")] = PrimitiveSizes[Attr(e, "encodingType")];
        }

        foreach (var s in types.Elements("set"))
        {
            sizes[Attr(s, "name")] = PrimitiveSizes[Attr(s, "encodingType")];
        }

        // Composites may reference types declared above them, so resolve repeatedly
        // until nothing more can be sized.
        var composites = types.Elements("composite").ToList();
        bool progressed;
        do
        {
            progressed = false;
            foreach (var composite in composites.ToList())
            {
                var total = 0;
                var resolvable = true;
                foreach (var member in composite.Elements())
                {
                    if (member.Attribute("presence")?.Value == "constant")
                    {
                        continue;
                    }

                    var memberType = member.Attribute("primitiveType")?.Value
                                     ?? member.Attribute("encodingType")?.Value
                                     ?? member.Attribute("type")?.Value;
                    if (memberType is null || !sizes.TryGetValue(memberType, out var memberSize))
                    {
                        resolvable = false;
                        break;
                    }

                    var memberLength = member.Attribute("length") is { } ml
                        ? int.Parse(ml.Value, CultureInfo.InvariantCulture)
                        : 1;
                    total += memberSize * memberLength;
                }

                if (resolvable)
                {
                    sizes[Attr(composite, "name")] = total;
                    composites.Remove(composite);
                    progressed = true;
                }
            }
        }
        while (progressed && composites.Count > 0);

        return sizes;
    }

    private static string Attr(XElement element, string name) =>
        element.Attribute(name)?.Value
        ?? throw new InvalidOperationException($"<{element.Name.LocalName}> is missing required attribute '{name}'");
}
