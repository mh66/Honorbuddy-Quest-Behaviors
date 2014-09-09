// Behavior originally contributed by HighVoltz.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.

#region Summary and Documentation

/* 
	 RunCode has the following characteristics:
	  * It can run C# statements and coroutines.
	  * It can define functions, classes or any other types 

	BEHAVIOR LIMITATIONS:
		Any type, variable and function definition cannot easily be accessed from a <If/While Condition />
		because they're placed in different namespaces and classes and the code generated by RunCode is 
		placed inside long namespace and in a somewhat obscure class name
 
	BEHAVIOR ATTRIBUTES:
		Type [optional; Default: Statement]
			This argument specifies whether the code is a statement or a  type/function definition
  
		Code [optional]
			This is the CSharp code. This attribute is optional because code can also be placed inside a CDATA node 
			Whats nice about using a CDATA node is you don't need to use xml escapes for <, >, ', " and &
 
	BEHAVIOR ELEMENTS:
		CDATA [optional]
			Code placed inside this element does not need to be escaped. 
			The Code attribute must be left out for this element to be used.
			See the examples.
 
 */

#endregion

#region Examples

/*
	This is an example of how this behavior can be used to stop the bot, very basic.
	You can leave out the semicolon at the end on single line statements if you want. 
 
	<CustomBehavior File="RunCode" Code="TreeRoot.Stop()">
 
	The following is an example that shows how to define a function, not very useful.

        <CustomBehavior File="RunCode" Type="Definition"><![CDATA[ 
                void Log(string format, params object[] args)
                {
                    Logging.Write(Colors.Green, Path.GetFileNameWithoutExtension(ProfileManager.XmlLocation)+": " + format, args);
                }
            ]]></CustomBehavior>

    The statements are executed inside a Honorbuddy coroutine function so this makes it possible to 
    create your own coroutine or execute coroutines elsewhere
 
    
  
 */

#endregion

#region Usings

using System;
using System.CodeDom.Compiler;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Bots.Professionbuddy;
using Buddy.Coroutines;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Microsoft.CSharp;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;

#endregion

namespace Honorbuddy.Quest_Behaviors
{
    [CustomBehaviorFileName(@"RunCode")]
    public class RunCode : CustomForcedBehavior
    {
        // static field members
        private static Dictionary<string, GeneratedClass> GeneratedClasses { get; set; }
        private static bool _initialized;

        #region Consructor and Argument Processing

        public RunCode(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // Parameters dealing with 'starting' the behavior...
                Code = GetAttributeAs<string>("Code", false, null, null);
                Type = GetAttributeAsNullable<CodeType>("Type", false, null, null) ?? CodeType.Statement;

                QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
                QuestRequirementComplete =
                    GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ??
                    QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog =
                    GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ??
                    QuestInLogRequirement.InLog;
            }

            catch (Exception except)
            {
                // Maintenance problems occur for a number of reasons.  The primary two are...
                // * Changes were made to the behavior, and boundary conditions weren't properly tested.
                // * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
                // In any case, we pinpoint the source of the problem area here, and hopefully it can be quickly
                // resolved.
                LogMessage(
                    "error",
                    "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message
                    + "\nFROM HERE:\n"
                    + except.StackTrace + "\n");
                IsAttributeProblem = true;
            }
        }


        // Variables for Attributes provided by caller

