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

            foreach (DynamicKeyword keyword in KeywordManager.GetDslKeywords(Name))
            {
                KeywordManager.RemoveKeyword(keyword);
            }

            base.EndProcessing();
        }
    }
}