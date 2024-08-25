using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NetCorePal.Extensions.DistributedTransactions.CAP.SourceGenerators
{
    [Generator]
    public class CapIntegrationConvertDomainEventHandlerSourceGenerator : ISourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.RootNamespace",
                out var rootNamespace);
            if (rootNamespace == null)
            {
                return;
            }

            var compilation = context.Compilation;
            foreach (var syntaxTree in compilation.SyntaxTrees)
            {
                if (syntaxTree.TryGetText(out var sourceText) &&
                    !sourceText.ToString().Contains("IIntegrationEventConvert"))
                {
                    continue;
                }

                var semanticModel = compilation.GetSemanticModel(syntaxTree);
                if (semanticModel == null)
                {
                    continue;
                }

                var typeDeclarationSyntaxs =
                    syntaxTree.GetRoot().DescendantNodesAndSelf().OfType<TypeDeclarationSyntax>();
                foreach (var tds in typeDeclarationSyntaxs)
                {
                    var symbol = semanticModel.GetDeclaredSymbol(tds);
                    if (symbol is not INamedTypeSymbol) return;
                    INamedTypeSymbol namedTypeSymbol = (INamedTypeSymbol)symbol;
                    if (!namedTypeSymbol.IsImplicitClass &&
                        namedTypeSymbol.AllInterfaces.Any(p => p.Name == "IIntegrationEventConvert"))
                    {
                        Generate(context, namedTypeSymbol, rootNamespace);
                    }
                }
            }
        }


        private void Generate(GeneratorExecutionContext context, INamedTypeSymbol integrationConvertTypeSymbol,
            string rootNamespace)
        {
            string className = integrationConvertTypeSymbol.Name;

            //根据dbContextType继承的接口IIntegrationEventHandle<TIntegrationEvent> 推断出TIntegrationEvent类型
            var convertNamespace = integrationConvertTypeSymbol.ContainingNamespace.ToString();
            var usingNamespace = integrationConvertTypeSymbol.ContainingNamespace.ContainingNamespace.ToString();

            var iinterface = integrationConvertTypeSymbol.AllInterfaces
                .FirstOrDefault(i => i.Name == "IIntegrationEventConvert");
            if (iinterface == null)
            {
                return;
            }

            var domainEvent = iinterface.TypeArguments[0].Name;

            string source = $@"// <auto-generated/>
using {convertNamespace};
using NetCorePal.Extensions.DistributedTransactions;
using NetCorePal.Extensions.Domain;
using {rootNamespace};

namespace  {usingNamespace}.DomainEventHandlers
{{
    /// <summary>
    /// {className}DomainEventHandlers
    /// </summary>
    public class {className}DomainEventHandler(IIntegrationEventPublisher integrationEventPublisher,
{className} convert) : IDomainEventHandler<{domainEvent}>
    {{
        /// <summary>
        /// {className}DomainEventHandler
        /// </summary>
        /// <param name=""notification"">notification</param>
        /// <param name=""cancellationToken"">cancellationToken</param>
        public async Task Handle({domainEvent} notification, CancellationToken cancellationToken){{
            // 发出转移操作集成事件
            var integrationEvent = convert.Convert(notification);
            await integrationEventPublisher.PublishAsync(integrationEvent, cancellationToken);
        }}
        
    }}
}}
";
            context.AddSource($"{className}DomainEventHandlers.g.cs", source);
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            // Method intentionally left empty.
        }
    }
}