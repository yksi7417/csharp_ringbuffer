// Drives SBE's C# code generator through its Java API.
//
// This exists because the documented CLI path does not work. SbeTool resolves
// -Dsbe.target.language against TargetCodeGeneratorLoader, which registers only
// JAVA, C, CPP, GOLANG and RUST -- verified across releases 1.27.0 through 1.39.0.
// CSharpGenerator is present and maintained, but has no no-arg constructor for the
// loader to instantiate, so it is unreachable that way:
//
//   IllegalArgumentException: No code generator for name: ...csharp.CSharpGenerator
//   Caused by: NoSuchMethodException: ...CSharpGenerator.<init>()
//
// See knowledge/findings/f2-csharp-codegen-requires-shim.md.
//
//   java -cp sbe-all-<version>.jar:out SbeCsharpGen <schema.xml> <outputDir>

import org.agrona.generation.OutputManager;
import uk.co.real_logic.sbe.SbeTool;
import uk.co.real_logic.sbe.generation.csharp.CSharpGenerator;
import uk.co.real_logic.sbe.generation.csharp.CSharpNamespaceOutputManager;
import uk.co.real_logic.sbe.ir.Ir;
import uk.co.real_logic.sbe.xml.IrGenerator;
import uk.co.real_logic.sbe.xml.MessageSchema;

public final class SbeCsharpGen
{
    private SbeCsharpGen()
    {
    }

    public static void main(final String[] args) throws Exception
    {
        if (args.length != 2)
        {
            System.err.println("usage: SbeCsharpGen <schema.xml> <outputDir>");
            System.exit(2);
        }

        final String schemaFile = args[0];
        final String outputDir = args[1];

        // Fail loudly on a schema that does not validate, rather than generating
        // something subtly wrong. Silence here is how a codegen step passes green
        // while producing nothing usable (TRAP-1).
        System.setProperty(SbeTool.VALIDATION_STOP_ON_ERROR, "true");
        System.setProperty(SbeTool.VALIDATION_WARNINGS_FATAL, "true");

        final MessageSchema schema = SbeTool.parseSchema(schemaFile);
        final Ir ir = new IrGenerator().generate(schema, schema.packageName());
        final OutputManager output = new CSharpNamespaceOutputManager(outputDir, ir.applicableNamespace());

        new CSharpGenerator(ir, output).generate();

        System.out.println("generated " + ir.applicableNamespace() + " -> " + outputDir);
    }
}
