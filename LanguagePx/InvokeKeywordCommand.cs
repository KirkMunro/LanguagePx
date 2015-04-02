using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Text.RegularExpressions;

namespace LanguagePx
{
    [Cmdlet(
            VerbsLifecycle.Invoke,
            "Keyword",
            DefaultParameterSetName = "Keyword"
    )]
    [OutputType(typeof(object))]
    public class InvokeKeywordCommand : PSCmdlet
    {
        [Parameter(
            Position = 0,
            HelpMessage = "The parameters that PowerShell passes in when invoking a keyword handler.",
            ParameterSetName = "Internal",
            ValueFromRemainingArguments = true,
            DontShow = true
        )]
        [Alias("Args")]
        public object[] ArgumentList;

        [Parameter(
            Mandatory = true,
            HelpMessage = "The keyword that is being invoked.",
            ParameterSetName = "Keyword"
        )]
        [ValidateNotNull()]
        public DynamicKeyword KeywordData;

        [Parameter(
            Mandatory = true,
            HelpMessage = "The name associated with the keyword invocation. If name is not required, this string will be empty.",
            ParameterSetName = "Keyword"
        )]
        [AllowEmptyString()]
        public string Name;

        [Parameter(
            Mandatory = true,
            HelpMessage = "The body of the keyword. This must be a script block or a hashtable.",
            ParameterSetName = "Keyword"
        )]
        [ValidateNotNull()]
        public object Value;

        [Parameter(
            Mandatory = true,
            HelpMessage = "A string identifying the location where the keyword was invoked.",
            ParameterSetName = "Keyword"
        )]
        [ValidateNotNullOrEmpty()]
        public string SourceMetadata;

        PowerShellHelper psHelper = null;

        protected override void BeginProcessing()
        {
            // Create the PowerShell helper
            psHelper = new PowerShellHelper(this);

            // Ensure the Keyword Manager is configured to use the appropriate PowerShellHelper instance
            KeywordManager.PSHelper = psHelper;

            if (string.Compare(ParameterSetName, "Keyword", true) == 0)
            {
                // If the cmdlet was invoked directly, throw a terminating error
                if (Regex.IsMatch(MyInvocation.InvocationName, @"^(LanguagePx\\)?Invoke-Keyword$"))
                {
                    string message = "The Invoke-Keyword cmdlet is reserved for keyword use only. Do not invoke this cmdlet directly. Invoke it using keyword aliases instead.";
                    InvalidOperationException exception = new InvalidOperationException(message);
                    ErrorRecord errorRecord = new ErrorRecord(exception, "IncorrectInvocation", ErrorCategory.InvalidOperation, null);
                    ThrowTerminatingError(errorRecord);
                }

                // If the cmdlet was passed pipeline input, throw a terminating error
                if (MyInvocation.ExpectingInput)
                {
                    string message = "The input object cannot be bound to any parameters for the keyword because keywords do not take pipeline input";
                    ParameterBindingException exception = new ParameterBindingException(message);
                    ErrorRecord errorRecord = new ErrorRecord(exception, "InputObjectNotBound", ErrorCategory.InvalidArgument, null);
                    ThrowTerminatingError(errorRecord);
                }

                // If the Value parameter is not of a supported type, throw a terminating error
                if (!(Value is ScriptBlock) && !(Value is Hashtable))
                {
                    string message = string.Format("Cannot convert '{0}' to the either of the types 'ScriptBlock' or 'Hashtable' that are required by parameter 'Value'.", Value);
                    ParameterBindingException exception = new ParameterBindingException(message);
                    ErrorRecord errorRecord = new ErrorRecord(exception, "ParameterArgumentValidationError", ErrorCategory.InvalidData, Value);
                    ThrowTerminatingError(errorRecord);
                }

                // If the keyword being invoked is not valid in the current DSL, throw a terminating error
                if (!KeywordManager.IsVisible(KeywordData))
                {
                    string message = string.Format("{0} is a valid keyword; however, it is not accessible in the current scope.", KeywordData.Keyword);
                    InvalidOperationException exception = new InvalidOperationException(message);
                    ErrorRecord errorRecord = new ErrorRecord(exception, "KeywordNotAccessible", ErrorCategory.InvalidOperation, KeywordData);
                    ThrowTerminatingError(errorRecord);
                }
            }

            // Invoke the base class BeginProcessing method
            base.BeginProcessing();
        }

