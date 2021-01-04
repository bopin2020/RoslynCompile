# Roslyn Notes

## 1. 项目地址:

```
msdn: https://docs.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/get-started/syntax-analysis

github: https://github.com/dotnet/roslyn

The Roslyn .NET compiler.privides C# 

nuget package: https://www.nuget.org/profiles/RoslynTeam  
    VS :   Microsoft.CodeAnalysis.CSharp    
```

## 2.  语法树

### 2.1 基础

```
"Syntax API"  语法API 提供对描述C#、VB程序的数据结构的访问

语法树是 C#  VB 编译器用于理解C#、VB程序的数据结构 
语法树由生成项目时或开发人员按 F5 时所运行的分析程序生成

语法树也是不可变的 ；一旦创建语法树，就不能再更改

// https://blog.csdn.net/WPwalter/article/details/80545207
```

### 2.2 语法树构建基块

> https://docs.microsoft.com/zh-cn/dotnet/csharp/roslyn-sdk/get-started/syntax-analysis
>
>  
>
> https://www.tugberkugurlu.com/archive/compiling-c-sharp-code-into-memory-and-executing-it-with-roslyn     
>
> Compiling C# Code Into Memory and Executing it with Roslyn
>
> https://stackoverflow.com/questions/50879342/roslyn-in-memory-compilation-cs0103-the-name-console-does-not-exist-in-the-c/50882172



SyntaxTree 是一种带有语言特定派生类的抽象类 

使用Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree 类的分析方法对 C#中的文本进行分析

* **Microsoft.CodeAnalysis.SyntaxTree**类  其实例表示整个分析树  
* **Microsoft.CodeAnalysis.SyntaxNode** 实例表示声明  语句  子句   表达式等语法构成
* **Microsoft.CodeAnalysis.SyntaxToken** 结构   表示独立的关键字  标识符  运算符
* **Microsoft.CodeAnalysis.SyntaxTrivia**











### 2.3 Demo  编译单个cs文件

步骤如下:

* 获取语法树实例
* 指定动态编译时一个随机的assembly名称   Path.GetRandomFileName() 即可
* 设置引用dll      非常重要（编译的程序缺少东西就是没有引用相关的dll ）
* CSharpCompilation.Create 编译  
* EmitResult获取编译结果  创建一个Memory内存流  然后通过Assembly.Load() 反射执行

```
// 遇到的问题：
https://stackoverflow.com/questions/50879342/roslyn-in-memory-compilation-cs0103-the-name-console-does-not-exist-in-the-c/50882172
// 参考: (不想花时间 就看这个)
https://www.tugberkugurlu.com/archive/compiling-c-sharp-code-into-memory-and-executing-it-with-roslyn
```



```c#
namespace RoslynDemo
{
    using Microsoft.CodeAnalysis.Emit;
    using Microsoft.Win32;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System;

    class Program
    {
        static void Main(string[] args)
        {
            start();
        }


        static void start()
        {         
            SyntaxTree syntaxtree = CSharpSyntaxTree.ParseText(@"
                namespace RoslynCompileSample
                {
                    using System;
                    public class Writer
                    {
                        public void Write(string message)
                        {
                            Console.WriteLine(message);
                        }
                    }
                }
                ");



            // Path.GetRandomFileName() 获取随机名
            string assemblyName = Path.GetRandomFileName();

            // 获取当前程序assembly运行的路径
            var assemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location);

            MetadataReference[] references = new MetadataReference[]
            {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                    MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Console.dll")),
                    MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Runtime.dll"))
        };



            // Type type = CompileType("GenericGenerator", syntaxtree);

            // 编译遇到问题   Roslyn in memory compilation  "Console" does not exist in the current context
            // https://stackoverflow.com/questions/50879342/roslyn-in-memory-compilation-cs0103-the-name-console-does-not-exist-in-the-c/50882172
            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName, 
                syntaxTrees : new[] { syntaxtree },
                references : references,
                // CSharpCompilationOptions  OutputKind 输出类型   DynamicallyLinkedLibrary  ConsoleApplication
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary
                ));

            // new CSharpCompilationOptions(OutputKind.ConsoleApplication, optimizationLevel:OptimizationLevel.Debug,allowUnsafe: true);


            // CSharpCompilation 编译完成 然后需要运行它   对于dll  使用Emit方法
            // 创建一个Stream 然后将Emit方法写assembly  到里面   EmitResult对象会给一个实例 然后我们可以获取编译的状态  警告 失败等

            using (var ms = new MemoryStream())
            {
                // EmitResult 返回编译结果
                EmitResult result = compilation.Emit(ms);

                if (!result.Success/* 编译成功 */)
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
                    // Seek() 文件指针    
                    ms.Seek(0, SeekOrigin.Begin);
                    Assembly assembly = Assembly.Load(ms.ToArray());

                    // 执行编译后的代码  
                    Type type = assembly.GetType("RoslynCompileSample.Writer");
                    object obj = Activator.CreateInstance(type);
                    type.InvokeMember("Write",
                        BindingFlags.Default | BindingFlags.InvokeMethod,
                        null,
                        obj,
                        new object[] { "Hello World" });
                }
            }

            // If it’s a success, we load the bytes into an Assembly object. The Assembly object you have here is no different the ones that you are used to. 
            // From this point on, it’s all up to your ninja reflection skills in order to execute the compiled code
        }
    }
}

```

