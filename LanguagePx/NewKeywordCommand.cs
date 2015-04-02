using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;


namespace LanguagePx
{
    //[Cmdlet(
    //    VerbsCommon.New,
    //    "Keyword"
    //)]
    //[OutputType(typeof(void))]
    //public class NewKeywordCommand : PSCmdlet
    //{
    //    // Possible keywords that could be implemented:
    //    // breakpoint - port of breakpoint/Enter-Debugger cmdlet; or not -- not sure about this one
    //    // ifdebug - port of ifdebug/Invoke-IfDebug cmdlet
    //    // safe - stop *Preference variables from affecting Confirm/WhatIf control over Get-* commands that use Set-Variable
    //    //      - alternative: use PSDefaultParameterValue for Set-Variable for Confirm and WhatIf -- that would fix it
    //    // strict - alternative to snippets
    //
    //    // BUTBUTBUTBUT!!!!! Downside is that keywords are dependent on PowerShell 4.0+, and those other modules work on PowerShell 3.0+,
    //    // SO, while support for keywords like this would be useful, they won't be great for modules unless you're already dependent on 4.0+.
    //
    //    // Still, these possibilities highlight the benefit of being able to create individual keywords that:
    //    // a) have scriptblock syntax, for items like ifdebug, which we really don't need a cmdlet for (nor do we want one -- best practice command invocations shouldn't be aliases)
    //    // b) have command syntax, for items like strict, which simply translate into a bunch of other things via ast manipulation (like snippets, but without the snippet folder requirement)
    //}
}