        private string Code { get; set; }
        private CodeType Type { get; set; }
        private int QuestId { get; set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId
        {
            get { return "$Id: GetOutOfGroundEffectAndAuras.cs 304 2013-02-06 05:19:13Z chinajade $"; }
        }

        public override string SubversionRevision { get { return "$Rev: 304 $"; } }

        #endregion

        #region Private and Convenience variables

        private bool _isBehaviorDone;

        private string _profileIdentifier;

        private string ProfileIdentifier
        {
            get
            {
                return _profileIdentifier ??
                       (_profileIdentifier = "_" + ProfileManager.XmlLocation.GetHashCode().ToString(CultureInfo.InvariantCulture));
            }
        }

        private const string UsingStatements = @"
using System;
using System.Threading.Tasks;
using System.Reflection;
using System.Data;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;
using System.Windows.Media;
using System.Xml.Linq;

using Styx;
using Styx.Common;
using Styx.Helpers;
using Styx.CommonBot.Routines;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.CommonBot.AreaManagement;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.Pathing;
using Styx.CommonBot.Profiles;
using Styx.Plugins;
using Styx.WoWInternals.World;
using Styx.CommonBot.Coroutines;
using Buddy.Coroutines;
";

        #endregion

        #region Overrides of CustomForcedBehavior

        public override void OnStart()
        {
            if (!_initialized)
            {
                BotEvents.OnBotStopped += BotEvents_OnBotStopped;
                GeneratedClasses = new Dictionary<string, GeneratedClass>();
                _initialized = true;
            }

            if (!GeneratedClasses.ContainsKey(ProfileIdentifier))
            {
                if (!CompileCodeInCurrentProfile())
                {
                    QBCLog.ProfileError("There was a compile error. Check your code.");
                    return;
                }
            }

            if (Type == CodeType.Definition)
            {
                _isBehaviorDone = true;
            }
        }

        public override bool IsDone
        {
            get
            {
                return (_isBehaviorDone // normal completion
                        || !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete));
            }
        }


        #endregion

        #region Behavior

        protected override Composite CreateBehavior()
        {
            return new ActionRunCoroutine(ctx => MainCoroutine());
        }

        private async Task<bool> MainCoroutine()
        {
            if (IsDone || Type == CodeType.Definition)
                return false;

            var index = GetFunctionDelegateIndex();
            if (!index.HasValue)
            {
                QBCLog.ProfileError("Unable to locate function delegate for running instance.");
                return true;
            }

            var codeInstance = GeneratedClasses[ProfileIdentifier];
            var taskProducer = (Func<RunCode, Task>)codeInstance.FunctionDelegates[index.Value];

            await taskProducer(this);
            _isBehaviorDone = true;
            return true;
        }

        void BotEvents_OnBotStopped(EventArgs args)
        {
            DoCleanup();
        }

        private void DoCleanup()
        {
            _initialized = false;
            GeneratedClasses = null;
            BotEvents.OnBotStopped -= BotEvents_OnBotStopped;
        }

        #endregion

        #region Utility Methods.

        private int? GetFunctionDelegateIndex()
        {
            var myElement = Element;
            var index = 0;
            var runCSharpElements =
                ProfileManager.CurrentOuterProfile.XmlElement.Descendants("CustomBehavior")
                    .Where(e => e.Attribute("File").Value == "RunCode");
            foreach (var element in runCSharpElements)
            {
                var type = element.Attribute("Type");
                // skip definition since they're not
                if (type != null && type.Value == "Definition")
                    continue;
                if (element == myElement)
                    return index;
                index++;
            }
            return null;
        }

        private string TempFolder { get { return Path.Combine(Utilities.AssemblyDirectory, "CompiledAssemblies"); } }

        private bool CompileCodeInCurrentProfile()
        {
            string[] statementNames;
            var code = GenerateCode(out statementNames);

            CompilerResults results;
            using (var provider = new CSharpCodeProvider(new Dictionary<string, string> {{"CompilerVersion", "v4.0"},}))
            {
                var options = new CompilerParameters();
                var assemblyName = "QuestBehaviors_RunCode" + ProfileIdentifier;
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    // we don't want to reference older version of the compiled assembly.
                    if (!asm.IsDynamic && !asm.GetName().Name.Contains(assemblyName))
                        options.ReferencedAssemblies.Add(asm.Location);
                }

                options.GenerateExecutable = false;
                options.TempFiles = new TempFileCollection(TempFolder, false);
                options.IncludeDebugInformation = false;
                options.OutputAssembly = string.Format(@"{0}\{2}{1:N}.dll", TempFolder, Guid.NewGuid(), assemblyName);
                options.CompilerOptions = "/optimize";

                results = provider.CompileAssemblyFromSource(options, code);

            }
            if (results.Errors.HasErrors)
            {
                if (results.Errors.Count > 0)
                {
                    foreach (CompilerError error in results.Errors)
                    {
                        LogMessage("fatal", error.ErrorText);
                    }
                }
                return false;
            }
            var classType =
                results.CompiledAssembly.GetType("Honorbuddy.Quest_Behaviors.RunCode_Generated." + ProfileIdentifier);
            var delegates = new List<Delegate>(statementNames.Length);