### 2.4 Roslyn编译项目& C# solution with Roslyn

```c#

MSBuildWorkspace workspace = MSBuildWorkspace.Create();
// OpenSolutionAsync 异步打开解决方案   .Result;
Solution solution = workspace.OpenSolutionAsync(solutionUrl).Result;
// 获取项目依赖
ProjectDependencyGraph projectGraph = solution.GetProjectDependencyGraph();

ProjectId projectId in projectGraph.GetTopologicallySortedProjects()
    
Compilation projectCompilation = solution.GetProject(projectId).GetCompilationAsync().Result;
```







```c#
MemoryStream stream = new MemoryStream();

EmitResult result = Compilation.Emit(Stream);    将编译结果输出到流中

    Diagnostics.Count
    
// CS5001: Program does not contain a static 'Main' method suitable for an entry point
    https://github.com/dotnet/roslyn/issues/39359

compile a WPF project using MSBuildWorkspace      
https://github.com/dotnet/roslyn/issues/2779    

通过Roslyn 编译找不到 "Main" 方法
error CS5001: Program does not contain a static 'Main' method suitable for an entry point
https://www.yuanmacha.com/1381353441.html


var assemblypath = Path.GetDirectoryName(typeof(object).Assembly.Location);

```

### 2.5 Roslyn 修改代码   & CSharpSyntaxRewriter 

> 读取模板文件  然后修改其中的代码   编译输出为exe
>
> 参考： https://blog.csdn.net/majian/article/details/106377183   

```
patch    byte[]    去掉原来的这个    项目模板文件
Roslyn  .cs 
手动添加   static  byte[]  

java 端加密后  私钥怎么弄到当前编译器文件内   字节数组      patch,嵌入资源
2. Roslyn  string 


模板文件             Roslyn  语法数  
https://www.codenong.com/46065777/
```

```c#
// 
String content = File.ReadAllText(path);     // 读取项目模板文件  
            // Roslyn 修改语法树     Syntax 树是不可变的，可以基于现有语法树创建新的 
            // 创建节点并替换现有节点 
            // https://www.thinbug.com/q/26458782  

            SyntaxTree syntaxtree = CSharpSyntaxTree.ParseText(content);
            var Mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);

            // 重新构建语法树
            var compilation2 = CSharpCompilation.Create("MyCompilation",
                syntaxTrees: new[] { syntaxtree }, references: new[] { Mscorlib });

            // 获取语义模型
            var model = compilation2.GetSemanticModel(syntaxtree);
            // 获取根节点   
            var root = model.SyntaxTree.GetRoot();    
            // 使用Visit 重写代码

            var rw = new LiteralRewriter();       // LiteralRewriter() 类继承
            var newRoot = rw.Visit(root);

            // 新生成代码
            string strNewCode = newRoot.GetText().ToString();
            Console.WriteLine(strNewCode);
			// 重新使用CSharpSyntaxTree.ParseText 解析语法树
            SyntaxTree syntaxtree2 = CSharpSyntaxTree.ParseText(strNewCode);

// 用roslyn 语法树工具  构造一个根节点
// 使用CSharpSyntaxReWriter 重写里面的部分代码 
// 重写方法
继承类  CSharpSyntaxReWriter   里面所有以Visit开头的函数都可以重载    
    VisitLiteralExpression 解决字符串表达式  入口参数node里面只有字符串
    

    // 
    class LiteralRewriter : CSharpSyntaxRewriter      // 继承CSharpSyntaxRewriter
    {
        public override SyntaxNode VisitLiteralExpression(LiteralExpressionSyntax node) // 重载 VisitLiteralExpression 方法, 输入节点是 文字表达式
        {
            if (!node.IsKind(SyntaxKind.StringLiteralExpression))
            { return base.VisitLiteralExpression(node); }
            // 重新构造一个字符串表达式
            var retVal = SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression,
                                                         SyntaxFactory.Literal("I'm bopin,siry"));
            return retVal;
        }

    }
```

