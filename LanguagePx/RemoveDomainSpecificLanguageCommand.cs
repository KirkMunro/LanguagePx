using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;

namespace LanguagePx
{
    [Cmdlet(
        VerbsCommon.Remove,
        "DomainSpecificLanguage"
    )]
    [OutputType(typeof(void))]
    public class RemoveDomainSpecificLanguageCommand : PSCmdlet
    {
        [Parameter(
            Position = 0,
            Mandatory = true,
            HelpMessage = "The name of the domain-specific language."
        )]
        [ValidateNotNullOrEmpty()]
        public string Name;

        PowerShellHelper psHelper = null;

        protected override void BeginProcessing()
        {
            psHelper = new PowerShellHelper(this);
            base.BeginProcessing();
        }

        protected override void EndProcessing()
        {
            foreach (DynamicKeyword keyword in DslDatabase.GetDslRootKeywords(Name))
            {
                if (DynamicKeyword.GetKeyword(keyword.Keyword) != null)
                {
                    DynamicKeyword.RemoveKeyword(keyword.Keyword);
                }

                psHelper.InvokeCommand(
                    "Remove-Item",
                    new OrderedDictionary {
                        { "LiteralPath", string.Format("Alias::{0}", keyword.Keyword) },
                        { "Force", true },
                        { "ErrorAction", ActionPreference.Ignore },
                        { "Confirm", false },
                        { "WhatIf", false }
                    });
            }

            base.EndProcessing();
        }
    }
}