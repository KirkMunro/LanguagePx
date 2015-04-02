using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;

namespace LanguagePx
{
    public class PowerShellHelper
    {
        PowerShell ps = null;
        PSCmdlet psCmdlet = null;

        public PowerShellHelper(PSCmdlet psCmdlet)
        {
            this.psCmdlet = psCmdlet;
        }

        void InitializePowerShell()
        {
            if (ps == null)
            {
                ps = PowerShell.Create(RunspaceMode.CurrentRunspace);
            }
            ps.Commands.Clear();
        }

        Collection<PSObject> InvokePowerShellWithErrorHandler(bool terminateOnError = false) {
            Collection<PSObject> results = ps.Invoke();
            if (ps.HadErrors)
            {
                foreach (ErrorRecord error in ps.Streams.Error)
                {
                    if (terminateOnError)
                    {
                        psCmdlet.ThrowTerminatingError(error);
                    }
                    else
                    {
                        psCmdlet.WriteError(error);
                    }
                }
            }
            if ((results == null) ||
                ((results.Count == 1) && (results[0] == null)))
            {
                return new Collection<PSObject>();
            }
            return results;
        }

        public Collection<PSObject> InvokeScript(string script, IDictionary parameters = null, bool terminateOnError = false, bool invokeInChildScope = false)
        {
            InitializePowerShell();

            ps.AddScript(script, invokeInChildScope);
            if (parameters != null)
            {
                ps.AddParameters(parameters);
            }

            return InvokePowerShellWithErrorHandler(terminateOnError);
        }

        public Collection<PSObject> InvokeScriptBlock(ScriptBlock scriptBlock, IDictionary parameters = null, bool terminateOnError = false, bool invokeInChildScope = false)
        {
            return InvokeScript(scriptBlock.ToString(), parameters, terminateOnError, invokeInChildScope);
        }

        public Collection<PSObject> InvokeCommand(string commandName, object[] argumentList, bool terminateOnError = false, bool invokeInChildScope = false)
        {
            InitializePowerShell();

            ps.AddCommand(commandName, invokeInChildScope);
            foreach (object argument in argumentList)
            {
                ps.AddArgument(argument);
            }

            return InvokePowerShellWithErrorHandler(terminateOnError);
        }

        public Collection<PSObject> InvokeCommand(string commandName, IDictionary parameters = null, bool terminateOnError = false, bool invokeInChildScope = false)
        {
            InitializePowerShell();

            ps.AddCommand(commandName, invokeInChildScope);
            if (parameters != null)
            {
                ps.AddParameters(parameters);
            }

            return InvokePowerShellWithErrorHandler(terminateOnError);
        }

        public Collection<PSObject> InvokeCommandAssertNotNull(string commandName, IDictionary parameters = null, bool terminateOnError = false, bool invokeInChildScope = false)
        {
            Collection<PSObject> result = InvokeCommand(commandName, parameters, terminateOnError, invokeInChildScope);

            if ((result == null) || (result.Count == 0))
            {
                ErrorRecord errorRecord = new ErrorRecord(
                    new Exception(string.Format("Command {0} was expected to return a value. It returned null.", commandName)),
                    "NullResult",
                    ErrorCategory.InvalidResult,
                    null
                );
                psCmdlet.ThrowTerminatingError(errorRecord);
            }

            return result;
        }
    }
}