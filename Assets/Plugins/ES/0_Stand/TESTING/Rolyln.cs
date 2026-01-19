using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.Linq;
using System.IO;
using Microsoft.CodeAnalysis;
using System.Threading.Tasks;


namespace ES
{
    public class EditorInvoker_DetectMethod : EditorInvoker_Level50
    {
     
        
        public override void InitInvoke()
        {
          
//            Debug.Log("开始工作");
           
          /*  if (!MSBuildLocator.IsRegistered)
            {
                MSBuildLocator.RegisterDefaults();
                var use = MSBuildLocator.QueryVisualStudioInstances().First();
                MSBuildLocator.RegisterInstance(use);
            }*/
          /*  var path = use.VisualStudioRootPath;
            Debug.Log("PATH" + path);
            var projectFile = new FileInfo(path);*/
           /*  solution = null;

            Project project=null;
            var docs = project.Documents;
            Task.Run(() => StartHandleDocs(docs, () => { }));*/
        }
    /*    public async void StartHandleDocs(IEnumerable<Document> documents, Action end)
        {
            foreach (var doc in documents)
            {
                var model = await doc.GetSemanticModelAsync();
                var root = await doc.GetSyntaxRootAsync();
                var methodDec = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First(who => who.Identifier.ValueText == "SendLink");

                if (methodDec != null)
                {
                    var symbol= model.GetDeclaredSymbol(methodDec);

                    var refernces = await SymbolFinder.FindReferencesAsync(symbol,solution);

                    foreach(var re in refernces)
                    {
                        var loc= re.Locations.First();
                        Console.WriteLine(re.Locations.First().Location.SourceSpan);
                    }
                }
            }
        }*/
    }
    public class Rolyln : MonoBehaviour
    {

    }
}
