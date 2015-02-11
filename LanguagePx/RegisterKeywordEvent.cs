using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;

namespace LanguagePx
{
    [Cmdlet(
        VerbsLifecycle.Register,
        "KeywordEvent"
    )]
    [OutputType(typeof(void))]
    public class RegisterKeywordEvent : PSCmdlet
    {
        [Parameter(
            Position = 0,
            Mandatory = true,
            HelpMessage = "The name of the domain-specific language."
        )]
        [ValidateNotNullOrEmpty()]
        public string DslName;

        [Parameter(
            Position = 1,
            Mandatory = true,
            HelpMessage = "The path that identifies the keyword in the domain-specific language definition."
        )]
        [ValidateNotNullOrEmpty()]
        public string KeywordPath;

        [Parameter(
            Position = 2,
            Mandatory = true,
            HelpMessage = "The name of the event."
        )]
        [ValidateNotNullOrEmpty()]
        [ValidateSet(new string[] { "OnInvoking", "OnInvoked" })]
        public string EventName;

        [Parameter(
            Position = 3,
            Mandatory = true,
            HelpMessage = "The action to take when the event is raised."
        )]
        [ValidateNotNull()]
        public ScriptBlock Action;

        protected override void EndProcessing()
        {
            ScriptBlockAst scriptBlockAst = Action.Ast as ScriptBlockAst;

            if (string.Compare(EventName,"OnInvoking",true) == 0)
            {
                if ((scriptBlockAst.ParamBlock == null) ||
                    (scriptBlockAst.ParamBlock.Parameters.Count != 1) ||
                    (scriptBlockAst.ParamBlock.Parameters[0].StaticType != typeof(string)) ||
                    (string.Compare(scriptBlockAst.ParamBlock.Parameters[0].Name.VariablePath.UserPath, "Name", true) != 0))
                {
                    string message = "The OnInvoking action must contain exactly one parameter called \"Name\". This parameter must be of type string.";
                    PSArgumentException exception = new PSArgumentException(message, "Action");
                    ErrorRecord errorRecord = new ErrorRecord(exception, "ParameterBindingValidationException", ErrorCategory.InvalidData, null);
                    ThrowTerminatingError(errorRecord);
                }

                DslDatabase.SetOnInvokingEventHandler(DslName, KeywordPath, Action);
            }
            else if (string.Compare(EventName,"OnInvoked",true) == 0)
            {
                if ((scriptBlockAst.ParamBlock != null) &&
                    (scriptBlockAst.ParamBlock.Parameters.Count != 0))
                {
                    string message = "The OnInvoked action must not contain any parameters.";
                    PSArgumentException exception = new PSArgumentException(message, "Action");
                    ErrorRecord errorRecord = new ErrorRecord(exception, "ParameterBindingValidationException", ErrorCategory.InvalidData, null);
                    ThrowTerminatingError(errorRecord);
                }

                DslDatabase.SetOnInvokedEventHandler(DslName, KeywordPath, Action);
            }

            base.EndProcessing();
        }
    }
}