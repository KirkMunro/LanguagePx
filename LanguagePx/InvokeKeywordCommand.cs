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
            "Keyword"
    )]
    [OutputType(typeof(object))]
    public class InvokeKeywordCommand : PSCmdlet
    {
        [Parameter(
            Mandatory = true,
            HelpMessage = "The keyword that is being invoked."
        )]
        [ValidateNotNull()]
        public DynamicKeyword KeywordData;

        [Parameter(
            Mandatory = true,
            HelpMessage = "The name associated with the keyword invocation. If name is not required, this string will be empty"
        )]
        [AllowEmptyString()]
        public string Name;

        [Parameter(
            Mandatory = true,
            HelpMessage = "The body of the keyword. This must be a script block or a hashtable."
        )]
        [ValidateNotNull()]
        public object Value;

        [Parameter(
            Mandatory = true,
            HelpMessage = "A string identifying the location where the keyword was invoked."
        )]
        [ValidateNotNullOrEmpty()]
        public string SourceMetadata;

        PowerShellHelper psHelper = null;

        protected override void BeginProcessing()
        {
            // Create the PowerShell helper
            psHelper = new PowerShellHelper(this);

            // If the cmdlet was invoked directly, throw a terminating error
            if (Regex.IsMatch(MyInvocation.InvocationName,@"^(LanguagePx\\)?Invoke-Keyword$"))
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

            // Invoke the base class BeginProcessing method
            base.BeginProcessing();
        }

        protected override void EndProcessing()
        {
            // Write a message to the verbose log when invoking a keyword
            // SourceMetadata format: FileName::LineNumber::ColumnNumber::TextExtent
            string[] invocationDetails = SourceMetadata.Split(new string[] { "::" }, StringSplitOptions.None);

            WriteVerbose(string.Format("Invoking keyword {0} at File {1} Ln {2} Col {3} (Keyword name = {4}; Command line = {5})", KeywordData.Keyword, invocationDetails[0], invocationDetails[1], invocationDetails[2], Name, invocationDetails[3]));

            // Define the ordered dictionary that will hold the properties of the object generated from the keyword
            OrderedDictionary properties = new OrderedDictionary(StringComparer.OrdinalIgnoreCase);

            string keywordPath = DslDatabase.GetPath(KeywordData);

            switch (KeywordData.BodyMode)
            {
                case DynamicKeywordBodyMode.ScriptBlock:
                    {
                        // Invoke the OnInvoking action if one exists
                        ScriptBlock onInvokingAction = DslDatabase.GetOnInvokingEventHandler(KeywordData);
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

                        // Get the AliasInfo object for the keyword
                        Collection<PSObject> getKeywordAliasResults = psHelper.InvokeCommand(
                            "Get-Alias",
                            new OrderedDictionary {
                                { "Name", KeywordData.Keyword },
                                { "Scope", 0 },
                                { "ErrorAction", ActionPreference.Ignore }
                            });

                        AliasInfo keywordAliasInfo = getKeywordAliasResults.Count == 0 ? null : (AliasInfo)getKeywordAliasResults[0].BaseObject;
                        Collection<DynamicKeyword> conflictingKeywords = new Collection<DynamicKeyword>();
                        Collection<AliasInfo> conflictingAliases = new Collection<AliasInfo>();
                        List<DynamicKeyword> childKeywords = DslDatabase.GetChildKeywords(KeywordData);

                        try
                        {
                            // Remove the alias for the current keyword temporarily; this prevents it
                            // from being invoked inside of itself
                            if (keywordAliasInfo != null)
                            {
                                psHelper.InvokeCommand(
                                    "Remove-Item",
                                    new OrderedDictionary {
                                        { "LiteralPath", string.Format("Alias::{0}", keywordAliasInfo.Name) },
                                        { "Force", true },
                                        { "Confirm", false },
                                        { "WhatIf", false },
                                        { "ErrorAction", ActionPreference.Stop }
                                    });
                            }

                            // Remove the keyword itself temporarily; this prevents it from being invoked
                            // inside of itself
                            DynamicKeyword.RemoveKeyword(KeywordData.Keyword);

                            // Determine if there are any keywords or aliases to replace
                            foreach (DynamicKeyword childKeyword in childKeywords)
                            {
                                DynamicKeyword keywordWithSameName = DynamicKeyword.GetKeyword(childKeyword.Keyword);
                                if (keywordWithSameName != null)
                                {
                                    conflictingKeywords.Add(keywordWithSameName);
                                    DynamicKeyword.RemoveKeyword(keywordWithSameName.Keyword);
                                }
                                Collection<PSObject> getChildKeywordAliasResults = psHelper.InvokeCommand(
                                    "Get-Alias",
                                    new OrderedDictionary {
                                        { "Name", childKeyword.Keyword },
                                        { "Scope", 0 },
                                        { "ErrorAction", ActionPreference.Ignore }
                                    });
                                if (getChildKeywordAliasResults.Count > 0)
                                {
                                    AliasInfo conflictingAlias = (AliasInfo)getChildKeywordAliasResults[0].BaseObject;
                                    conflictingAliases.Add(conflictingAlias);
                                    psHelper.InvokeCommand(
                                        "Remove-Item",
                                        new OrderedDictionary {
                                        { "LiteralPath", string.Format("Alias::{0}", conflictingAlias.Name) },
                                        { "Force", true },
                                        { "Confirm", false },
                                        { "WhatIf", false },
                                        { "ErrorAction", ActionPreference.Stop }
                                    });
                                }

                                DynamicKeyword.AddKeyword(childKeyword);
                                psHelper.InvokeCommand(
                                    "New-Alias",
                                    new OrderedDictionary {
                                        { "Name", childKeyword.Keyword },
                                        { "Value", "Invoke-Keyword" },
                                        { "Scope", 0 },
                                        { "ErrorAction", ActionPreference.Stop },
                                        { "Confirm", false },
                                        { "WhatIf", false }
                                    });
                            }

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
                            // Remove the child keywords and their aliases
                            foreach (DynamicKeyword childKeyword in childKeywords)
                            {
                                psHelper.InvokeCommand(
                                    "Remove-Item",
                                    new OrderedDictionary {
                                        { "LiteralPath", string.Format("Alias::{0}", childKeyword.Keyword) },
                                        { "Force", true },
                                        { "Confirm", false },
                                        { "WhatIf", false },
                                        { "ErrorAction", ActionPreference.Stop }
                                    });
                                DynamicKeyword.RemoveKeyword(childKeyword.Keyword);
                            }

                            // Restore any keywords and aliases that were replaced
                            foreach (DynamicKeyword conflictingKeyword in conflictingKeywords)
                            {
                                DynamicKeyword.AddKeyword(conflictingKeyword);
                            }
                            foreach (AliasInfo conflictingAlias in conflictingAliases)
                            {
                                psHelper.InvokeCommand(
                                    "New-Alias",
                                    new OrderedDictionary {
                                        { "Name", conflictingAlias.Name },
                                        { "Scope", 0 },
                                        { "Option", conflictingAlias.Options },
                                        { "ErrorAction", ActionPreference.Stop },
                                        { "Confirm", false },
                                        { "WhatIf", false }
                                    });
                            }

                            // Put the keyword back so that it continues to function as usual
                            DynamicKeyword.AddKeyword(KeywordData);

                            // Now put the alias back so that they keyword continues to function
                            if (keywordAliasInfo != null)
                            {
                                psHelper.InvokeCommand(
                                    "Set-Alias",
                                    new OrderedDictionary{
                                        { "Name", KeywordData.Keyword },
                                        { "Value", "Invoke-Keyword" },
                                        { "ErrorAction", ActionPreference.Stop },
                                        { "Confirm", false },
                                        { "WhatIf", false }
                                    });
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
            ScriptBlock onInvokedAction = DslDatabase.GetOnInvokedEventHandler(KeywordData);
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

            // Invoke the base class EndProcessing method
            base.EndProcessing();
        }
    }
}