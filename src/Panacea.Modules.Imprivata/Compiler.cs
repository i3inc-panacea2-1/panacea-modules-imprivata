using Microsoft.CSharp;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Panacea.Modules.Imprivata
{
    public interface ITransformer
    {
        List<string> Transform(string code);
    }

    static class Compiler
    {
        private static Assembly CompileSourceCodeDom(string sourceCode)
        {
            CompilerResults cr = null;
            CodeDomProvider cpd = new CSharpCodeProvider(new Dictionary<string, string> { { "CompilerVersion", "v4.0" } });
            var cp = new CompilerParameters();

            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"Microsoft.NET\assembly\GAC_MSIL");
            var root32 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"Microsoft.NET\assembly\GAC_32");
            cp.ReferencedAssemblies.Add(Assembly.GetExecutingAssembly().Location);
            cp.ReferencedAssemblies.Add(root + @"\System\v4.0_4.0.0.0__b77a5c561934e089\System.dll");

            cp.GenerateExecutable = false;
            cp.WarningLevel = 0;
            cr = cpd.CompileAssemblyFromSource(cp, "using UserPlugins.Imprivata; using System; using System.Collections.Generic; using System.Text;" + sourceCode);
            if (cr.Errors.HasErrors)
            {
                var lst = new List<string>();
                foreach (var error in cr.Errors)
                {
                    lst.Add(error.ToString());
                }
                throw new Exception(string.Join(", ", lst.ToArray()));
            }
            return cr.CompiledAssembly;
        }

        public static List<string> GetNumbers(string source, string code)
        {
            var assembly = CompileSourceCodeDom(source);
            Type fooType = assembly.GetTypes().First(t => typeof(ITransformer).IsAssignableFrom(t));
            var obj = (ITransformer)Activator.CreateInstance(fooType);
            return obj.Transform(code);
        }
    }
}