        protected override void EndProcessing()
        {
            // Ensure the Keyword Manager is configured to use the appropriate PowerShellHelper instance
            KeywordManager.PSHelper = psHelper;

            if (string.Compare(ParameterSetName, "Internal", true) == 0)
            {
                // Load the dynamic keyword into the session if it is not already loaded (this is
                // necessary because keyword may be removed from the session by external sources,
                // such as whenever you invoke a DSC configuration function)
                DynamicKeyword keyword = KeywordManager.LoadVisibleKeyword(MyInvocation.InvocationName);

                // Generate the keyword required properties
                Hashtable invokeKeywordParameters = new Hashtable
                {
                    { "KeywordData", keyword },
                    { "SourceMetadata", string.Format("{0}::{1}::{2}::{3}", MyInvocation.ScriptName, MyInvocation.ScriptLineNumber, MyInvocation.OffsetInLine, MyInvocation.Line) }
                };
                if (ArgumentList.Length > 0)
                {
                    if (ArgumentList[0] is string)
                    {
                        invokeKeywordParameters.Add("Name", ArgumentList[0]);
                        if (ArgumentList.Length > 1)
                        {
                            invokeKeywordParameters.Add("Value", ArgumentList[1]);
                        }
                    }
                    else
                    {
                        invokeKeywordParameters.Add("Value", ArgumentList[0]);
                    }
                }
                if (keyword.NameMode != DynamicKeywordNameMode.NameRequired && !invokeKeywordParameters.ContainsKey("Name"))
                {
                    invokeKeywordParameters.Add("Name", "");
                }

                // Now re-invoke the command so that we may properly trigger its keyword handler
                psHelper.InvokeCommand(
                    MyInvocation.InvocationName,
                    invokeKeywordParameters
                );
            }
            else
            {
                // Write a message to the verbose log when invoking a keyword
                // SourceMetadata format: FileName::LineNumber::ColumnNumber::TextExtent
                string[] invocationDetails = SourceMetadata.Split(new string[] { "::" }, 4, StringSplitOptions.None);

                WriteVerbose(string.Format("Invoking keyword {0} at File {1} Ln {2} Col {3} (Keyword name = {4}; Command line = {5})", KeywordData.Keyword, invocationDetails[0], invocationDetails[1], invocationDetails[2], Name, invocationDetails[3]));

                // Define the ordered dictionary that will hold the properties of the object generated from the keyword
                OrderedDictionary properties = new OrderedDictionary(StringComparer.OrdinalIgnoreCase);

                string keywordPath = KeywordManager.GetPath(KeywordData);

                switch (KeywordData.BodyMode)
                {
                    case DynamicKeywordBodyMode.ScriptBlock:
                        {
                            // Invoke the OnInvoking action if one exists
                            ScriptBlock onInvokingAction = KeywordManager.GetOnInvokingEventHandler(KeywordData);
                            if (onInvokingAction != null)
                            {
                                // TODO: Decide if we want to suppress whatif/confirm here or not (probably not, could be useful)
                                Collection<PSObject> onInvokingResults = psHelper.InvokeScriptBlock(
                                    onInvokingAction,
                                    new OrderedDictionary {
                                        { "Name", Name }
                                    });
                                foreach (PSObject psObject in onInvokingResults)
                                {
                                    WriteObject(psObject);
                                }
                            }

                            // Set a flag indicating whether we are inside of a DSL or not to help manage the keyword
                            // precedence and availability "magic"
                            bool insideDsl = KeywordManager.IsDslKeyword(KeywordData);

                            try
                            {
                                // If we're invoking a DSL keyword, we need to juggle the keywords that are available
                                // at any given moment
                                if (insideDsl)
                                {
                                    // Push the current keyword onto the stack
                                    KeywordManager.PushDslKeyword(KeywordData);
                                }

                                // Now invoke the script block to process the inner keyword
                                Collection<PSObject> scriptBlockResults = psHelper.InvokeScriptBlock((ScriptBlock)Value);
                                if (scriptBlockResults != null)
                                {
                                    foreach (PSObject psObject in scriptBlockResults)
                                    {
                                        string key = psObject.Properties.Any(x => string.Compare(x.Name, "Name", true) == 0) ? (string)psObject.Properties["Name"].Value : psObject.TypeNames[0];
                                        properties.Add(key, psObject);
                                    }
                                }
                            }
                            finally
                            {
                                if (insideDsl)
                                {
                                    // Pop the keyword off of the stack
                                    KeywordManager.PopDslKeyword();
                                }
                            }

                            break;
                        }

                    case DynamicKeywordBodyMode.Hashtable:
                        {
                            Hashtable hashtable = (Hashtable)Value;
                            foreach (string key in hashtable.Keys)
                            {
                                properties.Add(key, hashtable[key]);
                            }
                            break;
                        }

                    default:
                        {
                            string message = "The Invoke-Keyword cmdlet does not the Command body mode.";
                            ArgumentException exception = new ArgumentException(message, "KeywordData");
                            ErrorRecord errorRecord = new ErrorRecord(exception, "CommandBodyModeNotSupported", ErrorCategory.InvalidArgument, KeywordData);
                            ThrowTerminatingError(errorRecord);
                            break;
                        }
                }

                // Generate a custom object that contains the DSL data
                PSObject dslObject = properties.Count > 0 ? new PSObject(properties) : new PSObject();
                dslObject.TypeNames.Insert(0, string.Format("LanguagePx.DslAutomaticOutput#{0}", keywordPath));
                if (string.IsNullOrEmpty(Name))
                {
                    Name = KeywordData.Keyword;
                }
                dslObject.Properties.Add(new PSNoteProperty("Name", Name));
                dslObject.Properties.Add(new PSNoteProperty("ProducedByKeyword", keywordPath));

                // If there is an OnInvoked event handler for the keyword, invoke it.
                ScriptBlock onInvokedAction = KeywordManager.GetOnInvokedEventHandler(KeywordData);
                if (onInvokedAction == null)
                {
                    WriteObject(dslObject);
                }
                else
                {
                    // TODO: Decide if we want to suppress whatif/confirm here or not (probably not, could be useful)
                    Collection<PSObject> onInvokedResults = psHelper.InvokeCommand(
                        "ForEach-Object",
                        new OrderedDictionary {
                            { "InputObject", dslObject },
                            { "Process", onInvokedAction }
                        });
                    foreach (PSObject psObject in onInvokedResults)
                    {
                        WriteObject(psObject);
                    }
                }
            }

            // Invoke the base class EndProcessing method
            base.EndProcessing();
        }
    }
}