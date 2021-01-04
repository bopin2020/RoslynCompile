using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using SF = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace FrameworkRoslynCompileMultiFiles
{
    public class Demo
    {
        public static void Main(string[] args)
        {
            var tree = CSharpSyntaxTree.ParseText(@"
            using System.Windows.Forms;
            namespace Demo
            {
                public class Sample
                {
                    static void Main(string[] args)
                    {
                        MessageBox.Show(bopin);                                               
                    }
                }
            }
            ");

            var newresult = new ModifyClassDeclaration().Visit(tree.GetRoot());

            var newtree = CSharpSyntaxTree.ParseText(newresult.ToFullString());

            Console.WriteLine(newresult.ToFullString());


            string assemblyName = Path.GetRandomFileName();

            var assemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location);

            MetadataReference[] references = new MetadataReference[]
            {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                    MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Console.dll")),
                    MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Runtime.dll")),
                    MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Windows.Forms.dll"))
            };

            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees: new[] { newtree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.WindowsApplication
                ));

            using (var ms = new MemoryStream())
            {
                EmitResult result = compilation.Emit(ms);

                if (!result.Success)
                {
                    IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic =>
                           diagnostic.IsWarningAsError ||
                           diagnostic.Severity == DiagnosticSeverity.Error);
                    foreach (Diagnostic diagnostic in failures)
                    {
                        Console.Error.WriteLine("{0}: {1}", diagnostic.Id, diagnostic.GetMessage());
                    }
                }
                else
                {
                    ms.Seek(0, SeekOrigin.Begin);
                    Assembly assembly = Assembly.Load(ms.ToArray());
                    BinaryWriter b = new BinaryWriter(new FileStream(@"d:\desktop\success.exe", FileMode.Create));
                    b.Write(ms.ToArray());
                    b.Close();
                }
            }
        }
    }

    class ModifyClassDeclaration : CSharpSyntaxRewriter
    {
        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            FieldDeclarationSyntax aField = SF.FieldDeclaration(
            SF.VariableDeclaration(
                SF.ParseTypeName(" string "),
                SF.SeparatedList(new[] { SF.VariableDeclarator(SF.Identifier("bopin=\"Hi bopin\"")) })
            ))
            .AddModifiers(SF.Token(SyntaxKind.StaticKeyword));  //SyntaxKind.PublicKeyword),
            node = node.AddMembers(aField);
            return base.VisitClassDeclaration(node);
        }
    }
}
