using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using log4net;
using TraderTools.Simulation;

namespace Hallupa.TraderTools.Simulation
{
    public static class StrategyHelper
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static Type CompileStrategy(string code, string csPath)
        {
            var namespaceRegex = new Regex(@"namespace [a-zA-Z\.\r\n ]*{");
            var match = namespaceRegex.Match(code);
            if (match.Success)
            {
                code = namespaceRegex.Replace(code, "");

                var removeLastBraces = new Regex("}[ \n]*$");
                code = removeLastBraces.Replace(code, "");
            }

            // Get class name
            var classNameRegex = new Regex("public class ([a-zA-Z0-9_]*)");
            match = classNameRegex.Match(code);
            var className = match.Groups[1].Captures[0].Value;

            var a = CSharpLanguage.CreateAssemblyDefinition(
                code,
                csPath,
                "Hallupa.TraderTools.Basics.dll",
                "Hallupa.TraderTools.Core.dll",
                "Hallupa.TraderTools.Simulation.dll",
                "Hallupa.Library.dll",
                "Hallupa.TraderTools.Indicators.dll",
                "Hallupa.Library.dll",
                "log4net.dll");

            if (a == null)
            {
                Log.Error("Unable to compile strategy");
                return null;
            }

            var t = a.DefinedTypes.First(x => x.Name == className);
            if (t == null)
            {
                Log.Error($"Unable to create class '{className}'");
                return null;
            }

            return t;
        }
    }
}