### 2.6 CSharpSyntaxRewriter  

https://docs.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.csharpsyntaxrewriter.visit?view=roslyn-dotnet

```c#
Roslyn 语法树根节点       CompilationUnitSyntax   CompilationUnit 

// https://docs.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.csharpsyntaxrewriter.visitarraycreationexpression?view=roslyn-dotnet
CSharpSyntaxRewriter.VisitArrayCreationExpression()
    
    
// https://github.com/ErikSchierboom/AnalyzingSourceCodeUsingRoslyn/blob/6d8007e5bee6283ac72cb4dbca17ec59d1dd08bc/Representer/Program.cs
{
    RemoveUsingStatements().Visit();
    RemoveComments()
    
}
 
// SourceFusion    a pre-compile framework based on Roslyn.   build high-pergormance .NET code
https://github.com/dotnet-campus/        .NET campus


// convert a .. property to a object[] field
https://stackoverflow.com/questions/23438011/how-to-replace-nodes-while-preserving-their-trivia

ArrayCoreationExpressionSyntax
    
    node.DescendantNodes() 
    SyntaxFacrory.FieldDeclaration()
    SyntaxFactory.Token(SyntaxKind.StaticKeyword)
    
with an implicit type use an ImplicitArrayCreationExpression
    
    
// https://joshvarty.com/2014/08/15/learn-roslyn-now-part-5-csharpsyntaxrewriter/
    CSharpSyntaxRewriter     modify "the syntax tree "
   CSharpSyntaxVisitor 
    
 

```



#### 1. CSharpSyntaxRewriter  => modify the syntax tree

```
// https://stackoverflow.com/questions/25308944/removing-redundant-semicolons-in-code-with-syntaxrewriter
// https://joshvarty.com/2014/08/15/learn-roslyn-now-part-5-csharpsyntaxrewriter/


```

> Nish26  不错的提问

Is it possible to **override multiple VisitMethods** in a single Rewriter?? Fox example : I want to **rename a class** and **hence the constructor** inside it . So can i override **VisitClassDeclaration and VisitConstructorDeclaration methods** in the same rewriter and call base Visit to get the things done ? I tried doing it but could not get it working . Always the first method that is overridden gets called . How do you suggest this scenario should be handled?

> Answer:
>
> Yes you should definitely be able to override as many as you like. In one of my projects we’re overriding 20-30 of these methods. You have to make sure to call **base.VisitClassDeclaration(node)**. (Or whatever the appropriate base method is).

VisitClassDeclaration()







#### 2 .  remove redundant semicolon  移除多余的分号   

```c#
    how to remove redundant semicolon in code with SyntaxRewriter
// https://stackoverflow.com/questions/25308944/removing-redundant-semicolons-in-code-with-syntaxrewriter
public class Sample                     bopin 问题        leading or trailing trivia ? 
{
   public void Foo()
   {
      Console.WriteLine("Foo");
      ;
   }
}
									// inherit  from CSharpSyntaxRewriter     
unintended 不期望的,意外的
    
public class EmptyStatementRemoval : CSharpSyntaxRewriter
{
    // SyntaxNode  VisitEmptyStatement()
  public override SyntaxNode VisitEmptyStatement(EmptyStatementSyntax node)
  {
    return null;
  }
    											 // EmptyStatementSyntax    空语句节点
    
   public override SyntaxNode VisitEmptyStatement(EmptyStatementSyntax node)
	{
       // Token 作用是什么？       Se
       micolonToken
       
    return node.WithSemicolonToken(
        SyntaxFactory.MissingToken(SyntaxKind.SemicolonToken)
            .WithLeadingTrivia(node.SemicolonToken.LeadingTrivia)
            .WithTrailingTrivia(node.SemicolonToken.TrailingTrivia));
	}
}
```



