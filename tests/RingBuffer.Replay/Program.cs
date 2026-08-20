using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using RingBuffer.Core;

namespace RingBuffer.Replay;

/// <summary>
/// <c>replay --input &lt;journal&gt; --output &lt;journal&gt; [--seed n] [--ring name]</c>
///
/// A pure function from input journal to output journal. See
/// knowledge/architecture/replay-harness.md.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        string? input = null, output = null, ring = "reference";
        string? diffExpected = null, diffActual = null, schemaPath = null;
        long seed = 0;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--input" when i + 1 < args.Length: input = args[++i]; break;
                case "--output" when i + 1 < args.Length: output = args[++i]; break;
                case "--ring" when i + 1 < args.Length: ring = args[++i]; break;
                case "--diff" when i + 2 < args.Length:
                    diffExpected = args[++i];
                    diffActual = args[++i];
                    break;
                case "--schema" when i + 1 < args.Length: schemaPath = args[++i]; break;
                case "--seed" when i + 1 < args.Length:
                    seed = long.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                case "-h":
                case "--help":
                    Usage();
                    return 0;
                default:
                    Console.Error.WriteLine($"replay: unrecognised argument '{args[i]}'");
                    Usage();
                    return 2;
            }
        }

        if (diffExpected is not null && diffActual is not null)
        {
            return Diff(diffExpected, diffActual, schemaPath);
        }

        if (input is null || output is null)
        {
            Console.Error.WriteLine("replay: --input and --output are both required");
            Usage();
            return 2;
        }

        if (!File.Exists(input))
        {
            Console.Error.WriteLine($"replay: no such input journal: {input}");
            return 1;
        }

        var factory = RingFactory(ring);
        if (factory is null)
        {
            Console.Error.WriteLine($"replay: unknown ring '{ring}' (known: {string.Join(", ", KnownRings)})");
            return 2;
        }

        new ReplayEngine(factory).Run(input, output, seed);
        return 0;
    }

    private static int Diff(string expectedPath, string actualPath, string? schemaPath)
    {
        foreach (var path in new[] { expectedPath, actualPath })
        {
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"replay: no such journal: {path}");
                return 1;
            }
        }

        var schema = schemaPath is not null ? new SbeSchemaMap(schemaPath) : null;
        var differ = new JournalDiffer(schema);

        using var expectedStream = File.OpenRead(expectedPath);
        using var actualStream = File.OpenRead(actualPath);
        var result = differ.Compare(
            new List<byte[]>(Journal.ReadFrames(expectedStream)),
            new List<byte[]>(Journal.ReadFrames(actualStream)));

        if (result.Identical)
        {
            Console.WriteLine(result.Report);
            return 0;
        }

        Console.Error.WriteLine(result.Report);
        return 1;
    }

    private static readonly IReadOnlyList<string> KnownRings = new[] { "reference" };

    private static Func<IRingBuffer>? RingFactory(string name) => name switch
    {
        // SPSC and MPSC join this list in Phases 4 and 5; the same corpus then runs
        // against all three, which is what makes triangulation possible.
        "reference" => () => new ReferenceQueue(),
        _ => null,
    };

    private static void Usage() =>
        Console.Error.WriteLine(
            """
            usage: replay --input <journal> --output <journal> [--seed <n>] [--ring <name>]
                   replay --diff <expected> <actual> [--schema <schema.xml>]

              --input   journal of length-prefixed SBE frames to replay
              --output  journal to write the consumed frames to
              --seed    integer seeding the clock and id source (default 0)
              --ring    which IRingBuffer to replay through (default "reference")
              --diff    compare two journals byte for byte; exit 1 if they differ
              --schema  SBE schema, so the diff can name the field rather than the offset

            A pure function from input journal to output journal. No network, no clock,
            no filesystem beyond those two paths.
            """);
}
