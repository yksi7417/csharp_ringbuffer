import org.agrona.generation.OutputManager;
import uk.co.real_logic.sbe.SbeTool;
import uk.co.real_logic.sbe.generation.csharp.CSharpGenerator;
import uk.co.real_logic.sbe.generation.csharp.CSharpNamespaceOutputManager;
import uk.co.real_logic.sbe.ir.Ir;
import uk.co.real_logic.sbe.xml.IrGenerator;
import uk.co.real_logic.sbe.xml.MessageSchema;

public final class SbeCsharpGen {
    public static void main(final String[] args) throws Exception {
        final String schemaFile = args[0];
        final String outputDir  = args[1];
        final MessageSchema schema = SbeTool.parseSchema(schemaFile);
        final Ir ir = new IrGenerator().generate(schema, schema.packageName());
        final OutputManager om = new CSharpNamespaceOutputManager(outputDir, ir.applicableNamespace());
        new CSharpGenerator(ir, om).generate();
        System.out.println("generated -> " + outputDir);
    }
}