#### 3. 添加类成员

> VisitClassDeclaration   AddMembers

```c#
            var field =
Syntax.FieldDeclaration(
Syntax.VariableDeclaration(
Syntax.PredefinedType(
    Syntax.Token(
        SyntaxKind.StringKeyword))))
.WithModifiers(Syntax.Token(SyntaxKind.PrivateKeyword))
.AddDeclarationVariables(Syntax.VariableDeclarator("myAddedField"));
            var theClass = tree.GetRoot().DescendantNodes()
                .OfType<ClassDeclarationSyntax>().First();
            theClass = theClass.AddMembers(field).NormalizeWhitespace();
            System.Diagnostics.Debug.Write(theClass.GetFullText());
```

#### 4.  修改变量 类型为 var & CSharpSyntaxRewriter

```c#
    class UseVarDeclarations : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitVariableDeclaration(VariableDeclarationSyntax node)
        {
            if (node.Type.IsVar)
            {
                return base.VisitVariableDeclaration(node);    
            }

            return base.VisitVariableDeclaration(node.WithType(SyntaxFactory.IdentifierName("var")));
        }
    }
```

#### 5. 修改特性Attributes

```c#
    /// <summary>
    /// 代替 特性声明
    /// </summary>
    public class AttributeStatementChanger : CSharpSyntaxRewriter
    {
        /// Visited for all AttributeListSyntax nodes
        /// The method replaces all PreviousAttribute attributes annotating a method by ReplacementAttribute attributes
        public override SyntaxNode VisitAttributeList(AttributeListSyntax node)
        {
            // If the parent is a MethodDeclaration (= the attribute annotes a method)
            if (node.Parent is MethodDeclarationSyntax &&
                // and if the attribute name is PreviousAttribute
                node.Attributes.Any(
                    currentAttribute => currentAttribute.Name.GetText().ToString() == "PreviousAttribute"))
            {
                // Return an alternate node that is injected instead of the current node
                return SyntaxFactory.AttributeList(
                                SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("ReplacementAttribute"),
                                    SyntaxFactory.AttributeArgumentList(
                                        SyntaxFactory.SeparatedList(new[]
                                        {
                                    SyntaxFactory.AttributeArgument(
                                        SyntaxFactory.LiteralExpression(
                                            SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(@"Sample"))
                                        )
                                        })))));
            }
            // Otherwise the node is left untouched
            return base.VisitAttributeList(node);
        }
    }
```

------



> 最终目的:    添加类成员  添加类方法    方法内添加局部变量

#### 6. Generation Fields and Properties with Roslyn

> https://dogschasingsquirrels.com/2014/08/04/code-generation-with-roslyn-fields-and-properties/

```c#
class ModifyClassDeclaration : CSharpSyntaxRewriter
{
	public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
	{
		// node 节点表示类 
		        PropertyDeclarationSyntax @property = SF.PropertyDeclaration(SF.ParseTypeName(" String "), " MyProperty ")
                    .AddModifiers(SF.Token(SyntaxKind.PublicKeyword));
                // Add a getter
                @property = @property.AddAccessorListAccessors(
                    SF.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                        .WithSemicolonToken(SF.Token(SyntaxKind.SemicolonToken)
                        ));
                // Add a private setter
                @property = @property.AddAccessorListAccessors(
                    SF.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                    .AddModifiers(SF.Token(SyntaxKind.PrivateKeyword))
                    .WithSemicolonToken(SF.Token(SyntaxKind.SemicolonToken)
                    ));
                // Add the property to the class
               node = node.AddMembers(@property);

                return base.VisitClassDeclaration(node);
	}
}
   
```