            var instance = Activator.CreateInstance(classType);
            var methods = classType.GetMethods();

            foreach (var statementName in statementNames)
            {
                MethodInfo method = methods.FirstOrDefault(mi => mi.Name == statementName);
                if (method == null) continue;
                var del = Delegate.CreateDelegate(typeof(Func<RunCode,Task>), instance, method.Name);
                delegates.Add(del);
            }

            GeneratedClasses.Add(ProfileIdentifier, new GeneratedClass(instance, delegates));
            return true;
        }

        // gatherup all the <CustomBehavior File="RunCode' .. /> elements in current profile and 
        // compile it
        private string GenerateCode(out string[] statementNames)
        {
            var codeAndType = (from element in ProfileManager.CurrentOuterProfile.XmlElement.Descendants("CustomBehavior")
                where element.Attribute("File").Value == "RunCode"
                let typeAttr = element.Attribute("Type")
                let code = GetCodeFromElement(element)
                let type = typeAttr == null || typeAttr.Value == "Statement" ? CodeType.Statement : CodeType.Definition
                select new {Code = code, Type = type}).ToArray();

            var sb = new StringBuilder(100);

            var statements = codeAndType.Where(ct => ct.Type == CodeType.Statement).Select(ct => ct.Code).ToArray();
            var definitions = codeAndType.Where(ct => ct.Type == CodeType.Definition).Select(ct => ct.Code).ToArray();


            sb.Append(UsingStatements);
            sb.Append(
                string.Format(
                    "namespace Honorbuddy.Quest_Behaviors.RunCode_Generated\n{{\n\tpublic class {0}:Styx.CommonBot.Profiles.Quest.Order.ProfileHelperFunctionsBase\n\t\t{{",
                    ProfileIdentifier));

            foreach (var definition in definitions)
                sb.Append(definition);

            statementNames = statements.Select((ct, i) => ProfileIdentifier + "Func" + i).ToArray();

            for (int i = 0; i < statements.Length; i++)
            {
                var code = statements[i].Trim();
                if (string.IsNullOrEmpty(code))
                    continue;
                // append a semi-colon at end of statement left it out.
                if (code.Last() != ';')
                    code += ";";
                // allow statements to leave out the ending semi-colon.
                var funcCode = string.Format("public async Task {0} (RunCode instance)\n{{\n\t{1}\n}}", statementNames[i], code);
                sb.Append(funcCode);
            }

            sb.Append("\t\t}\n\t}\n");
            return sb.ToString();
        }

        private string GetCodeFromElement(XElement element)
        {
            var codeAttr = element.Attribute("Code");
            if (codeAttr != null)
                return codeAttr.Value;
            var cData = element.DescendantNodes().FirstOrDefault(e => e.NodeType == XmlNodeType.CDATA);
            return cData != null ? ((XCData) cData).Value : "";
        }

        #endregion

        #region Embedded Types

        public enum CodeType
        {
            // code is placed in a function that returns an async Task.
            Statement,
            // code is placed inside the body of a class. 
            Definition,
        }

        public class GeneratedClass
        {
            public GeneratedClass(object instance, List<Delegate> delegates)
            {
                ClassInstance = instance;
                FunctionDelegates = delegates;
            }

            // A reference to class instance. 
            public object ClassInstance { get; private set; }
            // list of delegates to generated functions within that class.
            public List<Delegate> FunctionDelegates { get; private set; }
        }

        #endregion
    }
}

