using System;
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
        VerbsCommon.New,
        "DomainSpecificLanguage"
    )]
    [OutputType(typeof(void))]
    public class NewDomainSpecificLanguageCommand : PSCmdlet
    {
        [Parameter(
            Position = 0,
            Mandatory = true,
            HelpMessage = "The name of the domain-specific language."
        )]
        [ValidateNotNullOrEmpty()]
        public string Name;

        [Parameter(
            Mandatory = true,
            HelpMessage = "The syntax definition for the domain-specific language."
        )]
        [ValidateNotNull()]
        public ScriptBlock Syntax;

        enum ParseMode
        {
            Undefined,
            Command,
            Property
        };

        PowerShellHelper psHelper = null;

        Dictionary<string, Type> typeNameMap = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        void ThrowSyntaxError(Ast ast, string message)
        {
            PSArgumentException exception = new PSArgumentException(string.Format("Syntax error. {0}", message), "Syntax");
            ErrorRecord errorRecord = new ErrorRecord(exception, "SyntaxError", ErrorCategory.InvalidArgument, ast);
            ThrowTerminatingError(errorRecord);
        }

        void ParseSyntaxTree(ScriptBlockAst scriptBlockAst, string parentKeywordName = null)
        {
            if (scriptBlockAst == null)
            {
                ThrowSyntaxError(Syntax.Ast, "All commands used in a domain-specific language definition must be in one of two formats: '<CommandName> [Name] {...}' or '<Type> [<PropertyName>]'.");
            }

            if (scriptBlockAst.BeginBlock != null)
            {
                ThrowSyntaxError(scriptBlockAst, "Begin blocks are not supported in domain-specific language definitions.");
            }

            if (scriptBlockAst.ProcessBlock != null)
            {
                ThrowSyntaxError(scriptBlockAst, "Process blocks are not supported in domain-specific language definitions.");
            }

            ParseMode parseMode = ParseMode.Undefined;
            List<string> keywordNames = new List<string>();

            foreach (StatementAst statementAst in scriptBlockAst.EndBlock.Statements)
            {
                PipelineAst pipelineAst = statementAst as PipelineAst;
                if (pipelineAst == null)
                {
                    ThrowSyntaxError(statementAst, "All commands used in a domain-specific language definition must be in one of two formats: '<CommandName> [Name] {...}' or '<Type> [<PropertyName>]'.");
                }

                if (pipelineAst.PipelineElements.Count > 1)
                {
                    ThrowSyntaxError(pipelineAst, "Pipelines are not supported in domain-specific language definitions.");
                }

                CommandAst commandAst = pipelineAst.PipelineElements[0] as CommandAst;
                if (commandAst == null)
                {
                    ThrowSyntaxError(pipelineAst.PipelineElements[0], "All commands used in a domain-specific language definition must be in one of two formats: '<CommandName> [Name] {...}' or '<Type> [<PropertyName>]'.");
                }

                if (commandAst.CommandElements.Count < 2 || commandAst.CommandElements.Count > 3)
                {
                    ThrowSyntaxError(commandAst, "All commands used in a domain-specific language definition must be in one of two formats: '<CommandName> [Name] {...}' or '<Type> [<PropertyName>]'.");
                }

                ScriptBlockExpressionAst scriptBlockExpressionAst = commandAst.CommandElements[commandAst.CommandElements.Count - 1] as ScriptBlockExpressionAst;
                if (scriptBlockExpressionAst != null)
                {
                    if (scriptBlockExpressionAst.ScriptBlock.EndBlock.Statements.Count == 0)
                    {
                        ThrowSyntaxError(scriptBlockExpressionAst, "Empty script blocks are not permitted in domain-specific language definitions.");
                    }

                    if (commandAst.CommandElements.Where((x, i) => i != commandAst.CommandElements.Count - 1).Any(x => !(x is StringConstantExpressionAst)))
                    {
                        ThrowSyntaxError(commandAst, "Expressions are not permitted in domain-specific language definitions.");
                    }

                    if (parseMode == ParseMode.Property)
                    {
                        ThrowSyntaxError(scriptBlockExpressionAst.Parent, "You cannot define both commands and properties in the same script block in domain-specific language definitions.");
                    }

                    parseMode = ParseMode.Command;

                    string keywordName = commandAst.CommandElements[0].Extent.Text;
                    if (!Regex.IsMatch(keywordName,@"^[A-Za-z]+$"))
                    {
                        ThrowSyntaxError(commandAst, "Keyword names can only contain alphabetic characters.");
                    }

                    if (string.Compare(keywordName, parentKeywordName, true) == 0)
                    {
                        ThrowSyntaxError(commandAst, "A keyword name cannot be the same as that of its parent.");
                    }

                    if (parentKeywordName == null)
                    {
                        Collection<PSObject> results = psHelper.InvokeCommand(
                            "Get-Alias",
                            new OrderedDictionary {
                                { "Name", keywordName },
                                { "Scope", "Script" },
                                { "ErrorAction", ActionPreference.Ignore }
                            });
                        if ((results.Count > 0) && (string.Compare(((AliasInfo)results[0].BaseObject).Definition, "Invoke-Keyword", true) != 0))
                        {
                            ThrowSyntaxError(commandAst, string.Format("A conflicting alias by the name of {0} already exists in the current scope. The {1} domain-specific language requires this alias in order to function properly. The domain-specific language cannot be defined until this alias conflict has been removed.", keywordName, Name));
                        }
                    }

                    string nameMode = commandAst.CommandElements.Count == 3 ? commandAst.CommandElements[1].Extent.Text : null;

                    if ((nameMode != null) &&
                        (string.Compare(nameMode, "[Name]", true) != 0) &&
                        (string.Compare(nameMode, "Name", true) != 0))
                    {
                        ThrowSyntaxError(commandAst, "When providing the name mode for a keyword, the only valid values are 'Name' (when the name is required) or '[Name]' (when the name is optional).");
                    }

                    if (keywordNames.Contains(keywordName, StringComparer.OrdinalIgnoreCase))
                    {
                        ThrowSyntaxError(commandAst, "Keywords can only be defined once per scriptblock in a domain-specific language definition.");
                    }
                    keywordNames.Add(keywordName);

                    ParseSyntaxTree(scriptBlockExpressionAst.ScriptBlock, keywordName);
                }
                else
                {
                    if (commandAst.CommandElements.Any(x => !(x is StringConstantExpressionAst)))
                    {
                        ThrowSyntaxError(commandAst, "Expressions are not permitted in domain-specific language definitions.");
                    }

                    string typeName = commandAst.CommandElements[0].Extent.Text;
                    if (!typeNameMap.ContainsKey(typeName))
                    {
                        Collection<PSObject> results = psHelper.InvokeScript(
                            "param([string]$TypeName); $TypeName -as [System.Type]",
                            new OrderedDictionary{
                                { "TypeName", commandAst.CommandElements[0].Extent.Text }
                            },
                            false,
                            true);

                        if (results.Count == 0)
                        {
                            ThrowSyntaxError(commandAst, string.Format("{0} is not a valid type name.", commandAst.CommandElements[0].Extent.Text));
                        }

                        Type type = (Type)results[0].BaseObject;

                        typeNameMap.Add(typeName, type);
                    }

                    string propertyName = Regex.Replace(commandAst.CommandElements[1].Extent.Text, @"^\[(\w+)\]$", "$1");
                    if (!Regex.IsMatch(propertyName, @"^\w+$") && !Regex.IsMatch(propertyName, @"^\[\w+\]$"))
                    {
                        ThrowSyntaxError(commandAst, "Property names can only contain alpha-numeric characters and underscores.");
                    }

                    if (parseMode == ParseMode.Command)
                    {
                        ThrowSyntaxError(scriptBlockExpressionAst.Parent, "You cannot define both commands and properties in the same script block in domain-specific language definitions.");
                    }

                    if (parentKeywordName == null)
                    {
                        ThrowSyntaxError(scriptBlockExpressionAst.Parent, "Properties can only be defined inside of a command script block in domain-specific language definitions.");
                    }

                    parseMode = ParseMode.Property;
                }
            }
        }

        List<DynamicKeywordProperty> CreateDsl(ScriptBlockAst scriptBlockAst, int parentKeywordId = 0, string parentKeywordPath = null)
        {
            List<DynamicKeywordProperty> propertyList = new List<DynamicKeywordProperty>();

            foreach (StatementAst statementAst in scriptBlockAst.EndBlock.Statements)
            {
                PipelineAst pipelineAst = statementAst as PipelineAst;
                CommandAst commandAst = pipelineAst.PipelineElements[0] as CommandAst;
                ScriptBlockExpressionAst scriptBlockExpressionAst = commandAst.CommandElements[commandAst.CommandElements.Count - 1] as ScriptBlockExpressionAst;
                if (scriptBlockExpressionAst != null)
                {
                    // If we got this far, then we have a keyword definition!
                    string keywordName = commandAst.CommandElements[0].Extent.Text;
                    string keywordPath = string.IsNullOrEmpty(parentKeywordPath) ? keywordName : string.Format(@"{0}\{1}", parentKeywordPath, keywordName);
                    string nameField = commandAst.CommandElements.Count < 3 ? null : commandAst.CommandElements[1].Extent.Text;
                    DynamicKeyword keyword = new DynamicKeyword() {
                        Keyword = keywordName,
                        NameMode = nameField == null ? DynamicKeywordNameMode.NoName : string.Compare(nameField,"[Name]",true) == 0 ? DynamicKeywordNameMode.OptionalName : DynamicKeywordNameMode.NameRequired
                    };

                    List<DynamicKeywordProperty> nestedPropertyList = CreateDsl(scriptBlockExpressionAst.ScriptBlock, keyword.GetHashCode(), keywordPath);
                    keyword.BodyMode = nestedPropertyList.Count > 0 ? DynamicKeywordBodyMode.Hashtable : DynamicKeywordBodyMode.ScriptBlock;

                    foreach (DynamicKeywordProperty nestedProperty in nestedPropertyList)
                    {
                        keyword.Properties.Add(nestedProperty.Name, nestedProperty);
                    }

                    // Use the keyword manager to add the DSL keyword to the keyword repository; this also
                    // creates an alias for visible keywords if a coresponding alias does not already exist
                    // in the script scope (for DSLs, visible keywords = root-level keywords)
                    KeywordManager.AddDslKeyword(Name, keyword, parentKeywordId, keywordPath, Syntax.Module);

                    // Once the keyword is created, if the keyword is at the root of the DSL, and if the DSL
                    // is being defined inside of a module, export the keyword alias and then remove the
                    // internal alias that was used to perform the export. This must happen to ensure that
                    // there are no internal keywords left behind inside the module so that DSL removal
                    // performed as part of a module OnRemove event can properly remove the keywords that
                    // this DSL uses.
                    if (parentKeywordId == 0)
                    {
                        Collection<PSObject> results = psHelper.InvokeCommandAssertNotNull(
                            "Get-Alias",
                            new OrderedDictionary {
                                { "Name", keywordName },
                                { "Scope", "Script" },
                                { "ErrorAction", ActionPreference.Stop }
                            });
                        AliasInfo aliasInfo = results.Count == 0 ? null : results[0].BaseObject as AliasInfo;
                        if ((Syntax.Module != null) && (aliasInfo != null))
                        {
                            psHelper.InvokeCommand(
                                "Export-ModuleMember",
                                new OrderedDictionary {
                                    { "Alias", aliasInfo.Name },
                                    { "ErrorAction", ActionPreference.Stop }
                                });

                            psHelper.InvokeCommand(
                                "Remove-Item",
                                new OrderedDictionary {
                                    { "LiteralPath", string.Format("Alias::{0}", keywordName) },
                                    { "Force", true },
                                    { "Confirm", false },
                                    { "WhatIf", false },
                                    { "ErrorAction", ActionPreference.Stop }
                                });
                        }
                    }
                }
                else
                {
                    // If we got this far, then we have a property definition!
                    DynamicKeywordProperty property = new DynamicKeywordProperty()
                    {
                        Name = Regex.Replace(commandAst.CommandElements[1].Extent.Text, @"^\[(\w+)\]$", @"$1"),
                        Mandatory = !Regex.IsMatch(commandAst.CommandElements[1].Extent.Text, @"^\[\w+\]$"),
                        TypeConstraint = typeNameMap[commandAst.CommandElements[0].Extent.Text].FullName
                    };

                    propertyList.Add(property);
                }
            }

            return propertyList;
        }

        protected override void BeginProcessing()
        {
            // Create the PowerShell helper
            psHelper = new PowerShellHelper(this);

            // Ensure the Keyword Manager is configured to use the appropriate PowerShellHelper instance
            KeywordManager.PSHelper = psHelper;

            base.BeginProcessing();
        }

        protected override void EndProcessing()
        {
            // Ensure the Keyword Manager is configured to use the appropriate PowerShellHelper instance
            KeywordManager.PSHelper = psHelper;

            ScriptBlockAst scriptBlockAst = Syntax.Ast as ScriptBlockAst;

            ParseSyntaxTree(scriptBlockAst);
            CreateDsl(scriptBlockAst);

            base.EndProcessing();
        }
    }
}